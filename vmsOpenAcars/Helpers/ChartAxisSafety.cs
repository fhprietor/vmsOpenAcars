using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms.DataVisualization.Charting;

namespace vmsOpenAcars.Helpers
{
    /// <summary>
    /// **Un gráfico no puede reventar al pintarse, ni en pantalla ni fuera de pantalla.**
    ///
    /// La causa raíz, con la pila exacta que se vio en el mantenedor:
    ///
    /// <code>
    /// System.InvalidOperationException: Axis Object - Auto interval does not have proper value.
    ///    at System.Windows.Forms.DataVisualization.Charting.Axis.EstimateAxis(...)
    ///    at ...ChartArea.SetDefaultAxesValues()
    ///    at ...Chart.SetData(Boolean, Boolean)
    ///    at ...ChartArea.ReCalcInternal()
    ///    at ...ChartPicture.Paint(Graphics, Boolean)
    ///    at ...Chart.OnPaint(PaintEventArgs)
    ///    at ...Control.OnPrint(PaintEventArgs)
    ///    at ...Control.WmPrintClient(Message&amp;, IntPtr, IntPtr, IntPtr)
    /// </code>
    ///
    /// El motor de gráficos estima el rango y el paso de **cada** eje al pintar, y **no sabe
    /// estimarlos cuando el área no tiene ni un punto**: se queda sin escala de la que sacar el
    /// intervalo automático y lanza. El camino `OnPrint → WmPrintClient` es el dibujado **a un mapa
    /// de bits** (`Control.DrawToBitmap`, que manda `WM_PRINTCLIENT`), no el pintado en pantalla: por
    /// eso aparecía al renderizar PNG desde los tests y no al mirar la ventana.
    ///
    /// **Lo que hace esta clase: dejar cada área en un estado explícito, del que el motor no tenga
    /// que deducir nada.**
    ///
    /// - **Área sin ningún punto** (una serie vacía, un área que se crea siempre como la de *pitch* y
    ///   que se queda sin serie cuando la traza no la trae, o los cuatro gráficos del análisis cuando
    ///   el vuelo no tiene traza de aproximación): el eje Y se **fija** y el área se marca
    ///   **`SIN DATOS`**, que es lo que de verdad ocurre — no un cero disfrazado de medida—. El X se
    ///   respeta si ya era usable (en la ventana del flare todas las áreas comparten el eje X) y si no
    ///   se fija también.
    /// - **Área con datos y el eje en automático**: **no se toca**. Ahí el motor sí sabe estimarlo, y
    ///   fijarlo cambiaría encuadres que ya están decididos y probados —la Y autoescalada del closeup
    ///   o las escalas de LATERAL y VS—. Lo único que se corrige es un rango **degenerado**
    ///   (`mínimo == máximo` o invertido), que tampoco tiene intervalo del que sacar marcas.
    /// - **Puntos no finitos** (`NaN`/infinito) se **quitan** antes de contar: una anotación del
    ///   closeup se ancla en el mínimo del eje, y en un área sin datos ese mínimo es `NaN`, así que
    ///   arrastraba puntos envenenados que ensuciaban la estimación del motor.
    ///
    /// Nada de esto decide una medida: son los límites de un marco. Ver el porqué de no inventar
    /// escalas en `FlareChartLayout` (cada eje del dato lo elige el helper del dominio).
    /// </summary>
    internal static class ChartAxisSafety
    {
        /// <summary>Suelo del marco de un área sin datos. Un marco, no una medida.</summary>
        internal const double NoDataMinimum = 0.0;

        /// <summary>Techo del marco de un área sin datos.</summary>
        internal const double NoDataMaximum = 1.0;

        /// <summary>Clave del rótulo que marca un área sin datos (en los dos `.json`).</summary>
        internal const string NoDataKey = "Chart_NoData";

        /// <summary>
        /// ¿El rango sirve para que el motor saque marcas? Tiene que ser finito y tener recorrido:
        /// `mínimo == máximo` (una serie plana con el eje fijado a mano) es justo el estado en el que
        /// no hay intervalo automático que valga.
        /// </summary>
        internal static bool IsUsable(double minimum, double maximum)
        {
            if (double.IsNaN(minimum) || double.IsNaN(maximum)) return false;
            if (double.IsInfinity(minimum) || double.IsInfinity(maximum)) return false;
            return maximum - minimum > 0.0;
        }

