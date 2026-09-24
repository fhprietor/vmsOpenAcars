using System;

namespace vmsOpenAcars.Helpers
{
    /// <summary>
    /// Geometría esférica/flat-earth compartida.
    ///
    /// Estas fórmulas estaban duplicadas en al menos tres sitios
    /// (`MapRouteController.Helpers.DispGeoNm`, `AirspaceMonitorService.ProjectPosition` y
    /// la proyección local de `ApproachChartForm`), cada uno con su propia tolerancia al
    /// caso degenerado. Una sola implementación evita que una corrección aplicada a la
    /// proyección de un módulo deje a los demás con el comportamiento antiguo.
    ///
    /// Ámbito de validez: la aproximación flat-earth es precisa a escalas de decenas de NM,
    /// que es todo lo que proyecta este cliente (ruta, espaciado de procedimientos, radios
    /// de espacio aéreo). No sirve para grandes círculos de miles de NM.
    /// </summary>
    internal static class GeoMath
    {
        /// <summary>Metros por grado de latitud (media esférica).</summary>
        internal const double MetersPerDegLat = 111320.0;

        /// <summary>Metros por milla náutica.</summary>
        internal const double MetersPerNm = 1852.0;

        /// <summary>
        /// Coseno de la latitud, acotado para que la división por él no explote cerca de
        /// los polos. Sin esta guarda, proyectar en una latitud extrema producía
        /// incrementos de longitud desmesurados.
        /// </summary>
        internal static double CosLat(double latDeg)
        {
            double c = Math.Cos(latDeg * Math.PI / 180.0);
            return Math.Abs(c) < 1e-6 ? 1e-6 : c;
        }

        /// <summary>
        /// Desplaza un punto <paramref name="distNm"/> millas náuticas en la dirección
        /// <paramref name="bearingDeg"/> (rumbo geográfico verdadero).
        /// </summary>
        internal static void Project(
            double lat, double lon, double bearingDeg, double distNm,
            out double outLat, out double outLon)
        {
            double rad    = bearingDeg * Math.PI / 180.0;
            double meters = distNm * MetersPerNm;
            double cosRef = CosLat(lat);

            outLat = lat + meters * Math.Cos(rad) / MetersPerDegLat;
            outLon = lon + meters * Math.Sin(rad) / (MetersPerDegLat * cosRef);
        }

        /// <summary>Variante de <see cref="Project"/> que devuelve una tupla.</summary>
        internal static (double Lat, double Lon) ProjectPoint(
            double lat, double lon, double bearingDeg, double distNm)
        {
            Project(lat, lon, bearingDeg, distNm, out double outLat, out double outLon);
            return (outLat, outLon);
        }

        /// <summary>
        /// Distancia aproximada entre dos puntos en millas náuticas (flat-earth,
        /// referenciada a la latitud del primer punto).
        /// </summary>
        internal static double DistanceNm(double lat1, double lon1, double lat2, double lon2)
        {
            double cosRef = CosLat(lat1);
            double dN = (lat2 - lat1) * MetersPerDegLat / MetersPerNm;
            double dE = (lon2 - lon1) * MetersPerDegLat * cosRef / MetersPerNm;
            return Math.Sqrt(dN * dN + dE * dE);
        }

        /// <summary>Distancia aproximada entre dos puntos en kilómetros (flat-earth).</summary>
        internal static double DistanceKm(double lat1, double lon1, double lat2, double lon2)
        {
            double cosRef = CosLat(lat1);
            double dN = (lat2 - lat1) * MetersPerDegLat / 1000.0;
            double dE = (lon2 - lon1) * MetersPerDegLat * cosRef / 1000.0;
            return Math.Sqrt(dN * dN + dE * dE);
        }

        /// <summary>Separa un par de componentes (lat, lon) en grados a metros (flat-earth).</summary>
        internal static void ToMeters(
            double lat1, double lon1, double lat2, double lon2,
            out double northMeters, out double eastMeters)
        {
            double cosRef = CosLat(lat1);
            northMeters = (lat2 - lat1) * MetersPerDegLat;
            eastMeters  = (lon2 - lon1) * MetersPerDegLat * cosRef;
        }

        /// <summary>Rumbo geográfico verdadero entre dos puntos, en grados [0,360).</summary>
        internal static double BearingDeg(double lat1, double lon1, double lat2, double lon2)
        {
            ToMeters(lat1, lon1, lat2, lon2, out double dN, out double dE);
            if (Math.Abs(dN) < 1e-9 && Math.Abs(dE) < 1e-9) return 0.0;
            double b = Math.Atan2(dE, dN) * 180.0 / Math.PI;
            return (b + 360.0) % 360.0;
        }

        /// <summary>
        /// Diferencia angular mínima entre dos rumbos, en grados [0,180].
        /// </summary>
        internal static double BearingDiffDeg(double a, double b)
        {
            double d = Math.Abs(a - b) % 360.0;
            return d > 180.0 ? 360.0 - d : d;
        }

        /// <summary>
        /// Distancia perpendicular, en NM, de un punto al <b>segmento</b> que va de
        /// (<paramref name="lat1"/>,<paramref name="lon1"/>) a (<paramref name="lat2"/>,<paramref name="lon2"/>).
        /// A diferencia de <see cref="Project"/>, que mide contra la recta infinita, aquí el
        /// parámetro se recorta a [0,1]: más allá de los extremos la distancia crece, que es lo
        /// que hace falta para medir la separación de un avión respecto a la traza de una ruta
        /// (un punto pasado el destino no está "sobre la ruta").
        /// </summary>
        internal static double DistanceToSegmentNm(
            double lat, double lon, double lat1, double lon1, double lat2, double lon2)
        {
            ToMeters(lat1, lon1, lat2, lon2, out double dy, out double dx);
            ToMeters(lat1, lon1, lat, lon, out double py, out double px);

            double lenSq = dx * dx + dy * dy;
            double t = 0.0;
            if (lenSq > 1e-9) t = (px * dx + py * dy) / lenSq;
            if (t < 0.0) t = 0.0;
            else if (t > 1.0) t = 1.0;

            double ex = px - t * dx;
            double ey = py - t * dy;
            return Math.Sqrt(ex * ex + ey * ey) / MetersPerNm;
        }
    }
}
