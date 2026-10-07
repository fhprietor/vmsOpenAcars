using System;

namespace vmsOpenAcars.Models
{
    public class FlightRecord
    {
        public int    Id                { get; set; }
        public string FlightNumber      { get; set; }
        public string Origin            { get; set; }
        public string Destination       { get; set; }
        public string RunwayName        { get; set; }
        public DateTime FlightDate      { get; set; }
        public int    LandingRateFpm    { get; set; }
        public double GForce            { get; set; }
        public double TouchdownDistFt   { get; set; }
        public double CenterlineDevFt   { get; set; }
        public int    Score             { get; set; }
        public string MetarRaw          { get; set; }

        // ── Meteo del ATERRIZAJE (capturada en el touchdown, no al filear) ────────
        // Todo nullable a propósito: son datos que pueden no existir (sin METAR del destino, sin
        // viento del simulador, sin rumbo de pista resuelto) y **degradar sin datos** es la regla
        // del repo. Un `0` no puede significar «no lo sé»: el 0 es un viento del norte / una calma.
        //
        // `MetarRaw` de arriba **es** el METAR en crudo del aterrizaje desde este cambio; no se
        // duplica en otra columna. Lo que se añade es su **hora de observación** (el `ddHHMMZ` del
        // propio METAR, ver `Helpers/MetarObservationTime`), el viento del simulador en el momento
        // del contacto y las **componentes calculadas** contra el rumbo **verdadero** de la pista
        // (`Helpers/WindComponents`).

        /// <summary>Hora de observación del METAR, en UTC (el grupo `ddHHMMZ`). Null si no se pudo leer.</summary>
        public DateTime? LandingMetarObsUtc { get; set; }

        /// <summary>Dirección del viento del simulador en el touchdown, en grados (de donde viene).</summary>
        public double?   LandingWindDirDeg   { get; set; }

        /// <summary>Intensidad del viento del simulador en el touchdown, en nudos.</summary>
        public double?   LandingWindSpeedKt  { get; set; }

        /// <summary>Racha, en nudos. La publica el **METAR**: FSUIPC no expone offset de racha.</summary>
        public double?   LandingWindGustKt   { get; set; }

        /// <summary>Componente en cara (+) o en cola (−), en nudos. Null si no se pudo calcular.</summary>
        public double?   LandingHeadwindKt   { get; set; }

        /// <summary>Componente cruzada: positiva **desde la derecha**, negativa **desde la izquierda**.</summary>
        public double?   LandingCrosswindKt  { get; set; }

        /// <summary>Rumbo **verdadero** de la pista de aterrizaje con el que se descompuso el viento.</summary>
        public double?   LandingRunwayTrueDeg { get; set; }

        /// <summary>
        /// **Longitud de la pista de la toma, en pies** (`length_ft` de NavData). Null = sin dato:
        /// los vuelos guardados antes de que existiera la columna y aquellos en los que NavData no
        /// resolvió la pista.
        ///
        /// La necesita el **closeup del aterrizaje** (`Helpers/TouchdownCloseupGeometry`) para pintar
        /// la pista con su largo de verdad; sin ella no se dibuja la línea de pista —nunca una de
        /// largo inventado— y el umbral, el punto de toque y las bandas de la zona siguen
        /// funcionando.
        ///
        /// No duplica el dato que ya usa la puntuación: `TouchdownZonePolicy` lo recibe por
        /// `TouchdownState.RunwayLengthFt` **en vuelo**, y esto es su persistencia, para poder volver
        /// a mirar el aterrizaje semanas después.
        /// </summary>
        public double?   RunwayLengthFt { get; set; }

        /// <summary>
        /// Las componentes tal como se guardaron, para poder pintarlas con
        /// <see cref="Helpers.LandingWeatherLine"/> sin recalcular nada: lo que se enseña es lo que
        /// quedó en la base. `Available` solo es true si las dos componentes se persistieron.
        /// </summary>
        internal Helpers.WindComponentResult WindAtLanding
        {
            get
            {
                var wind = new Helpers.WindComponentResult
                {
                    WindDirDeg           = LandingWindDirDeg,
                    WindSpeedKt          = LandingWindSpeedKt,
                    GustKt               = LandingWindGustKt,
                    RunwayHeadingTrueDeg = LandingRunwayTrueDeg,
                };
                if (LandingHeadwindKt.HasValue && LandingCrosswindKt.HasValue)
                {
                    wind.HeadwindKt  = LandingHeadwindKt.Value;
                    wind.CrosswindKt = LandingCrosswindKt.Value;
                    wind.Available   = true;
                    wind.Calm        = LandingWindSpeedKt.HasValue && LandingWindSpeedKt.Value <= 0;
                }
                return wind;
            }
        }

        public string DisplayDate        => FlightDate.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
        /// <summary>
        /// "—" when no touchdown was captured (stored as <see cref="Services.ScoringService.NoLandingData"/>),
        /// so an unknown landing rate is never shown as a real 0 fpm landing.
        /// </summary>
        public string DisplayLandingRate => LandingRateFpm == Services.ScoringService.NoLandingData
                                                ? "—"
                                                : $"{LandingRateFpm} fpm";
        public string DisplayRoute       => $"{Origin} → {Destination}";
        public string DisplayScore       => $"{Score}/100";
    }
}
