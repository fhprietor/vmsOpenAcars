using System;

namespace vmsOpenAcars.Models
{
    /// <summary>
    /// **La meteo del aterrizaje, capturada en el momento del contacto.**
    ///
    /// Existe como tipo propio porque hay que quedarse con ella **en el touchdown** y no al final del
    /// vuelo: entre la toma y el `SEND PIREP` pasan los minutos del rodaje y del aparcamiento, y el
    /// METAR del destino se refresca cada 5 minutos (`MetarService`). El dato que interesa para
    /// analizar «en qué condiciones se aterrizó» es el vigente en la toma, no el de la puerta.
    ///
    /// Se captura en `AcarsReporter.HandleLandingDetected` —el mismo instante en que
    /// `FlightManager.RegisterTouchdown` guarda el VS y el G— y **sobrevive al reset** porque el
    /// snapshot que lo consume (`AcarsReporter.SnapshotLandingRecord`) corre **antes** de
    /// `await FilePirep()`, que es quien llama a `ResetFlightState()`.
    ///
    /// Todo es nullable: sin METAR del destino o sin viento del simulador se guarda lo que haya.
    /// </summary>
    internal sealed class LandingWeather
    {
        /// <summary>METAR **en crudo** del aeropuerto de llegada (incluye su hora de observación).</summary>
        internal string   MetarRaw       { get; set; }

        /// <summary>Hora de observación del METAR, en UTC (`ddHHMMZ`). Null si no se pudo leer.</summary>
        internal DateTime? ObservedAtUtc { get; set; }

        /// <summary>Dirección del viento del simulador (FSUIPC 0x0E92), de donde viene.</summary>
        internal double?  WindDirDeg     { get; set; }

        /// <summary>Intensidad del viento del simulador (FSUIPC 0x0E90), en nudos.</summary>
        internal double?  WindSpeedKt    { get; set; }

        /// <summary>
        /// Racha, en nudos. Sale del **METAR del destino**, no del simulador: FSUIPC no publica un
        /// offset de racha (los de viento son 0x0E90 velocidad y 0x0E92 dirección, y no hay más), así
        /// que inventar uno sería leer basura. Null si el METAR no la trae.
        /// </summary>
        internal double?  WindGustKt     { get; set; }
    }
}
