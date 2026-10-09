using System;
using System.Collections.Generic;
using System.Globalization;
using vmsOpenAcars.Models;

namespace vmsOpenAcars.Helpers
{
    /// <summary>
    /// **Por qué no hay un tiempo umbral→toma que publicar.** Nunca se devuelve un `0` ni una
    /// aproximación: cada valor de este enumerado es un caso en el que el dato no existe y la
    /// respuesta correcta es callarse.
    /// </summary>
    internal enum ThresholdToTouchdownStatus
    {
        /// <summary>Hay tiempo: interpolado y redondeado a 0,1 s.</summary>
        Ok,

        /// <summary>Sin traza —o con menos de dos muestras utilizables—: no hay nada que interpolar.</summary>
        NoTrack,

        /// <summary>
        /// La traza no es fina: su cadencia es la de `approach_track` (**2 s**, 2,25 s medidos). Con
        /// ±1 s de error, publicarla como un dato de décimas sería vender precisión que no hay, así
        /// que se señala en vez de calcularse.
        /// </summary>
        CoarseTrack,

        /// <summary>La traza no cruza el umbral: no hay muestra a cada lado del eje.</summary>
        NoThresholdCrossing,

        /// <summary>No hay transición en el aire → en tierra detrás del cruce del umbral.</summary>
        NoGroundTransition,
    }

    /// <summary>El tiempo umbral→toma de un aterrizaje, o el motivo de no tenerlo.</summary>
    internal sealed class ThresholdToTouchdownResult
    {
        private ThresholdToTouchdownResult() { }

        /// <summary>Solo `true` con <see cref="ThresholdToTouchdownStatus.Ok"/>.</summary>
        internal bool   HasValue { get; private set; }

        /// <summary>Segundos del cruce del umbral al contacto, **redondeados a 0,1 s**. `NaN` sin dato.</summary>
        internal double Seconds  { get; private set; }

        internal ThresholdToTouchdownStatus Status { get; private set; }

        /// <summary>Cadencia mediana de la traza, en segundos (`NaN` si no se pudo medir). Es lo que
        /// decide si la traza es fina o la de 2 s.</summary>
        internal double MedianSampleIntervalSec { get; private set; }

        /// <summary>Muestras utilizadas (las que traen instante, distancia y no son nulas).</summary>
        internal int    SampleCount { get; private set; }

        /// <summary>
        /// El instante interpolado del **cruce del umbral**, o `null` cuando no hay dato. Se expone
        /// —no se recalcula fuera— para que el **corte de potencia** (`Helpers/PowerCut`) sitúe su
        /// marca con el mismo cruce que este tiempo: dos interpolaciones distintas del mismo suceso
        /// darían dos números que no cuadrarían entre sí.
        /// </summary>
        internal DateTime? ThresholdUtc { get; private set; }

        /// <summary>El instante interpolado del **contacto** (punto medio del salto a tierra).</summary>
        internal DateTime? TouchdownUtc { get; private set; }

        internal static ThresholdToTouchdownResult No(ThresholdToTouchdownStatus status,
                                                      int sampleCount, double medianSec)
            => new ThresholdToTouchdownResult
            {
                HasValue = false,
                Seconds  = double.NaN,
                Status   = status,
                SampleCount = sampleCount,
                MedianSampleIntervalSec = medianSec,
            };

        internal static ThresholdToTouchdownResult Value(double seconds, int sampleCount, double medianSec,
                                                         DateTime thresholdUtc, DateTime touchdownUtc)
            => new ThresholdToTouchdownResult
            {
                HasValue = true,
                Seconds  = seconds,
                Status   = ThresholdToTouchdownStatus.Ok,
                SampleCount = sampleCount,
                MedianSampleIntervalSec = medianSec,
                ThresholdUtc = thresholdUtc,
                TouchdownUtc = touchdownUtc,
            };
    }

