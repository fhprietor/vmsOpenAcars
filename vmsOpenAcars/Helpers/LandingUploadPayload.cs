using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json;
using vmsOpenAcars.Models;

namespace vmsOpenAcars.Helpers
{
    /// <summary>
    /// **Convierte lo que el cliente ya tiene guardado en el cuerpo del `POST /pireps/{id}/landing`.**
    ///
    /// Puro a propósito —sin red, sin WinForms, sin estado— porque es donde vive la regla de qué es
    /// un dato y qué es un hueco, y esa regla se prueba con los números de un vuelo real.
    ///
    /// **Las tres reglas que aplica, y por qué:**
    ///
    /// 1. **`null` es «sin dato», nunca `0`.** Es lo que phpVMS necesita para que una mediana no se
    ///    envenene con ceros falsos, y es la regla que este repo ya sigue en todas partes. Aquí eso
    ///    obliga a traducir dos cosas del modelo local:
    ///    - el centinela **`ScoringService.NoLandingData` (-1)** de `LandingRateFpm`, que significa
    ///      «no se capturó ningún toque». Mandarlo tal cual metería un **-1** en su agregación como
    ///      si fuera una tasa real, y un `0` se leería como un aterrizaje perfecto.
    ///    - los **ceros** de `GForce` y `CenterlineDevFt` en un vuelo **con** toque: la puntuación
    ///      los trata como ausencia de dato (`ScoringService` omite el G «si dato = 0» y activa el
    ///      criterio de eje «si `CenterlineDeviationFt > 0`»), así que aquí también son `null`.
    ///      **`touchdown_dist_ft` no entra en esa lista**: tocar justo en el umbral es `0` de verdad
    ///      y ahí el 0 es una medida, no un hueco.
    /// 2. **Nada que no sea finito se cuela como número.** Un `NaN` o un `Infinity` —que
    ///    `FlareTrackPoint` puede tener, y de hecho `FlapTrackSummary` los filtra explícitamente—
    ///    serializan como `NaN` / `Infinity`, que **no son JSON válido**: su validador rechazaría el
    ///    payload entero y perderíamos la traza completa por una muestra. Se convierten en `null`.
    /// 3. **Se respetan sus topes de longitud**, recortando en cliente. No es cosmético: su
    ///    validación es **todo o nada**, así que un METAR de 420 caracteres no perdería el METAR —
    ///    perdería **el vuelo entero**, traza incluida—. Recortar es el mal menor y se dice.
    /// </summary>
    internal static class LandingUploadPayload
    {
        /// <summary>La única versión de esquema que existe hoy. Entero 1–100 en su validación.</summary>
        internal const int SchemaVersion = 1;

        // Topes de longitud de su validación (`StoreLandingRequest`). Si alguno cambia allí, aquí
        // solo se recorta de más: el campo sigue llegando, entero hasta ese tope.
        internal const int MaxSourceName    = 64;
        internal const int MaxRunwayName    = 16;
        internal const int MaxAircraftIcao  = 8;
        internal const int MaxAircraftTitle = 191;
        internal const int MaxAircraftModel = 64;
        internal const int MaxFlapsLabel    = 32;
        internal const int MaxMetar         = 400;

