using System.Collections.Generic;

namespace vmsOpenAcars.Models
{
    /// <summary>
    /// **El cuerpo del `POST /api/pireps/{id}/landing`**, tal cual lo acordamos con phpVMS
    /// (`Docs/PEDIDO-PHPVMS-ANALISIS-FLARE.md`) y como lo aceptaron ellos el 11/10/2026.
    ///
    /// **Los nombres van en minúscula y con guion bajo porque son el contrato JSON**, no estilo
    /// C#: es la convención que ya siguen los demás DTO de la API del proyecto (`AcarsPosition`),
    /// y así el serializador no necesita un atributo por campo —que es donde se cuela un
    /// `[JsonProperty]` olvidado y el campo viaja con el nombre equivocado sin que nada falle—.
    ///
    /// **Este objeto se serializa con `JsonConvert.SerializeObject` y su configuración por
    /// defecto, que es la que emite los `null` explícitos.** Es deliberado y lo pedían ellos: un
    /// campo sin dato tiene que llegar como `null`, nunca omitido y nunca como `0`. Por eso **no**
    /// se pone `NullValueHandling.Ignore` en ningún sitio de este camino.
    /// </summary>
    internal class LandingUpload
    {
        /// <summary>Versión del esquema. Obligatorio, entero 1–100 (el 1 es el único que existe).</summary>
        public int schema_version { get; set; }

        /// <summary>
        /// Quién lo envía, con su versión: `vmsOpenACars/0.9.37`. Máximo 64 caracteres. La versión
        /// sale de `AppInfo.Version` —que prefiere el atributo **informativo**— y **no de una
        /// cadena fija**: una cadena fija miente en el release siguiente sin que nadie lo note.
        /// </summary>
        public string source_name { get; set; }

        /// <summary>Las métricas del aterrizaje. Van en columnas en su lado: son lo agregable.</summary>
        public LandingUploadMetrics metrics { get; set; }

        /// <summary>
        /// La traza fina del flare, **verbatim**: ellos la guardan tal cual, así que añadir un campo
        /// aquí no exige que ellos migren nada. Lista vacía si el vuelo no dejó muestras (las
        /// métricas se mandan igual: son útiles sin traza).
        /// </summary>
        public List<LandingUploadSample> trace { get; set; }
    }

    /// <summary>
    /// Las métricas del aterrizaje. Tipos **exactos** del contrato: `landing_rate_fpm`, `score` y
    /// `flaps_index` son **enteros** (su validación rechaza decimales), y el resto `float`.
    /// </summary>
    internal class LandingUploadMetrics
    {
        public int?    landing_rate_fpm  { get; set; }
        public double? g_force           { get; set; }
        public double? touchdown_dist_ft { get; set; }
        public double? centerline_dev_ft { get; set; }
        public int?    score             { get; set; }
        public string  runway_name       { get; set; }
        public double? runway_length_ft  { get; set; }
        public double? runway_true_deg   { get; set; }
        public double? landing_weight_lbs{ get; set; }
        public string  aircraft_icao     { get; set; }
        public string  aircraft_title    { get; set; }
        public string  aircraft_model    { get; set; }
        public string  flaps_label       { get; set; }
        public double? flaps_pct         { get; set; }
        public int?    flaps_index       { get; set; }
        public double? power_cut_sec     { get; set; }
        public bool?   flare_capture_armed { get; set; }
        public string  landing_metar     { get; set; }
        public double? wind_dir_deg      { get; set; }
        public double? wind_speed_kt     { get; set; }
        public double? wind_gust_kt      { get; set; }
        public double? headwind_kt       { get; set; }
        public double? crosswind_kt      { get; set; }
    }

    /// <summary>
    /// **Una muestra de la traza**, con los 17 campos del contrato.
    ///
    /// `ts_utc` es **cadena ISO-8601 en UTC** y no un `DateTime`: así el formato no depende de la
    /// cultura del equipo del piloto ni de cómo el serializador decida escribir el huso, que es la
    /// clase de detalle que hace que un servidor interprete una hora local como UTC.
    ///
    /// `dist_ft` y `on_ground` son los **dos únicos campos no anulables** de la muestra (en
    /// `FlareTrackPoint` son `double` y `bool`): siempre viajan con valor, y así se lo hemos dicho a
    /// phpVMS. El resto es anulable a propósito —`null` es «sin dato», y un 0 en el radioaltímetro
    /// sería un avión en el suelo—.
    /// </summary>
    internal class LandingUploadSample
    {
        public string  ts_utc       { get; set; }
        public double  dist_ft      { get; set; }
        public double? agl_ft       { get; set; }
        public double? radar_alt_ft { get; set; }
        public double? ias_kt       { get; set; }
        public double? vs_fpm       { get; set; }
        public double? pitch_deg    { get; set; }
        public double? bank_deg     { get; set; }
        public double? gs_kt        { get; set; }
        public double? n1_1_pct     { get; set; }
        public double? n1_2_pct     { get; set; }
        public double? n2_1_pct     { get; set; }
        public double? n2_2_pct     { get; set; }
        public double? flaps_pct    { get; set; }
        public int?    flaps_index  { get; set; }
        public bool?   spoilers     { get; set; }
        public bool    on_ground    { get; set; }
    }
}
