using System;
using System.Collections.Generic;
using vmsOpenAcars.Models;
using vmsOpenAcars.Models.NavData;

namespace vmsOpenAcars.Helpers
{
    /// <summary>
    /// **La comprobación de ruta del cliente, por intersección de segmentos**: ¿algún tramo del
    /// `navlog` del OFP cruza un polígono de espacio aéreo restringido o prohibido?
    ///
    /// Por qué existe y por qué así (phpVMS, mensaje del 16/11/2026): la comprobación anterior
    /// muestreaba la ruta —un punto cada ~130 nm en un SKBO–KATL—, y **SKR11 / BARRANQUILLA mide
    /// ~40 nm**: la zona entera cabía entre dos muestras y el aviso no saltaba hasta que el avión ya
    /// estaba dentro (el 863 lo detectó en vuelo a las 17:30:55). Una zona de 40 nm no se ve
    /// muestreando de 130 en 130: se ve **cortando el segmento con el polígono**, que es lo que hace
    /// esto y lo que hace el servidor con el `navlog` que descarga.
    ///
    /// Detalles que no son evidentes:
    /// * **Los dos extremos pueden estar fuera de la zona** y el tramo pasar por dentro. Es el caso
    ///   normal en ruta, y el único que un muestreo por puntos no puede detectar.
    /// * **La altitud del tramo manda**: si el tramo va por encima del techo de la zona, no hay aviso
    ///   —sobrevolar no es invadir—. Medido con datos reales: el SID `LLIX2A` de SKRG cruza
    ///   `SVR2508 - BARRA DE MARACAIBO` (GND–3000 ft MSL) a 10.500–11.500 ft; avisar de eso sería un
    ///   falso positivo. **Sin altitud no se descarta** (degradar sin datos) y se avisa, porque es
    ///   preferible un aviso de más que un vuelo dentro de una zona sin enterarse.
    /// * **`Danger` no avisa** (`AirspaceViolationPolicy`): es advertencia, no prohibición. En la
    ///   auditoría de los 10 PIREP marcados, 2 eran `Danger`.
    /// * La geometría se resuelve en un plano local por tramo (`GeoMath.ToMeters`), que es la escala
    ///   a la que se trabaja aquí: decenas o centenares de millas, no un gran círculo de miles.
    ///
    /// Helper **puro**: sin red, sin WinForms y sin estado. Los polígonos los pone
    /// `AirspaceMonitorService` (los que ya se pidieron para la ruta) y los tramos, el OFP.
    /// </summary>
    internal static class RouteAirspaceCheck
    {
        /// <summary>
        /// Cruces de la ruta con zonas acotadas, **en orden de ruta** y sin repetir zona (una zona
        /// se avisa una vez, aunque la ruta la cruce dos veces). Lista vacía = ruta limpia.
        /// </summary>
        /// <param name="waypoints">El `navlog` del OFP, en orden.</param>
        /// <param name="airspaces">Los espacios aéreos ya cargados para la ruta.</param>
        internal static List<RouteWarning> Find(IList<SimbriefWaypoint> waypoints,
                                                IEnumerable<NavAirspace> airspaces)
        {
            var hits = new List<RouteWarning>();
            if (waypoints == null || waypoints.Count < 2 || airspaces == null) return hits;

            // Las zonas que no son ni prohibidas ni restringidas no participan: ni se evalúan. De cada
            // una se guarda su caja envolvente, que es el descarte barato antes de proyectar el anillo
            // entero: con la lista real de la ruta (30 zonas acotadas contra 27 fix) el bucle sin
            // este filtro proyecta decenas de miles de lados para nada.
            var zones = new List<Zone>();
            foreach (var a in airspaces)
            {
                if (a?.Geometry?.Coordinates == null || a.Geometry.Coordinates.Count == 0
                    || !AirspaceViolationPolicy.IsViolation(a.Type))
                    continue;

                var box = BoxOf(a);
                if (box != null) zones.Add(new Zone { Airspace = a, Box = box });
            }
            if (zones.Count == 0) return hits;

            var reported = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < waypoints.Count - 1; i++)
            {
                SimbriefWaypoint from = waypoints[i];
                SimbriefWaypoint to   = waypoints[i + 1];
                if (from == null || to == null) continue;

                foreach (var zone in zones)
                {
                    string key = Key(zone.Airspace);
                    if (reported.Contains(key)) continue;        // ya avisada en un tramo anterior
                    if (!MayTouch(from, to, zone.Box)) continue; // descarte por caja
                    if (!CrossesLeg(from, to, zone.Airspace)) continue;

                    reported.Add(key);
                    hits.Add(new RouteWarning
                    {
                        Area       = zone.Airspace.Name,
                        Limits     = Limits(zone.Airspace),
                        Leg        = Leg(from, to),
                        Type       = zone.Airspace.Type,
                        Action     = "revisar_ruta",
                        FromServer = false,
                    });
                }
            }

