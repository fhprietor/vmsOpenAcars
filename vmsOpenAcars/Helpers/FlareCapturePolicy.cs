using System;
using System.Collections.Generic;
using System.Globalization;

namespace vmsOpenAcars.Helpers
{
    /// <summary>Qué toca hacer en este ciclo de telemetría con la traza del flare.</summary>
    internal enum FlareCaptureAction
    {
        /// <summary>Nada: ni se captura ni hay que cambiar de estado.</summary>
        None,

        /// <summary>Se arma la captura en este ciclo (y esta muestra es la primera).</summary>
        Start,

        /// <summary>Ya está armada y sigue viva: esta muestra se guarda.</summary>
        Keep,

        /// <summary>Se desarma en este ciclo. La muestra de este ciclo **no** se guarda.</summary>
        Stop,
    }

    /// <summary>La decisión de un ciclo, con el porqué para el log y para los tests.</summary>
    internal struct FlareCaptureDecision
    {
        internal FlareCaptureAction Action { get; set; }

        /// <summary>Motivo en texto (`armed:threshold`, `stopped:on-ground`…). Vacío sin cambio.</summary>
        internal string Reason { get; set; }

        /// <summary>Contador de muestras después de aplicar la decisión.</summary>
        internal int SampleCount { get; set; }

        /// <summary>¿Hay que guardar la muestra de este ciclo?</summary>
        internal bool KeepSampling =>
            Action == FlareCaptureAction.Start || Action == FlareCaptureAction.Keep;

        /// <summary>La captura ha terminado: no vuelve a armarse en este aterrizaje.</summary>
        internal bool Sealed { get; set; }
    }

    /// <summary>
    /// **Cuándo se arma y se desarma la traza fina del flare**, y cuántas muestras se guardan.
    ///
    /// Función pura con estado, sin red ni WinForms, para poder fijarla con tests. La regla nace de
    /// dos medidas hechas sobre la base local (`F:\FS\vmsOpenAcars\db\landing_log.sqlite`, 41 vuelos
    /// con `approach_track`):
    ///
    /// - El muestreo de hoy es de **2 s nominales, 2,25 s medidos** (mediana del paso reconstruido
    ///   con la distancia y la velocidad de cada par de muestras). A 148 kt de IAS media en final son
    ///   **≈ 550 ft por muestra**: en los últimos 1 500 ft al umbral solo caben **6 muestras**
    ///   (mediana del corpus; 0 en los vuelos sin captura de traza y hasta 109 en el vuelo 5, que es
    ///   un caso de muestreo denso por reinicios de la captura).
    /// - A 1 500 ft del umbral el avión está a **107 ft AGL de mediana** (mín 0, máx 305) con un
    ///   descenso de **−850 fpm**: quedan **~3,3 s** hasta el toque. El flare real (los ~50 ft
    ///   finales) vive dentro de ese tramo.
    ///
    /// De ahí la elección: **se arma a 1 500 ft del umbral**, que da unos 6 s de captura a 10 Hz
    /// (~60 muestras) y cubre el flare entero sin arrastrar medio descenso. El respaldo de **300 ft
    /// AGL** existe para cuando la geometría del umbral no está disponible (sin pista resuelta, sin
    /// NavData) y **solo decide si no hay dato de distancia**: con 1 000 ft AGL el vuelo 41 habría
    /// empezado a capturar a 1 653,8 ft del umbral, antes del propio umbral de armado.
    ///
    /// **Nada de esto toca el ritmo global del vuelo**: la captura solo existe entre esos dos hitos.
    /// </summary>
    internal sealed class FlareCapturePolicy
    {
        // ─── Los umbrales ─────────────────────────────────────────────────────────

        /// <summary>Distancia al umbral que arma la captura: **1 500 ft**.</summary>
        internal const double ArmDistanceFt = 1500.0;

        /// <summary>
        /// Respaldo por altura: **300 ft AGL**. Solo entra cuando **no hay** dato de distancia al
        /// umbral (sin pista resuelta), y a 850 fpm de descenso —la mediana del corpus— son unos 20 s
        /// antes del toque.
        ///
        /// **No puede ser un armado alternativo**: con 1 000 ft AGL (el umbral del estabilizado) el
        /// vuelo 41 habría empezado a capturar a **1653,8 ft del umbral**, es decir *antes* de que la
        /// distancia entrara en los 1 500 —la captura se habría abierto por la puerta de atrás—. Aquí
        /// la distancia **manda**: si existe, la altura no decide nada.
        ///
        /// Y no puede ser un umbral alto: como el armado solo se evalúa dentro de la fase de
        /// aproximación, un avión a 3 000 ft AGL en final lo dispararía. 300 ft es el tramo que de
        /// verdad interesa —el flare— y no deja la captura abierta medio descenso.
        /// </summary>
        internal const double ArmAglFt = 300.0;