    /// <summary>
    /// **El tiempo desde el cruce del umbral hasta el contacto**, la pregunta del piloto.
    ///
    /// Se calcula sobre la traza **fina** del flare (`flare_track`, 10 Hz), que es la única que
    /// sostiene el dato: a 0,1 s por muestra las dos interpolaciones son las que hacen que el número
    /// valga. Función pura, sin WinForms y sin base de datos, como `GeoMath` o
    /// `TouchdownCloseupGeometry`, para poder fijarla con tests.
    ///
    /// **El signo de `DistFt` es el de la traza**: positivo antes del umbral y negativo después (el
    /// mismo que documenta <see cref="Models.FlareTrackPoint.DistFt"/>). Aquí no se da la vuelta a
    /// nada: el cruce es el salto de `≥ 0` a `< 0` y el contacto es el salto de `on_ground` falso a
    /// verdadero.
    ///
    /// **Se interpola, no se coge la muestra más cercana.** A 150 kt cada muestra de 0,1 s son
    /// **25 ft**, así que quedarse con la muestra anterior o posterior al umbral metería hasta una
    /// décima de segundo de error en cada extremo; interpolando, el error baja a la fracción de
    /// intervalo que se recorre de verdad. El contacto es un salto booleano —la traza no dice en qué
    /// punto del intervalo tocó— y ahí la interpolación es el **punto medio**: el error máximo es
    /// medio intervalo, 0,05 s, que cae por debajo del redondeo de 0,1 s que se publica.
    ///
    /// **Degrada sin datos**, en los cuatro casos del enumerado, y **no publica la traza de 2 s**:
    /// ver <see cref="ThresholdToTouchdownStatus.CoarseTrack"/>.
    /// </summary>
    internal static class ThresholdToTouchdown
    {
        /// <summary>
        /// **Hasta qué cadencia se publica el dato: 0,5 s.** La traza del flare es de 0,1 s y la de
        /// aproximación de 2 s (2,25 s medidos), así que el corte no es discutible por ninguno de los
        /// dos lados; se deja en medio segundo porque pasado ese punto la propia interpolación ya
        /// arrastra ±0,25 s, más de lo que promete el redondeo de 0,1 s. Cualquier traza más lenta se
        /// señala como gruesa en vez de calcularse.
        /// </summary>
        internal const double MaxPublishableSampleIntervalSec = 0.5;

        /// <summary>Prefijo con el que el dato viaja en el `notes` del PIREP: `THR-TD 6.8s`.</summary>
        internal const string PirepPrefix = "THR-TD";

        /// <summary>
        /// **La decisión.** El orden importa: primero se descarta lo que no sirve (sin traza, traza
        /// gruesa), después se busca el cruce y por último el contacto, de forma que el motivo que se
        /// devuelve es el primero que de verdad impide el cálculo.
        ///
        /// La lista se recorre **en el orden en que llega**, que es el de captura: el buffer en vuelo
        /// lo guarda así y `LandingLogService.GetFlareTrack` lo lee con `ORDER BY seq_no`. No se
        /// reordena aquí, porque reordenar por un instante que puede faltar sería inventarse el orden.
        /// </summary>
        internal static ThresholdToTouchdownResult Compute(IList<FlareTrackPoint> samples)
        {
            // Muestra utilizable = con instante (para interpolar) y con distancia (para situarla
            // respecto al umbral). Un instante sin inicializar o una distancia no finita no se
            // pueden interpolar; se descartan en vez de arrastrar medio dato.
            var usable = new List<FlareTrackPoint>();
            if (samples != null)
            {
                foreach (var s in samples)
                {
                    if (s == null) continue;
                    if (s.TimestampUtc == default(DateTime)) continue;
                    if (double.IsNaN(s.DistFt) || double.IsInfinity(s.DistFt)) continue;
                    usable.Add(s);
                }
            }

            // Con una sola muestra no hay intervalo que interpolar: ni cruce ni contacto.
            if (usable.Count < 2)
                return ThresholdToTouchdownResult.No(ThresholdToTouchdownStatus.NoTrack,
                                                     usable.Count, double.NaN);

            double median = MedianIntervalSec(usable);

            // La traza gruesa no se publica: se señala. Va **antes** del cálculo a propósito —si se
            // calculara primero, el número de 2 s saldría con la misma cara de precisión que el de
            // 10 Hz—.
            if (double.IsNaN(median) || median > MaxPublishableSampleIntervalSec)
                return ThresholdToTouchdownResult.No(ThresholdToTouchdownStatus.CoarseTrack,
                                                     usable.Count, median);

            // ── Cruce del umbral: la última muestra antes y la primera después ──────
            int before = -1;
            for (int i = 0; i < usable.Count - 1; i++)
            {
                if (usable[i].DistFt >= 0.0 && usable[i + 1].DistFt < 0.0) { before = i; break; }
            }
            if (before < 0)
                return ThresholdToTouchdownResult.No(ThresholdToTouchdownStatus.NoThresholdCrossing,
                                                     usable.Count, median);

            var a = usable[before];
            var b = usable[before + 1];
            // Fracción del intervalo que falta para el cero: con `a.DistFt = 0` da 0 y el cruce cae
            // justo en esa muestra.
            double fraction = a.DistFt / (a.DistFt - b.DistFt);
            DateTime crossingUtc = Interpolate(a.TimestampUtc, b.TimestampUtc, fraction);

            // ── Contacto: la última muestra en el aire y la primera en tierra ───────
            int airborne = -1;
            for (int i = 1; i < usable.Count; i++)
            {
                if (!usable[i - 1].OnGround && usable[i].OnGround) { airborne = i - 1; break; }
            }
            if (airborne < 0)
                return ThresholdToTouchdownResult.No(ThresholdToTouchdownStatus.NoGroundTransition,
                                                     usable.Count, median);

            var air = usable[airborne];
            var ground = usable[airborne + 1];

            // Un contacto anterior al cruce no es «un tiempo corto»: es una traza incoherente (el
            // avión no puede tocar antes de llegar al umbral). Se degrada sin inventar un cero.
            if (ground.TimestampUtc <= crossingUtc)
                return ThresholdToTouchdownResult.No(ThresholdToTouchdownStatus.NoGroundTransition,
                                                     usable.Count, median);

            DateTime contactUtc = Interpolate(air.TimestampUtc, ground.TimestampUtc, 0.5);

            double seconds = (contactUtc - crossingUtc).TotalSeconds;
            if (seconds <= 0.0 || double.IsNaN(seconds) || double.IsInfinity(seconds))
                return ThresholdToTouchdownResult.No(ThresholdToTouchdownStatus.NoGroundTransition,
                                                     usable.Count, median);

            // Redondeo a 0,1 s, la resolución que el muestreo de 10 Hz puede sostener de verdad.
            double rounded = Math.Round(seconds, 1, MidpointRounding.AwayFromZero);
            return ThresholdToTouchdownResult.Value(rounded, usable.Count, median, crossingUtc, contactUtc);
        }

