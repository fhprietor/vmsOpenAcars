using System;

namespace vmsOpenAcars.Helpers
{
    /// <summary>
    /// **La única escala de flaps del cliente: el porcentaje del recorrido del mando.**
    ///
    /// Hasta v0.9.69 el mismo offset —`0x0BDC`, «posición del handle 0-16383»— se decodificaba de dos
    /// maneras y las dos se publicaban como «porcentaje». Medido en el PIREP real
    /// `D0Wy9Pa7GNB5pYEo` (AT76), en el mismo segundo del toque:
    ///
    /// ```
    /// 01:36:32  Flaps: 40% | Spoilers: 0%        ← DecodeFlaps: tabla de GRADOS de Boeing
    /// 01:36:32  … FLAPS 100%                     ← 0x0BDC / 16383 × 100
    /// ```
    ///
    /// No era un redondeo: eran **dos decodificadores del mismo número**. `DecodeFlaps` devolvía
    /// {0, 1, 2, 5, 10, 15, 25, 30, 40} —las compuertas de un Boeing— y el cliente lo imprimía con un
    /// `%` detrás. En el ATR, cuyas únicas posiciones son **15 % y 30 %**, eso estiraba la escala: el
    /// detente de 15 salía como **25** y el de 30 como **40**, que es exactamente la secuencia
    /// «hacia atrás» del log de la aproximación (`0% → 40%` a las 01:32:15, `40% → 25%` cinco
    /// segundos después, `25% → 40%` a las 01:32:37).
    ///
    /// **La escala que queda es la del offset**, `raw / 16383 × 100`, y se elige por dos razones:
    /// (1) es **lo único que el dato mide** —el offset publica la posición del mando, no una
    /// compuerta—; y (2) ya viene **normalizada contra el recorrido real de cada avión**, porque el
    /// addon declara su propio fondo de escala en esos 16 383. En el ATR eso se ve en el mismo log:
    /// con el mando a fondo —detente de 30— el offset da **100 %**, no 40. No hace falta ninguna
    /// tabla de «máximo por tipo», y por eso no hay ningún caso en el que falte el dato y haya que
    /// degradar la compuerta.
    ///
    /// **El umbral del 50 % del scoring no se toca** y sigue midiendo lo que medía: «al menos la
    /// mitad del recorrido de flaps desplegado». En el ATR el detente de 30 vale 100 % y el de 15
    /// ronda la mitad, así que la escala no lo deja inalcanzable. Lo que estaba mal era la línea del
    /// log, no la puerta: a 1 000 ft AGL el avión llevaba el mando a fondo (100 %) y la puerta dijo
    /// `STABILIZED — all criteria met` con razón; el `40 %` que se imprimía al lado era el otro
    /// decodificador.
    /// </summary>
    internal static class FlapsScale
    {
        /// <summary>El fondo de escala del offset: `0x0BDC` va de 0 a 16 383.</summary>
        internal const double RawFull = 16383.0;

        /// <summary>
        /// **El umbral de la puerta de estabilización, sin mover.** Se conserva el 50 % histórico para
        /// que un cambio de escala no altere puntuaciones: lo único que cambia es que ahora lo que se
        /// compara y lo que se imprime son el mismo número.
        /// </summary>
        internal const double StabilizedMinPercent = 50.0;

        /// <summary>
        /// Posición del mando (0–16 383) → porcentaje del recorrido del avión (0–100). Fuera de rango
        /// se recorta: un addon que publique de más no puede dar un 130 %.
        /// </summary>
        internal static double PercentOfTravel(double raw)
        {
            if (double.IsNaN(raw) || raw <= 0.0) return 0.0;
            if (raw >= RawFull) return 100.0;
            return raw / RawFull * 100.0;
        }

        /// <summary>
        /// **La banda en la que cae un porcentaje, solo para no escribir una línea de log por punto.**
        /// El mando se mueve en pasos de 0,006 % (1 de 16 383), así que comparar el porcentaje
        /// directo escribiría decenas de líneas por cada selección. Estas ocho bandas son las mismas
        /// que usaba el viejo `DecodeFlaps`, conservadas **solo como cuantizador**: su número no se
        /// publica nunca.
        /// </summary>
        internal static int BandKey(double percent)
        {
            if (double.IsNaN(percent) || percent < 2.0)  return 0;
            if (percent < 13.0) return 1;
            if (percent < 26.0) return 2;
            if (percent < 32.0) return 3;
            if (percent < 45.0) return 4;
            if (percent < 62.0) return 5;
            if (percent < 78.0) return 6;
            return 7;
        }
    }
}