            return hits;
        }

        /// <summary>Una zona y su caja envolvente, calculada una sola vez por comprobación.</summary>
        private sealed class Zone
        {
            internal NavAirspace Airspace;
            internal GeoBox      Box;
        }

        /// <summary>Caja en grados. En las rutas de este cliente no hay cambio de antimeridiano.</summary>
        private sealed class GeoBox
        {
            internal double MinLat, MaxLat, MinLon, MaxLon;
        }

        private static GeoBox BoxOf(NavAirspace zone)
        {
            GeoBox box = null;
            foreach (var ring in zone.Geometry.Coordinates)
            {
                if (ring == null) continue;
                foreach (var point in ring)
                {
                    if (point == null || point.Length < 2) continue;
                    double lon = point[0], lat = point[1];
                    if (box == null)
                        box = new GeoBox { MinLat = lat, MaxLat = lat, MinLon = lon, MaxLon = lon };
                    else
                    {
                        if (lat < box.MinLat) box.MinLat = lat;
                        if (lat > box.MaxLat) box.MaxLat = lat;
                        if (lon < box.MinLon) box.MinLon = lon;
                        if (lon > box.MaxLon) box.MaxLon = lon;
                    }
                }
            }
            return box;
        }

        /// <summary>
        /// ¿La caja del tramo puede tocar la de la zona? Solo descarta; el cruce lo decide
        /// <see cref="SegmentCrossesPolygon"/>. El margen de 0,01° (~0,6 nm) cubre la diferencia entre
        /// la recta lat/lon de la caja y el segmento proyectado.
        /// </summary>
        private static bool MayTouch(SimbriefWaypoint from, SimbriefWaypoint to, GeoBox box)
        {
            if (box == null) return false;

            const double margin = 0.01;
            double minLat = Math.Min(from.Lat, to.Lat) - margin;
            double maxLat = Math.Max(from.Lat, to.Lat) + margin;
            double minLon = Math.Min(from.Lon, to.Lon) - margin;
            double maxLon = Math.Max(from.Lon, to.Lon) + margin;

            return !(maxLat < box.MinLat || minLat > box.MaxLat
                  || maxLon < box.MinLon || minLon > box.MaxLon);
        }

        /// <summary>
        /// ¿Este tramo cruza esta zona **y** la altitud lo permite? Es la decisión completa de un
        /// tramo contra una zona, separada para poder probarla sola.
        /// </summary>
        internal static bool CrossesLeg(SimbriefWaypoint from, SimbriefWaypoint to, NavAirspace zone)
        {
            if (from == null || to == null || zone?.Geometry?.Coordinates == null) return false;
            if (!AirspaceViolationPolicy.IsViolation(zone.Type)) return false;

            if (!IsWithinVerticalBand(from.AltFt, to.AltFt, zone)) return false;

            foreach (var ring in zone.Geometry.Coordinates)
            {
                if (ring == null || ring.Count < 4) continue;
                if (SegmentCrossesPolygon(from.Lat, from.Lon, to.Lat, to.Lon, ring)) return true;
            }
            return false;
        }

