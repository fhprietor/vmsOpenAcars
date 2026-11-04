using System;
using System.Collections.Generic;
using vmsOpenAcars.Models;

namespace vmsOpenAcars.Helpers
{
    /// <summary>
    /// El resumen de viento de la **final**: el máximo de componente cruzada y el máximo de cola
    /// sobre todos los puntos de la traza de aproximación que traen viento, cada uno con su
    /// **distancia al umbral**.
    ///
    /// **Por qué existe.** Hasta v0.9.61 la traza de 2 s del análisis no llevaba viento: solo se
    /// guardaba la lectura del **instante del toque**. Con eso no se puede contar lo que pasó entre
    /// 5 NM y el umbral —un giro de dirección, una racha que entra en la final o una cizalladura—,
    /// porque el promedio del descenso no es la racha que el piloto vio. El resumen elige los dos
    /// números que se comparan contra las tablas del avión (cruzada y cola máximas) en vez de pintar
    /// una tira de 60 valores, y dice **dónde** ocurrieron, que es lo que convierte el dato en algo
    /// interpretable.
    ///
    /// **Degrada sin datos.** Los puntos con `WindDirDeg` o `WindSpeedKt` nulos se ignoran (son
    /// lecturas que no existieron: el simulador publicó 0/0 o FSUIPC no estaba conectado) y sin
    /// ningún punto con viento el resumen sale con `HasWind = false` y quien pinta no dibuja nada.
    /// Un viento **en calma** (0 kt explícito) sí es un dato y se marca con `AllCalm`, que no es lo
    /// mismo que «no hay dato»: la calma es una observación, el hueco es una ausencia.
    ///
    /// **La descomposición no se repite aquí**: se delega en <see cref="WindComponents"/>, que es la
    /// única regla de cara/cruzada y la que documenta el signo (cruzada positiva desde la derecha;
    /// cara positiva, **cola negativa**). Este helper solo agrega: se queda con el **valor absoluto**
    /// de la cola (el máximo de cola es una magnitud, no un número negativo) y con el **valor
    /// absoluto** de la cruzada, porque «máx cruzada 14 kt» se lee igual desde la derecha que desde
    /// la izquierda y el lado ya lo dice la línea del toque.
    /// </summary>
    internal struct FinalWindReport
    {
        /// <summary>Hay al menos un punto con viento **y** con rumbo de pista para descomponerlo.</summary>
        internal bool HasWind;

        /// <summary>Todos los puntos con viento estaban en calma (0 kt). Es un dato, no un hueco.</summary>
        internal bool AllCalm;

        /// <summary>Puntos de la traza que traían viento y se pudieron descomponer.</summary>
        internal int WindSamples;

        /// <summary>Máxima componente cruzada, en nudos, **en valor absoluto**.</summary>
        internal double MaxCrosswindKt;

        /// <summary>Distancia al umbral del punto donde se dio esa cruzada máxima, en NM.</summary>
        internal double MaxCrosswindDistNm;

        /// <summary>Hubo al menos un punto con componente en cola (&lt; 0 en <see cref="WindComponents"/>).</summary>
        internal bool HasTailwind;

        /// <summary>Máxima componente en cola, en nudos, **en positivo** (magnitud).</summary>
        internal double MaxTailwindKt;

        /// <summary>Distancia al umbral del punto donde se dio esa cola máxima, en NM.</summary>
        internal double MaxTailwindDistNm;
    }

    /// <summary>
    /// Regla pura del resumen de viento de la final. Ver <see cref="FinalWindReport"/> para el
    /// contrato, la degradación sin datos y el porqué de quedarse con los valores absolutos.
    /// </summary>
    internal static class FinalWindSummary
    {
        /// <summary>
        /// Recorre la traza y devuelve el resumen. Los puntos sin viento se **ignoran** —no se
        /// cuentan como calma— y sin rumbo de pista no se puede descomponer nada: en ese caso
        /// también sale `HasWind = false`, porque el formulario no tendría qué enseñar.
        /// </summary>
        internal static FinalWindReport Compute(
            IEnumerable<ApproachTrackPoint> track, double? runwayHeadingTrueDeg)
        {
            var report = new FinalWindReport();

            if (track == null || !runwayHeadingTrueDeg.HasValue) return report;

            bool anyNonCalm = false;
            foreach (var point in track)
            {
                if (point == null) continue;
                if (!point.WindDirDeg.HasValue || !point.WindSpeedKt.HasValue) continue;

                var components = WindComponents.Compute(
                    point.WindDirDeg, point.WindSpeedKt, null, runwayHeadingTrueDeg);
                if (!components.Available) continue;

                report.WindSamples++;
                if (!components.Calm) anyNonCalm = true;

                // La distancia se publica al umbral y nunca negativa: después del toque la traza
                // sigue un par de segundos y ahí «−0,1 NM» no significa nada para el piloto.
                double distNm = point.DistNm > 0 ? point.DistNm : 0.0;

                double crosswind = Math.Abs(components.CrosswindKt);
                if (report.WindSamples == 1 || crosswind > report.MaxCrosswindKt)
                {
                    report.MaxCrosswindKt     = crosswind;
                    report.MaxCrosswindDistNm = distNm;
                }

                // Cola = cara negativa. Se guarda en positivo porque «máx cola 6 kt» es la magnitud
                // que se compara con el límite del avión; el signo ya lo lleva `WindComponents`.
                if (components.HeadwindKt < 0)
                {
                    double tailwind = -components.HeadwindKt;
                    if (!report.HasTailwind || tailwind > report.MaxTailwindKt)
                    {
                        report.MaxTailwindKt     = tailwind;
                        report.MaxTailwindDistNm = distNm;
                        report.HasTailwind       = true;
                    }
                }
            }

            report.HasWind  = report.WindSamples > 0;
            report.AllCalm  = report.HasWind && !anyNonCalm;
            return report;
        }

        /// <summary>
        /// ¿Esta lectura del simulador **no** es un dato de viento? El centinela que publica FSUIPC
        /// cuando no tiene viento es **0/0**, y con FSUIPC desconectado las dos propiedades valen 0:
        /// guardar ese 0/0 en la traza fingiría un viento del norte en calma durante todo el
        /// descenso. Se guarda NULL y el análisis simplemente no enseña la línea.
        /// </summary>
        internal static bool IsMissingSample(bool connected, int speedKt, double dirDeg)
        {
            if (!connected) return true;
            return speedKt == 0 && dirDeg == 0.0;
        }
    }
}
