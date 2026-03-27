// En Helpers/Constants.cs

using System.Configuration;

namespace vmsOpenAcars.Helpers
{
    public static class Constants
    {
        /// <summary>
        /// Radio de la Tierra en millas náuticas (NM)
        /// </summary>
        public const double EarthRadiusNM = 3440.065;

        /// <summary>
        /// Radio de la Tierra en kilómetros (KM)
        /// </summary>
        public const double EarthRadiusKM = 6371.0;

        /// <summary>
        /// Distancia máxima para validar posición (5 km ≈ 2.7 NM)
        /// </summary>
        public const double MaxValidationDistanceNM = 2.7;

        public static double FuelTolerancePercent =>
            double.TryParse(ConfigurationManager.AppSettings["fuel_tolerance_percent"], out double p) ? p / 100 : 0.15;

        public static double FuelToleranceAbsolute =>
            double.TryParse(ConfigurationManager.AppSettings["fuel_tolerance_absolute"], out double a) ? a : 100;
    }
}