        /// <summary>
        /// ¿El punto está dentro del polígono? Es la misma proyección y el mismo ray-casting que usa
        /// <see cref="SegmentCrossesPolygon"/>, expuesto aparte porque un tramo que **empieza** dentro
        /// de una zona cuenta como cruce y hay que poder decirlo sin ambigüedad (y probarlo).
        /// </summary>
        internal static bool ContainsPoint(double lat, double lon, IList<double[]> ring)
        {
            if (ring == null || ring.Count < 4) return false;

            int count = ring.Count;
            if (Same(ring[0], ring[count - 1])) count--;

            return PointInRing(0.0, 0.0, ring, count, lat, lon);
        }

        /// <summary>
        /// Intersección **segmento contra polígono** en un plano local: hay cruce si algún lado del
        /// polígono corta el segmento, o si el punto de salida ya está dentro (un tramo que empieza
        /// dentro de la zona cruza su frontera hacia dentro antes de empezar, y eso también cuenta).
        ///
        /// El plano se ancla en el punto de salida del tramo: la distorsión de la proyección
        /// flat-earth crece con la distancia al ancla, y aquí el ancla está en uno de los extremos
        /// del segmento, así que el error dentro del tramo es despreciable.
        /// </summary>
        internal static bool SegmentCrossesPolygon(double lat1, double lon1,
                                                   double lat2, double lon2,
                                                   IList<double[]> ring)
        {
            if (ring == null || ring.Count < 4) return false;

            // El ancla del plano es el punto de salida del tramo: el segmento va de (0,0) a (bx,by).
            const double ax = 0.0, ay = 0.0;
            GeoMath.ToMeters(lat1, lon1, lat2, lon2, out double bx, out double by);

            // Tolerancia de cierre: el anillo GeoJSON a veces termina con una coordenada de más.
            int count = ring.Count;
            if (Same(ring[0], ring[count - 1])) count--;

            // ¿El extremo de salida está dentro? (ray-casting en el mismo plano)
            if (PointInRing(0.0, 0.0, ring, count, lat1, lon1)) return true;

            for (int i = 0; i < count; i++)
            {
                int j = (i + 1) % count;
                GeoMath.ToMeters(lat1, lon1, ring[i][1], ring[i][0], out double px1, out double py1);
                GeoMath.ToMeters(lat1, lon1, ring[j][1], ring[j][0], out double px2, out double py2);

                if (SegmentsIntersect(ax, ay, bx, by, px1, py1, px2, py2)) return true;
            }

            return false;
        }

        /// <summary>
        /// Intersección propia de dos segmentos por orientación (productos cruzados). Los contactos
        /// en un extremo no cuentan: rozar el vértice de una zona no es meterse en ella, y contarlo
        /// convertiría cada tramo que pasa por el borde en un aviso.
        /// </summary>
        internal static bool SegmentsIntersect(double ax, double ay, double bx, double by,
                                               double cx, double cy, double dx, double dy)
        {
            double d1 = Cross(cx, cy, dx, dy, ax, ay);
            double d2 = Cross(cx, cy, dx, dy, bx, by);
            double d3 = Cross(ax, ay, bx, by, cx, cy);
            double d4 = Cross(ax, ay, bx, by, dx, dy);

            return ((d1 > 0 && d2 < 0) || (d1 < 0 && d2 > 0))
                && ((d3 > 0 && d4 < 0) || (d3 < 0 && d4 > 0));
        }

        private static double Cross(double ox, double oy, double ax, double ay, double bx, double by)
            => (ax - ox) * (by - oy) - (ay - oy) * (bx - ox);