        /// <summary>
        /// El marco que se deduce de los valores que hay: el recorrido real con un **5 % de aire** por
        /// lado, para que la curva no toque los bordes. Con un valor constante —una traza plana, que es
        /// el caso que deja el rango sin recorrido— se abre medio punto por lado: es el mínimo que
        /// convierte un rango degenerado en uno con intervalo. Sin valores, el respaldo que le pase el
        /// llamante.
        /// </summary>
        internal static (double Minimum, double Maximum) RangeFrom(IList<double> values,
                                                                   double fallbackMinimum,
                                                                   double fallbackMaximum)
        {
            double min = double.MaxValue, max = double.MinValue;
            int count = 0;

            if (values != null)
            {
                foreach (double v in values)
                {
                    if (double.IsNaN(v) || double.IsInfinity(v)) continue;
                    if (v < min) min = v;
                    if (v > max) max = v;
                    count++;
                }
            }

            if (count == 0) return (fallbackMinimum, fallbackMaximum);
            if (max > min)
            {
                double pad = (max - min) * 0.05;
                return (min - pad, max + pad);
            }
            return (min - 0.5, max + 0.5);
        }

        /// <summary>¿Esa área tiene algún punto? Es lo que decide si su eje se puede rotular.</summary>
        internal static bool AreaHasData(Chart chart, ChartArea area)
            => chart != null && area != null && Collect(chart, area, xAxis: false).Count > 0;

        /// <summary>
        /// **El repaso que deja el gráfico en un estado que no puede lanzar.** Se llama al final de
        /// construir el gráfico, cuando ya están todas las series, las bandas y las marcas.
        /// </summary>
        /// <returns>Cuántas áreas se quedaron sin datos y hubo que marcar.</returns>
        internal static int EnsureExplicit(Chart chart)
        {
            if (chart == null) return 0;

            foreach (var series in chart.Series) RemoveNonFinitePoints(series);

            int marked = 0;
            foreach (var area in chart.ChartAreas)
            {
                var xs = Collect(chart, area, xAxis: true);
                var ys = Collect(chart, area, xAxis: false);

                if (ys.Count == 0)
                {
                    // Sin un solo punto el motor no tiene de dónde sacar el intervalo automático: es
                    // el estado que lanza. Se fija el marco y se dice que no hay datos.
                    LockIfUnusable(area.AxisX);
                    area.AxisY.Minimum = NoDataMinimum;
                    area.AxisY.Maximum = NoDataMaximum;
                    MarkNoData(chart, area);
                    marked++;
                    continue;
                }

                FixDegenerate(area.AxisX, xs);
                FixDegenerate(area.AxisY, ys);
            }

            return marked;
        }

        /// <summary>El X ya era usable (las áreas comparten eje): se respeta. Si no, marco fijo.</summary>
        private static void LockIfUnusable(Axis axis)
        {
            if (axis == null) return;
            if (IsUsable(axis.Minimum, axis.Maximum)) return;
            axis.Minimum = NoDataMinimum;
            axis.Maximum = NoDataMaximum;
        }

        /// <summary>
        /// Un eje **automático con datos** lo estima el motor y se deja como está. Lo que no se puede
        /// dejar es un rango explícito sin recorrido: se sustituye por el de los propios datos.
        /// </summary>
        private static void FixDegenerate(Axis axis, IList<double> values)
        {
            if (axis == null) return;
            if (double.IsNaN(axis.Minimum) || double.IsNaN(axis.Maximum)) return;   // automático
            if (IsUsable(axis.Minimum, axis.Maximum)) return;

            var range = RangeFrom(values, NoDataMinimum, NoDataMaximum);
            axis.Minimum = range.Minimum;
            axis.Maximum = range.Maximum;
        }

        /// <summary>Los valores finitos que esa área tiene en ese eje, serie a serie.</summary>
        private static List<double> Collect(Chart chart, ChartArea area, bool xAxis)
        {
            var values = new List<double>();
            foreach (var series in chart.Series)
            {
                if (series == null || series.ChartArea != area.Name) continue;
                foreach (var point in series.Points)
                {
                    if (point == null) continue;
                    double v = xAxis ? point.XValue : FirstY(point);
                    if (!double.IsNaN(v) && !double.IsInfinity(v)) values.Add(v);
                }
            }
            return values;
        }

