using System;
using System.Collections.Generic;
using System.Linq;

namespace vmsOpenAcars.Helpers
{
    internal enum TurnSide { Straight, Left, Right }

    /// <summary>Qué avisar. El texto y la voz los pone quien consume, en su idioma.</summary>
    internal enum RaasCalloutType
    {
        None,
        HoldShortApproaching,
        HoldShortStop,
        TurnAhead,
        TurnNow,
        OffRoute,
        RouteComplete
    }

    internal sealed class RaasCallout
    {
        public RaasCalloutType Type;
        public string          Taxiway;      // calle del aviso (la que hay que tomar)
        public string          Runway;       // pista, en los avisos de espera
        public double          DistanceM;
        public TurnSide        Side;
        public string          Key = "";     // identidad de la situación, para no repetirla
    }

    /// <summary>La ruta de rodaje escrita y editable: "B M K V".</summary>
    internal sealed class TaxiRoutePlan
    {
        internal List<string> Names = new List<string>();

        /// <summary>
        /// Acepta lo que un piloto escribe de verdad: espacios, comas, guiones, flechas o la
        /// palabra "via" ("B M K V", "b,m,k1,v", "B -> M -> K1"). Se normaliza a mayúsculas y
        /// sin duplicados consecutivos; un nombre repetido más adelante se conserva (hay rutas
        /// que vuelven a la misma calle).
        /// </summary>
        internal static TaxiRoutePlan Parse(string text)
        {
            var plan = new TaxiRoutePlan();
            if (string.IsNullOrWhiteSpace(text)) return plan;

            string cleaned = text.ToUpperInvariant()
                                 .Replace("->", " ").Replace(">", " ").Replace("→", " ")
                                 .Replace(",", " ").Replace(";", " ").Replace("-", " ")
                                 .Replace("/", " ").Replace("VIA", " ").Replace("VÍA", " ");
            foreach (string raw in cleaned.Split(new[] { ' ', '\t', '\r', '\n' },
                                                StringSplitOptions.RemoveEmptyEntries))
            {
                string name = raw.Trim();
                if (name.Length == 0) continue;
                if (plan.Names.Count > 0
                    && string.Equals(plan.Names.Last(), name, StringComparison.OrdinalIgnoreCase))
                    continue;
                plan.Names.Add(name);
            }
            return plan;
        }

        internal string ToText() => string.Join(" ", Names);

