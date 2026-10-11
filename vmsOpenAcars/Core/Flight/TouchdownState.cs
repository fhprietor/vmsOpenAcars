using System;

namespace vmsOpenAcars.Core.Flight
{
    internal sealed class TouchdownState
    {
        /// <summary>
        /// Runway geometry for the touchdown, swapped in as a single immutable reference.
        /// The three values are only meaningful together and are published from a
        /// Task.Run (TelemetryCoordinator.LookupRunwayData) while the polling thread and
        /// FilePirep read them. Assigning them as three separate fields allowed a reader
        /// to observe a half-updated set (new runway name against the previous distance);
        /// publishing one reference makes the update atomic — a reader sees either the
        /// whole previous set or the whole new one.
        /// </summary>
        internal sealed class RunwayGeometry
        {
            public readonly double DistanceFt;
            public readonly double CenterlineDeviationFt;
            public readonly string RunwayName;

            /// <summary>
            /// Longitud de la pista en pies (`length_ft` de NavData). **Cero = sin dato**. Viaja en el
            /// mismo bloque inmutable que la distancia y el nombre porque el criterio «Touchdown
            /// Zone» los necesita **juntos**: decidir el tramo con la distancia de una pista y la
            /// longitud de otra sería peor que no decidir. Ver `Helpers/TouchdownZonePolicy`.
            /// </summary>
            public readonly double RunwayLengthFt;

            /// <summary>
            /// Rumbo **VERDADERO** del eje de la pista de la toma, o **null** si NavData no pudo
            /// resolverla. Viaja en el mismo bloque inmutable por el mismo motivo que la longitud: el
            /// cálculo de las **componentes del viento del aterrizaje** (`Helpers/WindComponents`)
            /// necesita el eje y la dirección del viento del **mismo instante**; descomponer el viento
            /// contra el eje de otra pista sería peor que no descomponerlo.
            /// </summary>
            public readonly double? TrueHeadingDeg;

            public RunwayGeometry(double distanceFt, double centerlineDeviationFt, string runwayName,
                                  double runwayLengthFt, double? trueHeadingDeg)
            {
                DistanceFt            = distanceFt;
                CenterlineDeviationFt = centerlineDeviationFt;
                RunwayName            = runwayName;
                RunwayLengthFt        = runwayLengthFt;
                TrueHeadingDeg        = trueHeadingDeg;
            }
        }

        public bool     Captured;
        public DateTime Timestamp = DateTime.MinValue;
        public int?     Fpm;
        public double   Pitch, Bank, GForce;
        public double   Lat, Lon, HeadingDeg;

        /// <summary>
        /// **La lectura del toque que se publica y la que se puntúa**, congelada en un bloque
        /// inmutable.
        ///
        /// El toque vivo —`Fpm`, `GForce`, `Pitch`, `Bank`— **sí puede cambiar después de
        /// capturarse**: `ResetRunwayData()` (el rearme del touch-and-go, y también un rearme
        /// espurio del detector) pone `Captured = false` y el siguiente ciclo de 20 Hz vuelve a
        /// entrar por `RegisterTouchdown`, que sobrescribe el valor. Y entre la copia del registro
        /// (`AcarsReporter.SnapshotLandingRecord`, **antes** de `FilePirep`) y los dos sitios donde
        /// se puntúa y se arma el payload hay `await`s en los que la telemetría sigue corriendo, así
        /// que **el logbook y el PIREP podían llevar números distintos del mismo toque**.
        ///
        /// Medido en el log real: el vuelo 20 quedó con **+156** en el log local y **−155** en el
        /// PIREP, y el 23 con **+34** y **−26**. En el 40, que no se rearmó, los dos dicen **+74**.
        /// </summary>
        internal sealed class FrozenTouchdown
        {
            public readonly bool   Captured;
            public readonly int?   Fpm;
            public readonly double Pitch, Bank, GForce;

            public FrozenTouchdown(bool captured, int? fpm, double pitch, double bank, double gforce)
            {
                Captured = captured;
                Fpm      = fpm;
                Pitch    = pitch;
                Bank     = bank;
                GForce   = gforce;
            }
        }

        private volatile FrozenTouchdown _frozen;

        /// <summary>La lectura congelada del toque, o `null` si todavía no se ha congelado.</summary>
        public FrozenTouchdown Frozen => _frozen;

        /// <summary>
        /// Congela el toque **una sola vez** y devuelve esa lectura. Se llama al copiar el registro
        /// del vuelo —después del último aterrizaje y antes de filear—, que es el punto donde queda
        /// fijado lo que se publica y lo que se puntúa. Congelar aquí y no en `Capture` es
        /// deliberado: en un touch-and-go lo que se informa es el **último** aterrizaje, y la copia
        /// del registro corre después de él.
        /// </summary>
        public FrozenTouchdown FreezeIfNeeded()
        {
            var frozen = _frozen;
            if (frozen != null) return frozen;

            frozen = new FrozenTouchdown(Captured, Fpm, Pitch, Bank, GForce);
            _frozen = frozen;
            return frozen;
        }

        private volatile RunwayGeometry _geometry;

        public double DistanceFt            => _geometry?.DistanceFt ?? 0;
        public double CenterlineDeviationFt => _geometry?.CenterlineDeviationFt ?? 0;
        public string RunwayName            => _geometry?.RunwayName;

        /// <summary>Longitud de la pista de la toma, en pies; **0 = sin dato**.</summary>
        public double RunwayLengthFt        => _geometry?.RunwayLengthFt ?? 0;

        /// <summary>Rumbo **verdadero** del eje de la pista de la toma; **null = sin dato**.</summary>
        public double? RunwayTrueHeadingDeg => _geometry?.TrueHeadingDeg;

        public void Capture(int fpm, double pitch, double bank, double gforce,
                            double lat, double lon, double heading)
        {
            Captured   = true;
            Timestamp  = DateTime.UtcNow;
            Fpm        = fpm;
            Pitch      = pitch;
            Bank       = bank;
            GForce     = gforce;
            Lat        = lat;
            Lon        = lon;
            HeadingDeg = heading;
        }

        public void SetRunwayData(double distFt, double deviationFt, string runwayName,
                                  double runwayLengthFt, double? trueHeadingDeg)
            => _geometry = new RunwayGeometry(distFt, deviationFt, runwayName, runwayLengthFt,
                                              trueHeadingDeg);

        // Partial reset on touch-and-go: keep Fpm/GForce for scoring, clear runway data
        public void ResetRunwayData()
        {
            Captured  = false;
            _geometry = null;
            // El rearme del touch-and-go es para el aterrizaje SIGUIENTE, así que suelta también la
            // lectura congelada: la copia del registro volverá a congelar el toque que de verdad se
            // publica (el último). Sin esto, un touch-and-go informaría el toque del ciclo anterior.
            _frozen   = null;
        }

        public void Reset()
        {
            Captured              = false;
            Timestamp             = DateTime.MinValue;
            Fpm                   = null;
            Pitch = Bank = GForce = 0;
            Lat = Lon = HeadingDeg = 0;
            _geometry             = null;
            _frozen               = null;
        }
    }
}