        /// <summary>
        /// Construye el cuerpo completo. **Siempre el análisis entero**, nunca parcial: su ingesta
        /// es un reemplazo total (un reenvío con menos campos deja el resto en `null`), así que no
        /// existe el envío incremental.
        /// </summary>
        /// <param name="record">El registro del aterrizaje, ya con `Score` y `FlareCaptureArmed`.</param>
        /// <param name="trace">Las muestras de la traza fina, ya copiadas del buffer en vivo.</param>
        /// <param name="appVersion">`AppInfo.Version` — se compone como `vmsOpenACars/{versión}`.</param>
        internal static LandingUpload Build(FlightRecord record,
                                            IList<FlareTrackPoint> trace,
                                            string appVersion)
        {
            if (record == null) throw new ArgumentNullException(nameof(record));

            var samples = BuildTrace(trace);

            // Los flaps y el corte de potencia salen de los helpers que ya los deciden para el
            // gráfico y el PIREP: no se reimplementa ninguna regla aquí.
            var flaps = FlapTrackSummary.Compute(trace, record.AircraftIcao);
            var power = PowerCut.Compute(trace);

            bool touchdownCaptured = record.LandingRateFpm != Services.ScoringService.NoLandingData;

            // Los cuatro datos que solo existen si el avión llegó a tocar. Se resuelven antes de
            // componer el objeto para que la regla se lea de un tirón y no dentro de un ternario.
            int?    landingRate  = null;
            double? gForce       = null;
            double? touchdownFt  = null;
            double? centerlineFt = null;
            if (touchdownCaptured)
            {
                landingRate  = record.LandingRateFpm;
                // `> 0` porque la puntuación omite el criterio de G «si dato = 0»: en este modelo un
                // 0 no es una medida de G, es que no hay medida.
                gForce       = PositiveOrNull(record.GForce);
                // Aquí el 0 **sí** es una medida: tocar justo en el umbral.
                touchdownFt  = FiniteOrNull(record.TouchdownDistFt);
                // El eje usa el mismo guard que `ScoringService` (`> 0`): un 0 es «no medido».
                centerlineFt = record.CenterlineDevFt > 0 ? FiniteOrNull(record.CenterlineDevFt) : null;
            }

            return new LandingUpload
            {
                schema_version = SchemaVersion,
                source_name    = Clip("vmsOpenACars/" + (appVersion ?? ""), MaxSourceName),
                metrics = new LandingUploadMetrics
                {
                    landing_rate_fpm  = landingRate,
                    g_force           = gForce,
                    touchdown_dist_ft = touchdownFt,
                    centerline_dev_ft = centerlineFt,

                    score = record.Score,
                    runway_name      = ClipOrNull(record.RunwayName, MaxRunwayName),
                    runway_length_ft = FiniteOrNull(record.RunwayLengthFt),
                    runway_true_deg  = FiniteOrNull(record.LandingRunwayTrueDeg),
                    landing_weight_lbs = FiniteOrNull(record.LandingWeightLbs),

                    aircraft_icao  = ClipOrNull(record.AircraftIcao,  MaxAircraftIcao),
                    aircraft_title = ClipOrNull(record.AircraftTitle, MaxAircraftTitle),
                    aircraft_model = ClipOrNull(record.AircraftModel, MaxAircraftModel),

                    // El ajuste **en la toma**, que es el que explica el flotado, y su detente real
                    // (sin `≈`) cuando el avión lo publica. Sin muestra en la transición aire→tierra,
                    // los tres van a `null`: no se hereda el del umbral.
                    flaps_label = ClipOrNull(flaps.AtTouchdown?.Label, MaxFlapsLabel),
                    flaps_pct   = flaps.AtTouchdown != null && flaps.AtTouchdown.HasValue
                                      ? FiniteOrNull(flaps.AtTouchdown.Percent)
                                      : null,
                    flaps_index = flaps.AtTouchdownIndex,

                    // Sin valor, `null`: el helper distingue «no cortó» de «no se pudo medir», y lo
                    // que no se pudo medir no se publica como 0 s.
                    power_cut_sec = power.HasValue ? FiniteOrNull(power.SecondsBeforeTouchdown) : null,

                    flare_capture_armed = record.FlareCaptureArmed,
                    landing_metar       = ClipOrNull(record.MetarRaw, MaxMetar),

                    wind_dir_deg   = FiniteOrNull(record.LandingWindDirDeg),
                    wind_speed_kt  = FiniteOrNull(record.LandingWindSpeedKt),
                    wind_gust_kt   = FiniteOrNull(record.LandingWindGustKt),
                    headwind_kt    = FiniteOrNull(record.LandingHeadwindKt),
                    crosswind_kt   = FiniteOrNull(record.LandingCrosswindKt),
                },
                trace = samples,
            };
        }