        /// <summary>
        /// La cadencia **mediana** de la traza, en segundos. Es la mediana y no la media para que un
        /// hueco suelto —un ciclo perdido— no convierta una traza de 10 Hz en gruesa. `NaN` si no hay
        /// ni un intervalo positivo que medir.
        /// </summary>
        private static double MedianIntervalSec(IList<FlareTrackPoint> usable)
        {
            var steps = new List<double>();
            for (int i = 1; i < usable.Count; i++)
            {
                double dt = (usable[i].TimestampUtc - usable[i - 1].TimestampUtc).TotalSeconds;
                if (dt > 0.0) steps.Add(dt);
            }
            if (steps.Count == 0) return double.NaN;

            steps.Sort();
            int middle = steps.Count / 2;
            return steps.Count % 2 == 1
                ? steps[middle]
                : (steps[middle - 1] + steps[middle]) / 2.0;
        }

        /// <summary>El instante que cae en <paramref name="fraction"/> del intervalo, 0…1.</summary>
        private static DateTime Interpolate(DateTime fromUtc, DateTime toUtc, double fraction)
            => fromUtc.AddSeconds((toUtc - fromUtc).TotalSeconds * fraction);

        /// <summary>
        /// **El dato tal como viaja al `notes` del PIREP**: `THR-TD 6.8s`, en cultura invariante
        /// —el `notes` es texto para la aerolínea, no para la pantalla del piloto, y ahí el separador
        /// decimal no puede depender del idioma—. `null` cuando no hay dato, para que el llamante no
        /// concatene nada.
        /// </summary>
        internal static string PirepSuffix(ThresholdToTouchdownResult result)
        {
            if (result == null || !result.HasValue) return null;
            return string.Format(CultureInfo.InvariantCulture, "{0} {1:0.0}s",
                                 PirepPrefix, result.Seconds);
        }

        /// <summary>El valor con su unidad, para la consola y los tests: `6.8 s` (invariante).</summary>
        internal static string FormatSeconds(double seconds)
            => string.Format(CultureInfo.InvariantCulture, "{0:0.0} s", seconds);
    }
}
