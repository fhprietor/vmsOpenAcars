// Services/PositionValidator.cs
using System;
using System.Drawing;
using vmsOpenAcars.UI;
using static vmsOpenAcars.Helpers.L;

namespace vmsOpenAcars.Services
{
    public class PositionValidator
    {
        private const double EARTH_RADIUS_KM = 6371;
        private const double MAX_DISTANCE_KM = 5.0; // Tolerancia de 5km

        /// <summary>
        /// Calcula distancia entre dos puntos (Haversine)
        /// </summary>
        private double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
        {
            var dLat = Deg2Rad(lat2 - lat1);
            var dLon = Deg2Rad(lon2 - lon1);

            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(Deg2Rad(lat1)) * Math.Cos(Deg2Rad(lat2)) *
                    Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return EARTH_RADIUS_KM * c;
        }

        private double Deg2Rad(double deg) => deg * Math.PI / 180;

        /// <summary>
        /// Valida si la posición actual coincide con el aeropuerto asignado
        /// </summary>
        public (bool isValid, double distance, string message, Color messageColor) ValidatePosition(
            string requiredAirport,
            double? airportLat,
            double? airportLon,
            double currentLat,
            double currentLon)
        {
            // Si no tenemos coordenadas del aeropuerto, no podemos validar
            if (!airportLat.HasValue || !airportLon.HasValue)
            {
                return (true, 0, "📍 No hay coordenadas de referencia", Theme.Warning);
            }

            var distance = CalculateDistance(
                currentLat, currentLon,
                airportLat.Value, airportLon.Value
            );

            if (distance <= MAX_DISTANCE_KM)
            {
                return (true, distance,
                    $"{_("PositionValidated")} {requiredAirport} ({distance:F1}km)", Theme.MainText);
            }
            else
            {
                return (false, distance,
                    $"{_("YouShouldBe")} {requiredAirport} ({distance:F1}km)", Theme.Warning);
            }
        }

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