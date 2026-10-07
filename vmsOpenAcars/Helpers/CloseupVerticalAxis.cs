using System;
using System.Collections.Generic;

namespace vmsOpenAcars.Helpers
{
    /// <summary>
    /// **El encuadre vertical del closeup, ya decidido**: suelo, techo y paso del eje Y.
    ///
    /// Lo produce <see cref="CloseupVerticalAxis.Fit"/> y lo consume `LandingAnalysisForm` para
    /// fijar `AxisY.Minimum/Maximum/Interval`. `HasData` en falso significa «el tramo no se puede
    /// medir» y el formulario deja la Y como estaba: nunca se inventa una escala.
    /// </summary>
    internal sealed class CloseupVerticalFit
    {
        private CloseupVerticalFit() { }

        /// <summary>Hay al menos una muestra con altitud dentro del encuadre.</summary>
        internal bool HasData { get; private set; }

        /// <summary>Suelo del eje, en pies. Siempre **bajo** el cero del terreno (ver el porqué en el helper).</summary>
        internal double MinimumFt { get; private set; }

        /// <summary>Techo del eje, en pies. Múltiplo del paso.</summary>
        internal double MaximumFt { get; private set; }

        /// <summary>Paso del eje, de la lista de pasos «bonitos».</summary>
        internal double StepFt { get; private set; }

        /// <summary>Altitud más baja que entra en el encuadre (dato crudo, para el log y los tests).</summary>
        internal double DataMinFt { get; private set; }

        /// <summary>Altitud más alta que entra en el encuadre (dato crudo).</summary>
        internal double DataMaxFt { get; private set; }

        /// <summary>Muestras con altitud dentro del encuadre.</summary>
        internal int SampleCount { get; private set; }

        /// <summary>Recorrido del eje: `MaximumFt − MinimumFt`.</summary>
        internal double SpanFt => MaximumFt - MinimumFt;

        internal static CloseupVerticalFit None() => new CloseupVerticalFit();

        internal static CloseupVerticalFit Build(int sampleCount, double dataMinFt, double dataMaxFt,
                                                 double minimumFt, double maximumFt, double stepFt)
        {
            return new CloseupVerticalFit
            {
                HasData     = true,
                SampleCount = sampleCount,
                DataMinFt   = dataMinFt,
                DataMaxFt   = dataMaxFt,
                MinimumFt   = minimumFt,
                MaximumFt   = maximumFt,
                StepFt      = stepFt,
            };
        }
    }

    /// <summary>
    /// **El eje Y del closeup: la escala que hace visible el tramo final.**
    ///
    /// Hasta aquí la Y del perfil vertical era la del perfil **entero**: la traza de la
    /// aproximación es de 2 s y arranca a 2 563 ft AGL (vuelo 41 de la base local, `seq_no` 0), así
    /// que el motor de gráficos abría el eje a esos miles de pies y el tramo del closeup —147 ft de
    /// altitud en los últimos 1 653 ft antes del umbral— quedaba aplastado contra la línea del
    /// suelo. El closeup enseñaba la distancia correcta y una curva plana: el defecto que el
    /// mantenedor describió como «se ve aplastada».
    ///
    /// Este helper es puro, sin WinForms, y decide **una sola cosa**: qué escala vertical hace
    /// visible lo que hay dentro del encuadre. Tres reglas, cada una por un motivo medido:
    ///
    /// 1. **El cero del terreno va dentro.** El suelo es el cero del AGL y la pista se pinta ahí;
    ///    una escala que arrancara en la muestra más baja (147 ft en el vuelo 41) haría perder la
    ///    referencia de la altura real, que es justo lo que se viene a leer.
    /// 2. **Recorrido mínimo de <see cref="MinSpanFt"/> = 100 ft.** Es el seguro contra amplificar
    ///    ruido: en un tramo casi plano, con el suelo puesto, el eje mediría solo lo que varía el
    ///    dato. El caso real es el **vuelo 35** (SKBO → SKPE 08, toque a 1 165 ft), cuyo AGL en los
    ///    últimos 1 500 ft va de **0 a −19 ft** —la elevación del campo mal resuelta—: con el
    ///    recorrido mínimo el eje mide 100 ft y esa variación de 19 ft se lee como lo que es, ruido;
    ///    sin él, el eje se estiraría 5 veces para enseñar el ruido. Y por arriba: 100 ft es también
    ///    más fino que cualquier tramo que valga la pena mirar (a 3° de senda, 1 000 ft de
    ///    trayectoria son 52 ft de altitud), así que el mínimo no esconde nada.
    /// 3. **Pasos «bonitos»**: 25 / 50 / 100 / 250 / 500 ft, con **≤ <see cref="MaxIntervals"/>=5
    ///    intervalos** sobre el recorrido. Los rótulos del eje Y salen en esas marcas y son números
    ///    redondos; un eje con paso 37 ft o 118 ft sería tan ilegible como el de miles.
    ///
    /// **El suelo baja un paso entero bajo el cero** (`−max(paso, 50)`): así la banda de pista, que
    /// va **en** AGL 0, no queda cortada por el borde inferior (misma razón que el `−50` que ya usaba
    /// el formulario) y el cero cae en una marca del eje. Con paso 25 el suelo son 50 ft —dos
    /// marcas— para no quedarse más fino que la holgura que necesita la banda.
    ///
    /// **Sin datos no decide nada**: si no hay ninguna muestra con altitud dentro del encuadre
    /// —traza vacía, todo fuera del tramo o encuadre degenerado— devuelve `HasData = false` y el
    /// formulario deja la Y exactamente como estaba. Y un dato no finito (`NaN`, infinito) se
    /// descarta: no es una altitud.
    /// </summary>
    internal static class CloseupVerticalAxis
    {
        /// <summary>
        /// Recorrido mínimo del eje, en pies. El porqué está en el resumen de la clase: es lo que
        /// impide que un tramo casi plano (vuelo 35, 19 ft de variación real) se estire hasta
        /// convertir el ruido del dato en una gráfica.
        /// </summary>
        internal const double MinSpanFt = 100.0;