        /// <summary>Ray-casting, con los puntos del anillo igual de proyectados que el segmento.</summary>
        private static bool PointInRing(double x, double y, IList<double[]> ring, int count,
                                        double anchorLat, double anchorLon)
        {
            bool inside = false;
            for (int i = 0, j = count - 1; i < count; j = i++)
            {
                GeoMath.ToMeters(anchorLat, anchorLon, ring[i][1], ring[i][0], out double xi, out double yi);
                GeoMath.ToMeters(anchorLat, anchorLon, ring[j][1], ring[j][0], out double xj, out double yj);

                if (((yi > y) != (yj > y)) && (x < (xj - xi) * (y - yi) / (yj - yi) + xi))
                    inside = !inside;
            }
            return inside;
        }

        /// <summary>
        /// La compuerta vertical: el tramo solo cuenta si su banda de altitudes se solapa con la de la
        /// zona. **Sin altitud no se descarta**: `AltFt == 0` es «no sé» en el `navlog`, y ahí se deja
        /// pasar el aviso (degradar sin datos, y en la duda avisar).
        /// </summary>
        internal static bool IsWithinVerticalBand(int altFromFt, int altToFt, NavAirspace zone)
        {
            if (zone == null) return false;

            bool unknown = altFromFt <= 0 || altToFt <= 0;
            if (unknown) return true;

            double bandLow  = Math.Min(altFromFt, altToFt);
            double bandHigh = Math.Max(altFromFt, altToFt);

            double? lower = LimitFt(zone.LowerLimit);
            double? upper = LimitFt(zone.UpperLimit);

            if (lower.HasValue && bandHigh < lower.Value) return false;   // el tramo va por debajo
            if (upper.HasValue && bandLow  > upper.Value) return false;   // el tramo va por encima
            return true;
        }

        /// <summary>El valor en pies de un límite, o `null` si no se puede saber (`UNL`, vacío).</summary>
        internal static double? LimitFt(NavAirspaceLimit limit)
        {
            if (limit == null) return null;
            if (limit.ValueFt.HasValue) return limit.ValueFt.Value;

            string display = (limit.Display ?? "").Trim().ToUpperInvariant();
            if (display.Length == 0) return null;
            if (display == "GND" || display == "SFC") return 0.0;
            if (display == "UNL") return null;
            if (display.StartsWith("FL") && double.TryParse(display.Substring(2), out double fl))
                return fl * 100.0;

            // «3000ft MSL», «4500 FT»: el primer número del texto es la altitud.
            var digits = new System.Text.StringBuilder();
            foreach (char c in display)
            {
                if (char.IsDigit(c)) digits.Append(c);
                else if (digits.Length > 0) break;
            }
            return digits.Length > 0 && double.TryParse(digits.ToString(), out double ft) ? ft : (double?)null;
        }

        /// <summary>`GND–FL999`, como lo publica el servidor: el guion es el de su ejemplo.</summary>
        internal static string Limits(NavAirspace zone)
        {
            if (zone == null) return null;
            string lower = string.IsNullOrWhiteSpace(zone.LowerLimit?.Display) ? "SFC" : zone.LowerLimit.Display.Trim();
            string upper = string.IsNullOrWhiteSpace(zone.UpperLimit?.Display) ? "UNL" : zone.UpperLimit.Display.Trim();
            return lower + "–" + upper;
        }

        private static string Leg(SimbriefWaypoint from, SimbriefWaypoint to)
            => $"{Ident(from)}–{Ident(to)}";

        private static string Ident(SimbriefWaypoint w)
            => string.IsNullOrWhiteSpace(w?.Ident) ? "?" : w.Ident.Trim();

        /// <summary>
        /// Identidad de la zona para no avisar dos veces del mismo sitio. El `Id` es lo que publica
        /// NavData; si no viniera, el nombre hace de clave.
        /// </summary>
        private static string Key(NavAirspace zone)
            => string.IsNullOrEmpty(zone?.Id) ? (zone?.Name ?? "") : zone.Id;

        private static bool Same(double[] a, double[] b)
            => a != null && b != null && a.Length >= 2 && b.Length >= 2
            && Math.Abs(a[0] - b[0]) < 1e-9 && Math.Abs(a[1] - b[1]) < 1e-9;
    }
}

