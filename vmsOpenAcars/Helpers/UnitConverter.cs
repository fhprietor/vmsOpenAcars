using System;

namespace vmsOpenAcars.Helpers
{
    public static class UnitConverter
    {
        // Constantes de conversión
        public const double LbsToKg = 0.45359237;
        public const double KgToLbs = 2.20462262185;
        public const double NmToKmValue = 1.852;
        public const double KmToNmValue = 0.539956803;

        /// <summary>
        /// Convierte un valor de combustible a la unidad destino
        /// </summary>
        public static double ConvertFuel(double value, string fromUnit, string toUnit)
        {
            if (string.IsNullOrEmpty(fromUnit) || string.IsNullOrEmpty(toUnit))
                return value;

            string from = NormalizeUnit(fromUnit);
            string to = NormalizeUnit(toUnit);

            if (from == to)
                return value;

            if (from == "kg" && to == "lbs")
                return value * KgToLbs;

            if (from == "lbs" && to == "kg")
                return value * LbsToKg;

            return value;
        }

        /// <summary>
        /// Normaliza el nombre de la unidad (kg, KGS, kilogramos → kg)
        /// </summary>
        public static string NormalizeUnit(string unit)
        {
            if (string.IsNullOrEmpty(unit))
                return "kg";

            string u = unit.ToLowerInvariant();

            if (u == "kg" || u == "kgs" || u == "kilogram" || u == "kilograms")
                return "kg";

            if (u == "lb" || u == "lbs" || u == "pound" || u == "pounds")
                return "lbs";

            return u;
        }

        /// <summary>
        /// Convierte millas náuticas a kilómetros
        /// </summary>
        public static double NmToKm(double nm)
        {
            return nm * NmToKmValue;
        }

        /// <summary>
        /// Convierte kilómetros a millas náuticas
        /// </summary>
        public static double KmToNm(double km)
        {
            return km * KmToNmValue;
        }

        /// <summary>
        /// Determina la unidad probable basada en el valor (heurística)
        /// </summary>
        public static string GuessUnit(double value)
        {
            if (value > 5000)
                return "lbs";
            if (value > 2000)
                return "kg";

            return "kg";
        }

        /// <summary>
        /// Compara dos valores de combustible considerando sus unidades
        /// </summary>
        public static bool CompareFuel(double value1, string unit1, double value2, string unit2, double tolerancePercent = 0.10, double toleranceAbsolute = 50)
        {
            double kg1 = ConvertFuel(value1, unit1, "kg");
            double kg2 = ConvertFuel(value2, unit2, "kg");

            double difference = Math.Abs(kg1 - kg2);
            double differencePercent = (difference / Math.Max(kg1, kg2)) * 100;

            bool withinPercent = differencePercent <= (tolerancePercent * 100);
            bool withinAbsolute = difference <= toleranceAbsolute;

            return withinPercent || withinAbsolute;
        }

        /// <summary>
        /// Obtiene la diferencia formateada entre dos valores de combustible
        /// </summary>
        public static string GetFuelDifferenceDisplay(double value1, string unit1, double value2, string unit2, string displayUnit = "kg")
        {
            double v1 = ConvertFuel(value1, unit1, displayUnit);
            double v2 = ConvertFuel(value2, unit2, displayUnit);
            double diff = v1 - v2;
            string sign = diff >= 0 ? "+" : "";

            return $"{sign}{diff:F0} {displayUnit}";
        }
    }
}