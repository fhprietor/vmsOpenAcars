using System;
using System.Collections.Generic;

namespace vmsOpenAcars.Helpers
{
    /// <summary>
    /// **El G del toque, medido en la ventana del contacto.**
    ///
    /// Hasta v0.9.69 el valor que se **puntuaba** no se medía: lo deducía una tabla del descenso
    /// (`FlightManager.CalculateGForce`, de 1,00 g con 30 fpm a 2,50 g) y el valor que se **veía** era
    /// la lectura del acelerómetro `0x11BA`. Medido en el PIREP real `D0Wy9Pa7GNB5pYEo` (AT76):
    ///
    /// ```
    /// 01:36:32  G-Force: 1.32g (Normal)                 ← 0x11BA
    /// 01:36:32  Landing recorded: -346 fpm, 1.85 G …    ← la tabla
    /// 01:43:04  −15 pts: G-Force (1.85g at touchdown)
    /// ```
    ///
    /// El mismo toque con dos cifras, y la que decidía los 15 puntos era la inventada: `-346 fpm` cae
    /// en el escalón `vs <= 350 → 1.85`. Con la lectura del avión (`1,32 g`) el escalón es
    /// `≤1.5g = 0 puntos`: el vuelo no debía estar penalizado. La incoherencia física también lo dice
    /// —un contacto de **−346 fpm** es un firme normal, y **1,85 g** le correspondería a uno mucho más
    /// duro—, mientras que 1,32 g con −346 fpm encaja. En el vuelo anterior `nROPdk4GxYY5AOy1` el
    /// registro decía **1,05 G** y no hubo penalización: también salía de la tabla (`vs <= 90`).
    ///
    /// **Por qué una ventana y no un pico de todo el vuelo.** El acumulador anterior
    /// (`_peakGforceApproach`) no se reiniciaba hasta el aterrizaje, así que «el pico del toque» era en
    /// realidad **el pico de todo el vuelo**: una racha de turbulencia en el ascenso podía decidir la
    /// nota del aterrizaje. Y una ventana más larga que el contacto tampoco vale, porque lo que sigue
    /// al primer contacto es el **rebote**, no el impacto. Un segundo es la ventana que cubre la
    /// toma completa (a 20 Hz son ~20 muestras) y deja fuera todo lo anterior.
    ///
    /// Este filtro no decide nada por sí solo: mientras no haya muestras —un addon que no escriba
    /// `0x11BA`— <see cref="Peak"/> vale 0, que es «no medido», y el scoring lo omite en vez de
    /// inventar una cifra.
    /// </summary>
    internal sealed class TouchdownGForceWindow
    {
        /// <summary>
        /// Lo que dura la ventana del contacto. Un segundo cubre la aproximación final inmediata y el
        /// toque; a 20 Hz son ~20 muestras, suficientes para no perder el pico del impacto.
        /// </summary>
        internal const double WindowSeconds = 1.0;

        private struct Sample
        {
            internal double   G;
            internal DateTime Utc;
        }

        private readonly List<Sample> _samples = new List<Sample>();
        private DateTime _newest = DateTime.MinValue;

        /// <summary>
        /// Añade la lectura **actual** del acelerómetro. Solo se alimenta con el avión en el aire: en
        /// tierra el sensor ya está filtrado y la muestra del contacto es la última que interesa.
        /// </summary>
        internal void Observe(double g, DateTime utc)
        {
            if (double.IsNaN(g) || double.IsInfinity(g) || g <= 0.0) return;

            _newest = utc;
            _samples.Add(new Sample { G = g, Utc = utc });
            Prune(utc);
        }

        /// <summary>
        /// El mayor G de la ventana que termina en la última muestra, o **0 si no hay ninguna** (que es
        /// «no medido», no «1 g»).
        /// </summary>
        internal double Peak
        {
            get
            {
                Prune(_newest);
                double peak = 0.0;
                for (int i = 0; i < _samples.Count; i++)
                    if (_samples[i].G > peak) peak = _samples[i].G;
                return peak;
            }
        }

        /// <summary>Cuántas muestras tiene la ventana (para los tests y el diagnóstico).</summary>
        internal int Count => _samples.Count;

        /// <summary>
        /// Se suelta al **despegar**: la ventana del aterrizaje siguiente empieza ahí y no arrastra
        /// nada del vuelo anterior (importa en un touch-and-go, donde el avión vuelve a estar en el
        /// aire un segundo después del toque).
        /// </summary>
        internal void Reset()
        {
            _samples.Clear();
            _newest = DateTime.MinValue;
        }

        private void Prune(DateTime reference)
        {
            if (reference == DateTime.MinValue) return;
            DateTime floor = reference.AddSeconds(-WindowSeconds);
            int drop = 0;
            while (drop < _samples.Count && _samples[drop].Utc < floor) drop++;
            if (drop > 0) _samples.RemoveRange(0, drop);
        }
    }
}