        private static double FirstY(DataPoint point)
            => point.YValues != null && point.YValues.Length > 0 ? point.YValues[0] : double.NaN;

        /// <summary>
        /// Quita los puntos que no son números. `NaN` en un punto no es un dato del que el motor pueda
        /// sacar un rango, y con uno solo de esos el eje puede quedar sin escala.
        /// </summary>
        private static void RemoveNonFinitePoints(Series series)
        {
            if (series == null) return;
            for (int i = series.Points.Count - 1; i >= 0; i--)
            {
                var point = series.Points[i];
                if (point == null ||
                    double.IsNaN(point.XValue) || double.IsInfinity(point.XValue) ||
                    double.IsNaN(FirstY(point)) || double.IsInfinity(FirstY(point)))
                    series.Points.RemoveAt(i);
            }
        }

        /// <summary>
        /// Marca el área como **sin datos**, centrada en ella. Es la verdad del dato: el área existe
        /// porque el gráfico la reserva, no porque haya algo que enseñar. No se repite si ya está.
        /// </summary>
        private static void MarkNoData(Chart chart, ChartArea area)
        {
            string name = NoDataAnnotationName(area.Name);

            foreach (var existing in chart.Annotations)
            {
                var annotation = existing as TextAnnotation;
                if (annotation != null && annotation.Name == name) return;
            }

            // **Sin `ClipToChartArea`, a propósito.** Dentro del área la anotación hereda el sistema de
            // coordenadas del eje X, y en estos gráficos el eje X va **invertido** (`IsReversed`: la
            // izquierda es «antes del umbral»), así que el rótulo se pintaba **espejado** —visto en el
            // PNG: `SOTAD NIS`—. Fuera del área, las coordenadas son porcentajes del propio gráfico y
            // el texto sale derecho; el centro del área se calcula con su `Position`, que es lo que el
            // formulario ya dejó fijado.
            chart.Annotations.Add(new TextAnnotation
            {
                Name      = name,
                Text      = L._(NoDataKey),
                X         = CentreX(area),
                Y         = CentreY(area),
                Font      = new Font("Consolas", 9, FontStyle.Bold),
                ForeColor = Color.FromArgb(170, 190, 210),
                Alignment = ContentAlignment.MiddleCenter,
            });
        }

        /// <summary>
        /// El centro del área en porcentajes del **gráfico**, para no depender del sistema de
        /// coordenadas del eje.
        ///
        /// Con `Position` sin resolver —el caso de los cuatro gráficos del análisis, que dejan el
        /// reparto al motor y lo resuelven al pintar— el centro que sale de ahí es una esquina
        /// (visto en el PNG: el rótulo pegado al título), así que un `Position` que no describe una
        /// banda de verdad se sustituye por el centro del gráfico. En la ventana del flare las áreas
        /// sí llevan su `ElementPosition` explícito y cada rótulo va al centro de su banda.
        /// </summary>
        private static double CentreX(ChartArea area)
        {
            var pos = area.Position;
            return IsUsablePosition(pos) ? pos.X + pos.Width / 2.0 : 50.0;
        }

        /// <inheritdoc cref="CentreX"/>
        private static double CentreY(ChartArea area)
        {
            var pos = area.Position;
            return IsUsablePosition(pos) ? pos.Y + pos.Height / 2.0 : 50.0;
        }

        private static bool IsUsablePosition(ElementPosition pos)
        {
            if (pos == null) return false;
            if (float.IsNaN(pos.X) || float.IsNaN(pos.Y)) return false;
            if (float.IsNaN(pos.Width) || float.IsNaN(pos.Height)) return false;
            // Menos de un 5 % del gráfico no es una banda: es un `Position` sin resolver.
            return pos.Width >= 5f && pos.Height >= 5f;
        }

        /// <summary>
        /// El nombre de la anotación de un área. Se puede identificar por él —los tests lo hacen—
        /// sin depender de que la anotación esté recortada al área, que es justo lo que se evita.
        /// </summary>
        internal static string NoDataAnnotationName(string areaName) => "nodata:" + areaName;
    }
}