        /// <summary>
        /// Segundos de captura **después** del toque. El toque se detecta en el ciclo en que el
        /// avión pasa a tierra, así que las muestras posteriores son la frenada y el morro bajando:
        /// dos segundos son suficientes para ver el final de la maniobra.
        /// </summary>
        internal const double PostTouchdownSec = 2.0;

        /// <summary>
        /// **Cap de muestras**: 400, el tope duro de memoria. A 10 Hz son 40 s de vuelo. Si la
        /// captura se abre con el respaldo de AGL en un avión que desciende muy plano, el cap corta
        /// por lo más viejo y se conserva el final —que es lo que se mira—; y con el tope, el hilo
        /// de telemetría nunca puede crecer sin límite.
        /// </summary>
        internal const int MaxSamples = 400;

        /// <summary>
        /// Segundos de captura sin llegar al toque. **No es una decisión de producto, es un seguro**:
        /// un go-around deja la fase en `Approach` y, sin él, la captura seguiría abierta todo el
        /// descenso. Al llegar aquí se cierra y **no se vuelve a armar** en ese aterrizaje.
        /// </summary>
        internal const double MaxCaptureSec = 45.0;

        private FlareCaptureDecision _last = new FlareCaptureDecision { Action = FlareCaptureAction.None };

        /// <summary>Ya había empezado a capturar (los ticks siguientes con captura viva lo llevan a 1).</summary>
        internal bool EverStarted { get; private set; }

        /// <summary>La captura está viva ahora mismo.</summary>
        internal bool Capturing { get; private set; }

        /// <summary>La captura terminó para este aterrizaje y no se rearma.</summary>
        internal bool Sealed { get; private set; }

        internal int SampleCount { get; private set; }

        internal DateTime CaptureStartUtc { get; private set; }

        internal FlareCaptureDecision LastDecision => _last;

        /// <summary>Vuelve al estado inicial. Se llama al empezar un vuelo.</summary>
        internal void Reset()
        {
            EverStarted   = false;
            Capturing     = false;
            Sealed        = false;
            SampleCount   = 0;
            CaptureStartUtc = DateTime.MinValue;
            _last = new FlareCaptureDecision { Action = FlareCaptureAction.None };
        }

        /// <summary>
        /// **La decisión del ciclo.** Se evalúa en orden de prioridad: primero el cierre (que es lo
        /// que no puede esperar), después el armado.
        /// </summary>
        /// <param name="nowUtc">Reloj del ciclo.</param>
        /// <param name="aglFt">Altura sobre el terreno; `null` sin dato (no bloquea).</param>
        /// <param name="distToThresholdFt">Distancia al umbral en pies, positiva antes de él; `null` sin dato.</param>
        /// <param name="onGround">El avión está en tierra.</param>
        /// <param name="touchdownUtc">Instante del toque si ya se detectó; `null` si no.</param>
        /// <param name="inLandingPhase">La fase del vuelo es de aterrizaje (Approach/Landing/…).</param>
        internal FlareCaptureDecision Update(
            DateTime nowUtc,
            double?  aglFt,
            double?  distToThresholdFt,
            bool     onGround,
            DateTime? touchdownUtc,
            bool     inLandingPhase)
        {
            int before = SampleCount;

            // ── Cierre ────────────────────────────────────────────────────────────
            if (Capturing)
            {
                if (touchdownUtc.HasValue)
                {
                    // Con el toque detectado manda **el toque**: se captura el margen posterior (la
                    // frenada y el morro bajando) y ahí se cierra. La comprobación «en tierra y
                    // pasado el umbral» queda de respaldo **solo** para cuando esa detección no
                    // llega —y si mandara siempre, el margen no existiría nunca, porque en el mismo
                    // ciclo del toque ya se cumple—.
                    if ((nowUtc - touchdownUtc.Value).TotalSeconds >= PostTouchdownSec)
                        return Finish(FlareCaptureAction.Stop,
                            string.Format(CultureInfo.InvariantCulture,
                                "stopped:post-touchdown {0:F1}s",
                                (nowUtc - touchdownUtc.Value).TotalSeconds));
                }
                else if (onGround && distToThresholdFt.HasValue && distToThresholdFt.Value <= 0.0)
                {
                    // Toma suave, con el VS por encima del umbral del detector de toque: tierra y ya
                    // pasado el umbral es el mismo dato que mira la captura, no una suposición nueva.
                    return Finish(FlareCaptureAction.Stop, "stopped:on-ground");
                }

                if (SampleCount >= MaxSamples)
                    return Finish(FlareCaptureAction.Stop, "stopped:cap");

                // La muestra se cuenta **antes** de decidir si se guarda, así el tope es exacto: el
                // cap de 400 significa 400 muestras en la base, no 401 (el `Update` se llama en cada
                // ciclo, incluido aquel en que la captura se cierra).
                SampleCount = before + 1;

                if (CaptureStartUtc != DateTime.MinValue &&
                    (nowUtc - CaptureStartUtc).TotalSeconds >= MaxCaptureSec)
                    return Finish(FlareCaptureAction.Stop, "stopped:timeout");

                _last = new FlareCaptureDecision
                {
                    Action = FlareCaptureAction.Keep, Reason = "", SampleCount = SampleCount,
                };
                return _last;
            }

            // ── Armado ────────────────────────────────────────────────────────────
            if (Sealed || EverStarted) return Quiet(before);
            if (!inLandingPhase)       return Quiet(before);

            string reason = ArmReason(aglFt, distToThresholdFt);
            if (reason == null) return Quiet(before);

            EverStarted     = true;
            Capturing       = true;
            CaptureStartUtc = nowUtc;
            SampleCount     = before + 1;
            _last = new FlareCaptureDecision
            {
                Action = FlareCaptureAction.Start, Reason = reason, SampleCount = SampleCount,
            };
            return _last;
        }

