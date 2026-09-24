using System;
using System.Collections.Generic;
using System.Linq;
using vmsOpenAcars.Services;

namespace vmsOpenAcars.Helpers
{
    /// <summary>
    /// Orden de presentación de las posiciones ATC de IVAO.
    ///
    /// Vive fuera del control WinForms a propósito: es una regla de dominio (qué
    /// información es más urgente para el piloto), no una decisión de dibujo, y así puede
    /// probarse sin arrastrar `System.Windows.Forms` al proyecto de tests.
    /// </summary>
    internal static class AtcStationOrder
    {
        /// <summary>
        /// Rango dentro de un aeropuerto: dependencias locales primero (lo útil en rodaje
        /// y despegue), luego las de área, y al final lo desconocido.
        /// </summary>
        internal static int PositionRank(string position)
        {
            switch ((position ?? "").ToUpperInvariant())
            {
                case "DEL":  return 0;
                case "GND":  return 1;
                case "TWR":  return 2;
                case "ATIS": return 3;
                case "APP":  return 4;
                case "DEP":  return 5;
                case "CTR":  return 6;
                default:     return 7;
            }
        }

        /// <summary>
        /// Ordena por aeropuerto y, dentro de cada uno, por urgencia de la posición.
        /// Devuelve una lista nueva; no muta la entrada.
        /// </summary>
        internal static List<IvaoAtcStation> Sort(IEnumerable<IvaoAtcStation> stations)
        {
            if (stations == null) return new List<IvaoAtcStation>();

            return stations
                .OrderBy(s => s?.Icao ?? "", StringComparer.OrdinalIgnoreCase)
                .ThenBy(s => PositionRank(s?.Position))
                .ThenBy(s => s?.Position ?? "", StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }
}