        /// <summary>
        /// Serializa el cuerpo. **Con la configuración por defecto de Newtonsoft, que emite los
        /// `null` explícitos** —que es justo lo que ellos piden—. Poner aquí
        /// `NullValueHandling.Ignore` convertiría cada hueco en un campo ausente: hoy lo tratan
        /// igual, pero dejaría de estar escrito en el cuerpo lo que de verdad no se midió.
        /// </summary>
        internal static string ToJson(LandingUpload payload)
        {
            return JsonConvert.SerializeObject(payload);
        }

        private static List<LandingUploadSample> BuildTrace(IList<FlareTrackPoint> trace)
        {
            var list = new List<LandingUploadSample>();
            if (trace == null) return list;

            foreach (var s in trace)
            {
                if (s == null) continue;
                list.Add(new LandingUploadSample
                {
                    ts_utc       = ToUtcIso(s.TimestampUtc),
                    // Los dos no anulables del contrato: siempre con valor.
                    dist_ft      = Finite(s.DistFt) ?? 0.0,
                    on_ground    = s.OnGround,
                    agl_ft       = FiniteOrNull(s.AglFt),
                    radar_alt_ft = FiniteOrNull(s.RadarAltFt),
                    ias_kt       = FiniteOrNull(s.IasKt),
                    vs_fpm       = FiniteOrNull(s.VsFpm),
                    pitch_deg    = FiniteOrNull(s.PitchDeg),
                    bank_deg     = FiniteOrNull(s.BankDeg),
                    gs_kt        = FiniteOrNull(s.GsKt),
                    n1_1_pct     = FiniteOrNull(s.Eng1Pct),
                    n1_2_pct     = FiniteOrNull(s.Eng2Pct),
                    n2_1_pct     = FiniteOrNull(s.Eng1N2Pct),
                    n2_2_pct     = FiniteOrNull(s.Eng2N2Pct),
                    flaps_pct    = FiniteOrNull(s.FlapsPct),
                    flaps_index  = s.FlapsIndex,
                    spoilers     = s.SpoilersDeployed,
                });
            }
            return list;
        }

        /// <summary>
        /// Instante de la muestra en ISO-8601 UTC, con la `Z` explícita.
        ///
        /// Se compone en vez de dejar que el serializador decida: el formato de un `DateTime` en
        /// JSON depende de su `Kind`, y un `Unspecified` viajaría **sin huso**, que un servidor
        /// razonablemente leería como su hora local y colocaría el flare a las tantas. Un
        /// `Unspecified` se interpreta como UTC porque es lo que nuestras marcas son
        /// (`DateTime.UtcNow` en toda la telemetría); convertirlo desde «local» lo desplazaría.
        /// </summary>
        private static string ToUtcIso(DateTime value)
        {
            var utc = value.Kind == DateTimeKind.Utc
                          ? value
                          : DateTime.SpecifyKind(value, DateTimeKind.Utc);
            return utc.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);
        }

        /// <summary>Un `double` que no sea finito no es un dato: es un hueco que rompería el JSON.</summary>
        private static double? FiniteOrNull(double? value) => value.HasValue ? Finite(value.Value) : null;

        /// <summary>
        /// El valor **solo si es una medida positiva**. Para los campos donde el 0 del modelo local
        /// significa «no medido» (`GForce`), publicarlo sería inventar una medida.
        /// </summary>
        private static double? PositiveOrNull(double value)
        {
            var finite = Finite(value);
            return finite.HasValue && finite.Value > 0 ? finite : null;
        }

        private static double? Finite(double value)
            => double.IsNaN(value) || double.IsInfinity(value) ? (double?)null : value;

        /// <summary>Recorta al tope del contrato. Vacío o solo espacios es «sin dato», no `""`.</summary>
        private static string ClipOrNull(string value, int max)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            return Clip(value.Trim(), max);
        }

        private static string Clip(string value, int max)
            => value.Length <= max ? value : value.Substring(0, max);
    }
}