        /// <summary>
        /// Por qué se arma, o `null` si todavía no toca.
        ///
        /// **La distancia manda**: si hay dato de distancia al umbral, decide ella sola y la altura no
        /// entra. El respaldo de AGL es para cuando ese dato **no existe** (sin pista resuelta, sin
        /// NavData), y ahí sí es el único que puede decidir.
        /// </summary>
        private static string ArmReason(double? aglFt, double? distToThresholdFt)
        {
            if (distToThresholdFt.HasValue)
                return distToThresholdFt.Value <= ArmDistanceFt ? "armed:threshold" : null;

            if (aglFt.HasValue && aglFt.Value <= ArmAglFt)
                return "armed:agl";

            return null;
        }

        private FlareCaptureDecision Finish(FlareCaptureAction action, string reason)
        {
            Capturing = false;
            Sealed    = true;
            _last = new FlareCaptureDecision
            {
                Action = action, Reason = reason, SampleCount = SampleCount, Sealed = true,
            };
            return _last;
        }

        private FlareCaptureDecision Quiet(int count)
        {
            _last = new FlareCaptureDecision
            {
                Action = FlareCaptureAction.None, Reason = "", SampleCount = count,
            };
            return _last;
        }
    }

    /// <summary>
    /// **El almacén de las muestras del flare**, con tope.
    ///
    /// Lista y tope en el mismo sitio, con cerrojo, porque la escribe el hilo de telemetría y la lee
    /// —y la vacía— el hilo de UI al filear el PIREP. Es el mismo patrón que `_approachBuffer` en
    /// `TelemetryCoordinator`, que ya se corrompió una vez por tocarlo desde dos hilos (puntos
    /// perdidos y `IndexOutOfRange` dentro de `List.Add`).
    ///
    /// **Recorta por lo viejo, no por lo nuevo**: cuando se llega al tope se suelta la muestra más
    /// antigua. Lo que se viene a mirar es el final de la maniobra.
    /// </summary>
    internal sealed class FlareSampleBuffer
    {
        private readonly List<Models.FlareTrackPoint> _samples = new List<Models.FlareTrackPoint>();
        private readonly object _lock = new object();
        private readonly int _cap;

        internal FlareSampleBuffer(int cap)
        {
            _cap = cap > 0 ? cap : FlareCapturePolicy.MaxSamples;
        }

        internal int Count { get { lock (_lock) return _samples.Count; } }

        /// <summary>Añade una muestra y devuelve el número de muestras guardadas tras el recorte.</summary>
        internal int Append(Models.FlareTrackPoint point)
        {
            if (point == null) return Count;
            lock (_lock)
            {
                _samples.Add(point);
                // Mientras se pueda, se renumera para que `seq_no` describa lo que hay de verdad en
                // la tabla. Al llegar al tope la renumeración dejaría de ser 0…n, así que se mantiene
                // el contador de llegada: en la base queda un orden correcto aunque no sea correlativo.
                point.SeqNo = _samples.Count - 1;
                if (_samples.Count > _cap) _samples.RemoveAt(0);
                return _samples.Count;
            }
        }

        /// <summary>Copia para consumidores que necesitan iterar sin el cerrojo tomado.</summary>
        internal List<Models.FlareTrackPoint> Snapshot()
        {
            lock (_lock) return new List<Models.FlareTrackPoint>(_samples);
        }

        internal void Clear()
        {
            lock (_lock) _samples.Clear();
        }
    }
}
