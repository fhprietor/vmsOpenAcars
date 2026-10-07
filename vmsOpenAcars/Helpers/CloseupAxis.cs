using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Forms.DataVisualization.Charting;

namespace vmsOpenAcars.Helpers
{
    /// <summary>
    /// **El eje X de distancia al umbral, en pies enteros.**
    ///
    /// El eje del closeup se pinta con `AxisX.IsReversed` y un rango de 9 500 ft (−4 500 … 5 000), y
    /// el motor de gráficos elige las marcas **solo**: dividía ese rango en cuatro y sacaba
    /// **−2 250 · −750 · 750 · 2 250 · 3 750**. En un eje de distancia al umbral las fracciones de pie
    /// no dicen nada: se mide en pies, y las referencias que el piloto busca son `THR` y las marcas
    /// redondas.
    ///
    /// Este helper decide las dos cosas que hasta ahora no decidía nadie: el **paso**
    /// (`100/250/500/1 000/2 500/5 000 ft`) y la **lista de rótulos** con su texto ya formateado
    /// (`5.000`, `THR`, `1.500`…). Es puro, sin estado, y con test.
    ///
    /// **Los rótulos van en la rejilla del paso, no en los bordes del encuadre.** El encuadre del
    /// closeup es 5 000 / 4 500 y el paso son 1 000: 4 500 no es múltiplo de 1 000 y **no hace
    /// falta que lo sea** — el eje llega hasta 4 500 y la última marca cae en 4 000. Exigir bordes
    /// múltiplos del paso obligaba a estirar el encuadre a 5 000/5 000 (y eso sí sería cambiar lo
    /// que se ve), o a quedarse sin rótulos. Ninguna de las dos cosas.
    /// </summary>
    internal static class CloseupAxis
    {
        /// <summary>
        /// Máximo de marcas que se aceptan. Se probó con 13 y **no cabe**: en el perfil vertical, que
        /// ocupa media ventana de 1 000 px (~450 px de rejilla), 11 rótulos dejan ~40 px por marca y
        /// el signo menos se recorta —`-3.000` se pinta como `3.000`, que miente sobre el lado del
        /// umbral—. Con **9** quedan ~50 px por marca y entran enteros los rótulos con su signo.
        /// </summary>
        internal const int MaxLabels = 9;

        /// <summary>Pasos candidatos, de más fino a más grueso. Es la lista entera de unidades que se usan.</summary>
        internal static readonly double[] NiceSteps = { 100.0, 250.0, 500.0, 1000.0, 2500.0, 5000.0 };

        /// <summary>Texto del umbral: es el punto que se mira y no lleva número.</summary>
        internal const string ThresholdLabel = "THR";

        /// <summary>
        /// El **paso** del eje para un encuadre dado: el candidato más fino que deja ≤
        /// <see cref="MaxLabels"/> rótulos sobre la rejilla del paso.
        /// </summary>
        internal static double StepFor(double beforeFt, double afterFt)
        {
            double before = Numeric(beforeFt);
            double after  = Numeric(afterFt);

            foreach (double step in NiceSteps)
                if (LabelCount(before, after, step) <= MaxLabels) return step;

            return NiceSteps[NiceSteps.Length - 1];
        }

        /// <summary>
        /// Cuántas marcas caen dentro del encuadre con ese paso: los múltiplos del paso desde
        /// `-afterFt` a `beforeFt`, **más el del umbral**, que siempre está.
        /// </summary>
        /// <summary>
        /// Cuántas marcas caen dentro del encuadre con ese paso: las de cada lado **en la rejilla del
        /// paso**, contando desde `±paso`, más el del umbral.
        ///
        /// La cuenta no puede ser «cuántos múltiplos caben desde el borde»: el borde del closeup son
        /// 4 500 ft, que **no** es múltiplo del paso de 1 000, y desde ahí salían marcas de más (−5 000
        /// entra en el rango, y contarlo desde el borde lo perdía). Contar desde el **primer múltiplo
        /// del paso** es exacto: 4 500 → 4 (1 000, 2 000, 3 000, 4 000) y 5 000 → 5. Total 5 + THR +
        /// 4 = 10.
        /// </summary>
        internal static int LabelCount(double beforeFt, double afterFt, double stepFt)
        {
            if (stepFt <= 0.0) return 1;

            int n = 1;   // el THR (x = 0)
            if (beforeFt >= stepFt)
                n += (int)Math.Floor(beforeFt / stepFt);
            if (afterFt >= stepFt)
                n += (int)Math.Floor(afterFt / stepFt);
            return n;
        }

        /// <summary>
        /// **Los rótulos**, de izquierda a derecha (primero lo que está antes del umbral, en pies
        /// positivos porque el eje está invertido).
        ///
        /// Devuelve pares (posición, texto) listos para `AxisX.CustomLabels`. Si el encuadre no se
        /// puede rotular en pies enteros —bordes fraccionarios o no finitos— devuelve la lista
        /// **vacía**: mejor el eje automático que un rótulo que miente.
        /// </summary>
        internal static IList<KeyValuePair<double, string>> Labels(double beforeFt, double afterFt)
        {
            var labels = new List<KeyValuePair<double, string>>();
            if (!IsWholeNumber(beforeFt) || !IsWholeNumber(afterFt)) return labels;
            if (beforeFt <= 0.0 && afterFt <= 0.0)
            {
                labels.Add(new KeyValuePair<double, string>(0.0, ThresholdLabel));
                return labels;
            }

            double step = StepFor(beforeFt, afterFt);

            for (double v = beforeFt - (beforeFt % step); v >= step - 1e-6; v -= step)
                labels.Add(new KeyValuePair<double, string>(v, Format(v)));

            labels.Add(new KeyValuePair<double, string>(0.0, ThresholdLabel));

            for (double v = -step; v >= -afterFt - 1e-6; v -= step)
                labels.Add(new KeyValuePair<double, string>(v, Format(v)));

            return labels;
        }