        /// <summary>
        /// ¿Las dos rutas son la misma? Se comparan ya normalizadas, para que «c b m» y «C B M» no
        /// cuenten como un cambio. Lo usa el coordinador para decidir si merece la pena volver a
        /// preguntar por la ruta cuando el avión cambia de punto de inicio (fin del pushback):
        /// en el rodaje real de SKBO la propuesta desde el puesto y desde el fin del pushback es
        /// la misma, así que volver a abrir el popup solo sería ruido.
        /// </summary>
        internal static bool SameRoute(string a, string b)
            => string.Equals(Parse(a).ToText(), Parse(b).ToText(),
                             StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Dónde está el próximo giro y para qué lado.</summary>
    internal sealed class RouteGuidance
    {
        public bool      HasRoute;
        public bool      OnRoute;
        public bool      Done;
        public string    NextTaxiway = "";
        public double    DistanceToTurnM = double.NaN;
        public TurnSide  Side = TurnSide.Straight;
        public int       IndexOnRoute = -1;
    }

    /// <summary>
    /// Motor de avisos tipo RAAS, más la guía giro a giro de la ruta elegida.
    ///
    /// No calcula geometría de red —eso es <see cref="TaxiGraph"/>— ni habla: recibe los hechos
    /// ya resueltos (¿estoy en pista?, ¿a qué distancia está el hold-short?, ¿dónde está el
    /// próximo giro?) y decide **qué** avisar y **cuándo**, que es lo que hay que poder probar.
    /// Sin I/O y sin reloj propio: el instante entra por parámetro.
    /// </summary>
    internal sealed class RaasAdvisor
    {
        // Umbrales en metros. Los de hold-short son los de un RAAS real: se avisa al acercarse y
        // se insiste al llegar. En el rodaje real de SKBO que originó esto, el avión pasó a 5–22 m
        // de los hold-shorts de la 14R y estuvo 135 s parado a 22 m: con 150/40 m habría avisado.
        internal const double ApproachingM = 150.0;
        internal const double StopM        = 40.0;
        internal const double TurnAheadM   = 250.0;
        internal const double TurnNowM     = 60.0;

        /// <summary>Antirrebote: la misma situación no se repite antes de este tiempo.</summary>
        internal const double RepeatCooldownSec = 20.0;

        // «Fuera de ruta» no se dice porque una calle no esté en la lista, sino porque el avión
        // se ha perdido. En el rodaje real del `MNjR664PBAr25RbD` la ruta que propuso el grafo
        // era la más corta geométricamente (`C P G N H M K K2 K1`) y el piloto hizo la de ATC
        // (`C B9 B M K K1 V`): mismo destino por otras calles, y el aviso saltó 8 veces
        // seguidas. Ahora hay que insistir `OffRoutePersistSec` sin acercarse a la pista.
        internal const double OffRoutePersistSec = 15.0;

        /// <summary>Acercarse a la pista al menos esto reinicia la cuenta: no está perdido.</summary>
        internal const double OffRouteProgressM = 50.0;

        private readonly Dictionary<string, DateTime> _spoken =
            new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);

        private DateTime? _offRouteSince;
        private double    _offRouteBestM = double.NaN;
        private bool      _routeCompleteAnnounced;

        public sealed class Inputs
        {
            public bool       OnRunway;
            public double     GroundSpeedKt;
            public string     HoldShortRunway;      // pista cuyo hold-short está delante, si hay
            public double     HoldShortDistanceM = double.NaN;
            public bool       HeadingTowardHoldShort;
            public string     ActiveTaxiway;
            public RouteGuidance Guidance;

            /// <summary>
            /// Distancia recta al umbral de la pista elegida, si se conoce. Es lo que distingue
            /// «voy por otra calle pero me acerco» de «me he perdido»: sin dato, el aviso se
            /// decide solo por insistencia (degradar sin datos, nunca bloquear por suposición).
            /// </summary>
            public double     DistanceToRunwayM = double.NaN;

            /// <summary>Copia ligera: el motor guarda el objeto entre llamadas en los tests.</summary>
            public Inputs Clone() => (Inputs)MemberwiseClone();
        }

        /// <summary>
        /// Devuelve el aviso que toca, o uno con <see cref="RaasCalloutType.None"/> si no hay
        /// nada que decir (o ya se dijo hace menos de <see cref="RepeatCooldownSec"/>).
        /// </summary>
        internal RaasCallout Evaluate(Inputs i, DateTime utcNow)
        {
            var candidate = Decide(i);
            var none      = new RaasCallout { Type = RaasCalloutType.None };

            // «RUTA COMPLETA» se dice una vez por guía: es la llegada a la pista, no una situación
            // que se repita, y el avión se queda dentro de la pista durante todo el despegue.
            if (candidate.Type == RaasCalloutType.RouteComplete)
            {
                if (_routeCompleteAnnounced) return none;
                _routeCompleteAnnounced = true;
                _spoken.Clear();
                _spoken[candidate.Key] = utcNow;
                return candidate;
            }

            if (candidate.Type == RaasCalloutType.OffRoute)
            {
                TrackOffRoute(i, utcNow);
                if (!OffRouteConfirmed(utcNow)) return none;   // todavía puede estar acercándose
            }
            else
            {
                _offRouteSince = null;
                _offRouteBestM = double.NaN;
            }

            if (candidate.Type == RaasCalloutType.None)
            {
                _spoken.Clear();     // la situación terminó: el próximo aviso vuelve a sonar
                return candidate;
            }

            if (_spoken.TryGetValue(candidate.Key, out DateTime last)
                && (utcNow - last).TotalSeconds < RepeatCooldownSec)
                return new RaasCallout { Type = RaasCalloutType.None };

            // Cambió la situación: lo anterior ya no aplica y no debe bloquear al nuevo aviso.
            _spoken.Clear();
            _spoken[candidate.Key] = utcNow;
            return candidate;
        }

        /// <summary>Vuelo nuevo o guía reiniciada: no se arrastra nada del rodaje anterior.</summary>
        internal void ResetRouteState()
        {
            _spoken.Clear();
            _offRouteSince          = null;
            _offRouteBestM          = double.NaN;
            _routeCompleteAnnounced = false;
        }

        /// <summary>
        /// Sigue el episodio de «fuera de ruta». Cada vez que el avión se acerca a la pista más
        /// de <see cref="OffRouteProgressM"/> la cuenta se reinicia: va por otro camino, pero va.
        /// Un aviso de desvío que salta mientras el avión avanza hacia su pista es peor que no
        /// avisar — es exactamente lo que pasó en el rodaje real.
        /// </summary>
        private void TrackOffRoute(Inputs i, DateTime utcNow)
        {
            if (!_offRouteSince.HasValue)
            {
                _offRouteSince = utcNow;
                _offRouteBestM = i.DistanceToRunwayM;
                return;
            }
            if (!double.IsNaN(i.DistanceToRunwayM)
                && (double.IsNaN(_offRouteBestM) || i.DistanceToRunwayM < _offRouteBestM - OffRouteProgressM))
            {
                _offRouteBestM = i.DistanceToRunwayM;
                _offRouteSince = utcNow;
            }
        }

        private bool OffRouteConfirmed(DateTime utcNow)
            => _offRouteSince.HasValue
               && (utcNow - _offRouteSince.Value).TotalSeconds >= OffRoutePersistSec;

        private static RaasCallout Decide(Inputs i)
        {
            var none = new RaasCallout { Type = RaasCalloutType.None };

            // Dentro de la pista manda el RAAS de pista (entrada, distancia restante): aquí lo
            // único que queda por decir es que la ruta de rodaje se completó, y eso es
            // precisamente haber llegado a ella. Hasta v0.9.14 se anunciaba al agotar la lista de
            // calles —el último cruce, todavía fuera de la pista—, y en el rodaje real salió
            // 3 min antes de `ENTRANDO PISTA 14R`.
            if (i.OnRunway)
            {
                if (i.Guidance != null && i.Guidance.HasRoute)
                    return new RaasCallout { Type = RaasCalloutType.RouteComplete, Key = "route-done" };
                return none;
            }

            // 1) Hold-short: lo más importante. Solo si el avión va HACIA él; si lo tiene a un
            //    lado mientras rueda en paralelo, avisar sería ruido.
            if (!string.IsNullOrEmpty(i.HoldShortRunway)
                && !double.IsNaN(i.HoldShortDistanceM)
                && i.HeadingTowardHoldShort
                && i.GroundSpeedKt >= 2.0)
            {
                if (i.HoldShortDistanceM <= StopM)
                    return new RaasCallout
                    {
                        Type = RaasCalloutType.HoldShortStop, Runway = i.HoldShortRunway,
                        DistanceM = i.HoldShortDistanceM,
                        Key = "hs-stop:" + i.HoldShortRunway
                    };
                if (i.HoldShortDistanceM <= ApproachingM)
                    return new RaasCallout
                    {
                        Type = RaasCalloutType.HoldShortApproaching, Runway = i.HoldShortRunway,
                        DistanceM = i.HoldShortDistanceM,
                        Key = "hs-appr:" + i.HoldShortRunway
                    };
            }

            // 2) Guía giro a giro de la ruta elegida.
            var g = i.Guidance;
            if (g == null || !g.HasRoute) return none;

            if (!g.OnRoute && !string.IsNullOrEmpty(i.ActiveTaxiway))
                return new RaasCallout
                {
                    Type = RaasCalloutType.OffRoute, Taxiway = i.ActiveTaxiway,
                    Key = "off-route:" + i.ActiveTaxiway
                };

            if (g.Done) return none;      // ya en la última calle: no queda giro que anunciar

            if (double.IsNaN(g.DistanceToTurnM) || string.IsNullOrEmpty(g.NextTaxiway)) return none;

            if (g.DistanceToTurnM <= TurnNowM)
                return new RaasCallout
                {
                    Type = RaasCalloutType.TurnNow, Taxiway = g.NextTaxiway,
                    DistanceM = g.DistanceToTurnM, Side = g.Side,
                    Key = "turn-now:" + g.NextTaxiway
                };
            if (g.DistanceToTurnM <= TurnAheadM)
                return new RaasCallout
                {
                    Type = RaasCalloutType.TurnAhead, Taxiway = g.NextTaxiway,
                    DistanceM = g.DistanceToTurnM, Side = g.Side,
                    Key = "turn-ahead:" + g.NextTaxiway
                };

            return none;
        }

        /// <summary>
        /// Resuelve la guía sobre la ruta planificada: en qué punto de la ruta va el avión, cuál
        /// es la próxima calle y a qué distancia está el cruce.
        ///
        /// La distancia es la recta hasta el cruce, no el recorrido por el eje de la calle: en
        /// calles que doblan la diferencia es de unos metros y el aviso solo necesita el orden de
        /// magnitud ("en 120 m"). Se prefiere eso a inventar una longitud de arco sobre segmentos
        /// que el dataset corta en trozos de 30 m.
        /// </summary>
        internal static RouteGuidance ResolveGuidance(
            IEnumerable<TaxiGraph.Segment> segments,
            TaxiRoutePlan plan, double lat, double lon, string activeTaxiway)
        {
            var g = new RouteGuidance();
            if (plan == null || plan.Names.Count == 0 || segments == null) return g;

            var list = segments.Where(s => s != null && !string.IsNullOrWhiteSpace(s.Name)).ToList();
            if (list.Count == 0) return g;
            g.HasRoute = true;

            // ¿Dónde estoy dentro de la ruta? Si la calle actual está en el plan, esa; si no,
            // se usa la más cercana de las que quedan por delante (puede ir un tramo por detrás
            // del rótulo, o haberse saltado una calle corta).
            int index = -1;
            if (!string.IsNullOrEmpty(activeTaxiway))
            {
                for (int k = 0; k < plan.Names.Count; k++)
                {
                    if (string.Equals(plan.Names[k], activeTaxiway,
                                      StringComparison.OrdinalIgnoreCase)) { index = k; break; }
                }
            }
            g.IndexOnRoute = index;
            g.OnRoute      = index >= 0;

            if (!g.OnRoute)
            {
                // Fuera de ruta: se apunta a la primera calle del plan, para poder decir "te has
                // salido, vuelve a B".
                g.NextTaxiway = plan.Names[0];
                g.DistanceToTurnM = NearestSegmentDistanceM(list, g.NextTaxiway, lat, lon);
                return g;
            }

            if (index >= plan.Names.Count - 1)
            {
                g.Done = true;
                return g;
            }

            g.NextTaxiway = plan.Names[index + 1];
            string current = plan.Names[index];

            if (TaxiGraph.TryFindJunction(list, current, g.NextTaxiway,
                                          out double jLat, out double jLon))
            {
                g.DistanceToTurnM = GeoMath.DistanceNm(lat, lon, jLat, jLon) * GeoMath.MetersPerNm;
                double brgCurrent = TaxiGraph.SegmentBearing(list, current, jLat, jLon);
                double brgNext    = TaxiGraph.SegmentBearing(list, g.NextTaxiway, jLat, jLon);
                g.Side = SideOfTurn(brgCurrent, brgNext);
            }
            else
            {
                // Las calles no se tocan en el dataset (falta el tramo de unión): se avisa igual,
                // por distancia a la próxima calle, pero sin inventar el lado del giro.
                g.DistanceToTurnM = NearestSegmentDistanceM(list, g.NextTaxiway, lat, lon);
                g.Side = TurnSide.Straight;
            }
            return g;
        }

        /// <summary>
        /// Lado del giro a partir de los dos rumbos en el cruce. Se compara el rumbo de salida
        /// con el de llegada: si el nuevo está a la derecha (diferencia positiva) es giro a la
        /// derecha. Menos de 15° se considera recto, que es lo que se ve en calles que continúan.
        /// </summary>
        internal static TurnSide SideOfTurn(double bearingIn, double bearingOut)
        {
            if (double.IsNaN(bearingIn) || double.IsNaN(bearingOut)) return TurnSide.Straight;
            double signed = ((bearingOut - bearingIn + 540.0) % 360.0) - 180.0;   // (−180,180]
            if (signed > 15.0)  return TurnSide.Right;
            if (signed < -15.0) return TurnSide.Left;
            return TurnSide.Straight;
        }

        private static double NearestSegmentDistanceM(
            IEnumerable<TaxiGraph.Segment> segments, string name, double lat, double lon)
        {
            double best = double.MaxValue;
            foreach (var s in segments)
            {
                if (!string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase)) continue;
                double d = GeoMath.DistanceToSegmentNm(lat, lon, s.Lat1, s.Lon1, s.Lat2, s.Lon2)
                           * GeoMath.MetersPerNm;
                if (d < best) best = d;
            }
            return best == double.MaxValue ? double.NaN : best;
        }
    }
}
