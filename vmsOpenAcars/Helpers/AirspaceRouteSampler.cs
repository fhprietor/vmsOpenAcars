using System;
using System.Collections.Generic;

namespace vmsOpenAcars.Helpers
{
    /// <summary>
    /// Puntos de muestreo a lo largo de la ruta para pedir espacios aéreos.
    ///
    /// `/airspaces/` devuelve lo que hay alrededor de **un punto**, con un radio que declara el
    /// servidor: eran 200 nm hasta el aviso de NavData del 29/09/2026, y desde entonces son
    /// **54 nm**. Pedir solo origen, destino y —a veces— el punto medio dejaba la ruta a trozos:
    /// en un SKBO→KBOS (~2.200 nm) se cubrían 600 nm de 2.200 y el resto era "cielo vacío" sin
    /// que nada lo dijera. Repartiendo puntos con solape, la cobertura deja de depender de dónde
    /// caiga el punto medio.
    ///
    /// Los puntos se interpolan sobre la **esfera** (slerp), no en línea recta sobre lat/lon: en
    /// rutas largas la línea recta en el mapa se separa cientos de millas del arco real, y
    /// muestrearíamos sitios por los que el avión no pasa.
    /// </summary>
    internal static class AirspaceRouteSampler
    {
        /// <summary>Solape entre coberturas consecutivas: se avanza 75 % del radio, no el 100 %.</summary>
        internal const double StepFactor = 0.75;

        /// <summary>Tope de peticiones por vuelo. Con 200 nm de radio no deja huecos; con el radio
        /// **reducido por `capped`** —medido en Londres: 91 nm, porque la respuesta se recorta a 500
        /// espacios— cubre sin huecos una ruta de 1.900 nm (23 saltos de 84 nm). El tope sólo se
        /// aplica cuando hace falta: con 200 nm la misma ruta son 13 peticiones, no 24.</summary>
        internal const int MaxPoints = 24;

        /// <summary>Radio asumido mientras el servidor no declare ninguno. Se toma el **menor**
        /// conocido (54 nm, el que declaró tras el aviso del 29/09/2026) a propósito: asumir de más
        /// deja huecos en la ruta sin que nada lo diga, asumir de menos sólo cuesta peticiones —y
        /// desde que el servidor indexa los países por su cuenta, cada petición son ~0,3 s sin
        /// límite de tasa—. El valor real de cada respuesta sobrescribe éste.</summary>
        internal const double DefaultRadiusNm = 54.0;

        internal static List<(double Lat, double Lon)> Sample(
            double oLat, double oLon, double dLat, double dLon, double radiusNm)
            => Sample(oLat, oLon, dLat, dLon, radiusNm, MaxPoints);

        internal static List<(double Lat, double Lon)> Sample(
            double oLat, double oLon, double dLat, double dLon, double radiusNm, int maxPoints)
        {
            if (maxPoints < 2) maxPoints = 2;
            if (radiusNm <= 0) radiusNm = DefaultRadiusNm;

            var points = new List<(double Lat, double Lon)>();
            double totalNm = GeoMath.DistanceNm(oLat, oLon, dLat, dLon);

            // Origen y destino más cerca que el radio: una sola petición en el punto medio cubre
            // los dos. Sin esto pediríamos dos veces lo mismo.
            if (totalNm <= radiusNm)
            {
                points.Add(Interpolate(oLat, oLon, dLat, dLon, 0.5));
                return points;
            }

            int segments = (int)Math.Ceiling(totalNm / (radiusNm * StepFactor));
            if (segments > maxPoints - 1) segments = maxPoints - 1;
            if (segments < 2) segments = 2;

            for (int i = 0; i <= segments; i++)
                points.Add(Interpolate(oLat, oLon, dLat, dLon, (double)i / segments));

            return points;
        }

        /// <summary>Punto a fracción <paramref name="f"/> del arco origen→destino (slerp en la esfera).</summary>
        internal static (double Lat, double Lon) Interpolate(
            double lat1, double lon1, double lat2, double lon2, double f)
        {
            if (f <= 0) return (lat1, lon1);
            if (f >= 1) return (lat2, lon2);

            const double Deg = Math.PI / 180.0;
            double p1 = lat1 * Deg, l1 = lon1 * Deg;
            double p2 = lat2 * Deg, l2 = lon2 * Deg;

            double dp = (p2 - p1) / 2.0, dl = (l2 - l1) / 2.0;
            double s = Math.Sin(dp) * Math.Sin(dp)
                     + Math.Cos(p1) * Math.Cos(p2) * Math.Sin(dl) * Math.Sin(dl);
            double d = 2.0 * Math.Asin(Math.Min(1.0, Math.Sqrt(s)));

            // Coincidentes (o casi): no hay arco que interpolar.
            if (d < 1e-9) return (lat1, lon1);

            double sinD = Math.Sin(d);
            double a = Math.Sin((1.0 - f) * d) / sinD;
            double b = Math.Sin(f * d) / sinD;

            double x = a * Math.Cos(p1) * Math.Cos(l1) + b * Math.Cos(p2) * Math.Cos(l2);
            double y = a * Math.Cos(p1) * Math.Sin(l1) + b * Math.Cos(p2) * Math.Sin(l2);
            double z = a * Math.Sin(p1) + b * Math.Sin(p2);

            double lat = Math.Atan2(z, Math.Sqrt(x * x + y * y)) / Deg;
            double lon = Math.Atan2(y, x) / Deg;
            return (lat, lon);
        }
    }
}
