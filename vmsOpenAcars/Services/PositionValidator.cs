// Services/PositionValidator.cs
using System;
using System.Drawing;
using vmsOpenAcars.Helpers;
using vmsOpenAcars.UI;
using static vmsOpenAcars.Helpers.L;

namespace vmsOpenAcars.Services
{
    public class PositionValidator
    {
        public (bool isValid, double distance, string message, Color messageColor) ValidatePosition(
            string requiredAirport,
            double? airportLat,
            double? airportLon,
            double currentLat,
            double currentLon)
        {
            if (!airportLat.HasValue || !airportLon.HasValue)
            {
                return (true, 0, "📍 No hay coordenadas de referencia", Theme.Warning);
            }

            var distance = UnitConverter.CalculateDistanceNm(
                currentLat, currentLon,
                airportLat.Value, airportLon.Value
            );

            if (distance <= Constants.MaxValidationDistanceNM)
            {
                return (true, distance,
                    $"{_("PositionValidated")} {requiredAirport} ({distance:F1} NM)", Theme.MainText);
            }
            else
            {
                return (false, distance,
                    $"{_("YouShouldBe")} {requiredAirport} ({distance:F1} NM)", Theme.Warning);
            }
        }

        /// <summary>
        /// Valida si la posición actual coincide con el aeropuerto asignado
        /// </summary>
        
        /// <summary>
        /// Compara aeropuertos por código ICAO (fase despacho)
        /// </summary>
        public bool CompareIcaoCodes(string phpvmsAirport, string simbriefAirport)
        {
            return string.Equals(phpvmsAirport, simbriefAirport,
                StringComparison.OrdinalIgnoreCase);
        }
    }
}