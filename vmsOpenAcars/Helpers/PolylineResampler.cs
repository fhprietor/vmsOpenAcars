using System;
using System.Collections.Generic;

namespace vmsOpenAcars.Helpers
{
    /// <summary>
    /// Recorte determinista de una polilínea al tope que acepta el transporte.
    ///
    /// NavData limita el `planned.polyline` a **500 puntos** y el cuerpo del POST a **256 KB**
    /// (mensaje suyo nº 10, §3.3: pidieron el tope porque con polilíneas de ~100 puntos se pasaban
    /// de cuerpo). Recortar por el principio —quedarse con los 500 primeros— o por el final
    /// destruiría justo los dos puntos que dan sentido a la observación: el primero es dónde
    /// arrancó el rodaje y el último es el punto de espera de la pista contra el que se mide
    /// «no empeora». Por eso se conservan **siempre** el primero y el último y el resto se
    /// muestrea **a intervalos iguales de índice**, que es lo que mantiene la geometría del camino
    /// entero en vez de la de un tramo.
    ///
    /// Puro a propósito: sin red ni WinForms, para poder fijar el comportamiento con una polilínea
    /// de 1 200 puntos sin salir del test.
    /// </summary>
    internal static class PolylineResampler
    {
        /// <summary>Tope de puntos por polilínea que NavData acepta en `planned.polyline`.</summary>
        internal const int MaxPolylinePoints = 500;

        /// <summary>
        /// Devuelve la polilínea reducida a <paramref name="max"/> puntos como mucho, conservando el
        /// primero, el último y un muestreo uniforme en medio. Con <paramref name="max"/> puntos o
        /// menos, la devuelve **intacta** (mismo orden y mismos valores). Determinista: el mismo
        /// índice de entrada da el mismo índice de salida en cualquier máquina.
        /// </summary>
        internal static List<(double Lat, double Lon)> Cap(
            IList<(double Lat, double Lon)> points, int max = MaxPolylinePoints)
        {
            var result = new List<(double Lat, double Lon)>();
            if (points == null || points.Count == 0 || max < 1) return result;

            // Sin recorte no se toca nada: 3 puntos → los 3, y 500 → los 500. Es la garantía de que
            // este helper solo actúa cuando de verdad hay que actuar.
            if (points.Count <= max)
            {
                for (int i = 0; i < points.Count; i++) result.Add(points[i]);
                return result;
            }

            if (max == 1) { result.Add(points[0]); return result; }

            for (int i = 0; i < max; i++)
            {
                // El índice se calcula sobre la recta 0..Count-1 repartida en max-1 tramos: i=0 cae
                // en el primero y i=max-1 en el último, sin casos especiales.
                int idx = (int)Math.Round((double)i * (points.Count - 1) / (max - 1));
                if (idx < 0) idx = 0;
                if (idx > points.Count - 1) idx = points.Count - 1;
                result.Add(points[idx]);
            }
            return result;
        }
    }
}