        /// <summary>
        /// Holgura **mínima** bajo el cero del terreno, en pies: la banda de pista va en AGL 0 con
        /// 8 px de grosor y sin suelo bajo ella quedaría cortada por el borde inferior del área.
        /// </summary>
        internal const double ClearanceFt = 50.0;

        /// <summary>Intervalos máximos que se aceptan en el eje. Ver el porqué en el resumen.</summary>
        internal const int MaxIntervals = 5;

        /// <summary>Las unidades de paso que se usan, de más fina a más gruesa.</summary>
        internal static readonly double[] NiceSteps = { 25.0, 50.0, 100.0, 250.0, 500.0 };

        /// <summary>
        /// **El encuadre vertical de un tramo**, a partir de las muestras con su X en el sistema del
        /// closeup (pies al umbral: positivo antes, negativo después) y su altitud AGL.
        ///
        /// Solo entran las muestras dentro de `[−afterFt, beforeFt]`: la escala describe **lo que se
        /// ve** en el encuadre, no lo que hay en la traza entera.
        /// </summary>
        internal static CloseupVerticalFit Fit(
            IEnumerable<(double XFt, double AltFt)> samples, double beforeFt, double afterFt)
        {
            double before = Frame(beforeFt);
            double after  = Frame(afterFt);
            if (before <= 0.0 && after <= 0.0) return CloseupVerticalFit.None();

            double min = double.MaxValue, max = double.MinValue;
            int count = 0;

            if (samples != null)
            {
                foreach (var s in samples)
                {
                    double x = s.XFt, alt = s.AltFt;
                    if (double.IsNaN(x) || double.IsInfinity(x)) continue;
                    if (double.IsNaN(alt) || double.IsInfinity(alt)) continue;
                    if (x > before || x < -after) continue;

                    if (alt < min) min = alt;
                    if (alt > max) max = alt;
                    count++;
                }
            }

            if (count == 0) return CloseupVerticalFit.None();

            // El paso se elige sobre el **recorrido mínimo**: un tramo de 19 ft de variación no puede
            // acabar con un paso de 5 ft por muy plano que sea.
            double step = StepFor(Math.Max(max - min, MinSpanFt));
            double floor, top;
            do
            {
                // Un paso entero bajo el cero, y nunca menos de `ClearanceFt`: así el cero cae en una
                // marca del eje y la banda de pista tiene sitio.
                floor = -Math.Max(step, ClearanceFt);
                // Un dato por debajo del suelo (AGL negativo real, visto en el vuelo 19: la
                // elevación del campo quedó ~300 ft alta y todo el tramo va de −61 a −375 ft) no se
                // puede recortar: el suelo baja a la marca que lo contiene.
                if (min < floor) floor = SnapDown(min, step);

                // Y el techo se queda, como mínimo, la misma holgura **por encima** del cero del
                // terreno: sin eso, una traza entera con AGL negativo dejaba la banda de pista pegada
                // al borde superior del área.
                top = SnapUp(Math.Max(Math.Max(max, ClearanceFt), floor + MinSpanFt), step);
            }
            // El paso elegido sobre el dato puede no llegar si el redondeo hacia fuera estira el
            // recorrido; se sube un escalón y se recalcula. Con el paso mayor se sale siempre.
            while ((top - floor) / step > MaxIntervals + 1e-9 && TryBump(ref step));

            return CloseupVerticalFit.Build(count, min, max, floor, top, step);
        }

        /// <summary>
        /// El paso «bonito» para un recorrido: el más fino que no pase de
        /// <see cref="MaxIntervals"/> intervalos.
        /// </summary>
        internal static double StepFor(double spanFt)
        {
            if (double.IsNaN(spanFt) || double.IsInfinity(spanFt) || spanFt <= 0.0)
                return NiceSteps[0];

            foreach (double step in NiceSteps)
                if (spanFt <= step * MaxIntervals + 1e-9) return step;

            return NiceSteps[NiceSteps.Length - 1];
        }

        private static bool TryBump(ref double step)
        {
            for (int i = 0; i < NiceSteps.Length - 1; i++)
            {
                if (NiceSteps[i] != step) continue;
                step = NiceSteps[i + 1];
                return true;
            }
            return false;
        }

        /// <summary>Sube al múltiplo del paso (hacia fuera, nunca recorta el dato).</summary>
        private static double SnapUp(double value, double step) =>
            Math.Ceiling(value / step - 1e-9) * step;

        /// <summary>Baja al múltiplo del paso.</summary>
        private static double SnapDown(double value, double step) =>
            Math.Floor(value / step + 1e-9) * step;

        /// <summary>Redondea el borde a pies enteros; lo no utilizable o negativo vale 0.</summary>
        private static double Frame(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value < 0.0) return 0.0;
            return Math.Round(value);
        }
    }
}