        /// <summary>
        /// **Aplica el eje en pies enteros** a un `Axis` de WinForms Charts: el paso «bonito», los
        /// rótulos ya formateados y **THR** en el umbral, todo en `CustomLabels`.
        ///
        /// Vive aquí y no en el formulario que la usa porque es la misma decisión en los dos sitios
        /// que la necesitan —el closeup del perfil vertical y el gráfico del flare—, y porque así el
        /// formulario no tiene que saber cómo se rotula un eje: se lo pide al helper.
        /// </summary>
        internal static void ApplyTo(Axis axis, double beforeFt, double afterFt)
        {
            if (axis == null) return;

            axis.LabelStyle.Format = "";
            axis.CustomLabels.Clear();

            var labels = Labels(beforeFt, afterFt);
            if (labels.Count == 0) { Reset(axis); return; }

            double step = StepFor(beforeFt, afterFt);
            axis.Interval           = step;
            // `IntervalOffset` **a cero**: los ticks caen en los múltiplos del paso (… −1 000, 0,
            // 1 000 …) y el umbral siempre es uno de ellos. Ponerlo al paso desplazaba las marcas y
            // los rótulos dejaban de coincidir con su número.
            axis.IntervalOffset     = 0.0;
            axis.IntervalOffsetType = DateTimeIntervalType.Number;
            axis.IntervalType       = DateTimeIntervalType.Number;
            axis.IsStartedFromZero  = false;
            // Rótulos en horizontal, uno por marca. Sin fijar el ángulo, el motor los inclina o los
            // escalona en dos filas cuando no los ve holgados (se vio renderizando a PNG); aquí van
            // todos en la misma línea y el reparto lo garantiza la cuenta de `MaxLabels`.
            axis.LabelStyle.Angle   = 0;

            // La media anchura del rótulo en la rejilla: al centrarlo en su tick y no solaparlo con
            // el vecino, el motor lo pinta justo debajo del número que representa. Y se ancla al
            // `TickMark` (no al `GridLine`): con las dos rejillas activas el motor reparte los rótulos
            // en **dos filas alternas** para que quepan —visto en el PNG del closeup—, y aquí caben
            // todos en una.
            double half = step * 0.45;
            foreach (var label in labels)
            {
                var custom = axis.CustomLabels.Add(label.Key - half, label.Key + half, label.Value);
                if (custom != null) custom.GridTicks = GridTickTypes.TickMark;
            }
        }

        /// <summary>
        /// Devuelve el eje a como estaba antes de <see cref="ApplyTo"/>: los rótulos automáticos del
        /// motor. Lo necesita el perfil vertical al volver del closeup al eje en NM, donde el paso
        /// decimal lo elige el gráfico y no este helper.
        /// </summary>
        internal static void Reset(Axis axis)
        {
            if (axis == null) return;
            axis.CustomLabels.Clear();
            axis.LabelStyle.Format = "";
            axis.Interval = 0;              // 0 = automático
            axis.IntervalOffset = 0;
            axis.IsStartedFromZero = false;
        }

        /// <summary>
        /// `5.000` / `−1.500`: entero y con los miles separados por **punto**, que es el formato que
        /// pidió el mantenedor para este rótulo (`5.000 · 4.500 · … · 500 · THR`) y el que se lee sin
        /// ambigüedad con la escala en pies.
        /// </summary>
        internal static string Format(double value)
        {
            long whole    = (long)Math.Round(value);
            string digits = Math.Abs(whole).ToString(CultureInfo.InvariantCulture);
            var grouped   = new List<char>(digits.Length + digits.Length / 3);

            for (int i = 0; i < digits.Length; i++)
            {
                if (i > 0 && (digits.Length - i) % 3 == 0) grouped.Add('.');
                grouped.Add(digits[i]);
            }
            return (whole < 0 ? "-" : "") + new string(grouped.ToArray());
        }

        /// <summary>¿El valor es un número utilizable y entero? Filtra NaN, infinito y fracciones.</summary>
        internal static bool IsWholeNumber(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) return false;
            return Math.Abs(value - Math.Round(value)) < 1e-6;
        }

        /// <summary>
        /// Redondea un encuadre **hacia fuera** a la centena: **los dos bordes suben** al siguiente
        /// múltiplo de 100, así que el encuadre solo puede crecer y nunca recortar lo capturado.
        ///
        /// Ojo: redondear el borde «de después» a la baja (al múltiplo anterior) parecería lo natural
        /// y **recortaría la última muestra** —1 650 ft de captura con el borde en 1 600—. Los dos
        /// bordes van hacia arriba por eso, y no por simetría.
        ///
        /// Es lo que usa el gráfico del flare, cuyo encuadre sale de la traza (p. ej. +1 641 / −1 650
        /// → +1 700 / −1 700). La centena es el redondeo más fino que mantiene los rótulos enteros, y
        /// el paso bueno lo elige luego <see cref="StepFor"/> sobre el encuadre ya redondeado.
        /// </summary>
        internal static void RoundFrameOut(ref double beforeFt, ref double afterFt)
        {
            double before = Numeric(beforeFt);
            double after  = Numeric(afterFt);
            if (before <= 0.0 && after <= 0.0) { beforeFt = 0.0; afterFt = 0.0; return; }

            beforeFt = before > 0.0 ? Math.Ceiling(before / 100.0) * 100.0 : 0.0;
            afterFt  = after  > 0.0 ? Math.Ceiling(after  / 100.0) * 100.0 : 0.0;
        }

        private static double Numeric(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value < 0.0) return 0.0;
            return Math.Round(value);
        }
    }
}
