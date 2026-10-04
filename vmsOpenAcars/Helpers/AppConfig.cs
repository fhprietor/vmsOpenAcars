using System.Configuration;

namespace vmsOpenAcars.Helpers
{
    public static class AppConfig
    {
        // Polling
        public static int PollingIntervalMs => GetInt("polling_interval_ms", 50);

        // Position update intervals by phase (seconds)
        public static int UpdateIntervalTaxi => GetInt("update_interval_taxi", 30);
        public static int UpdateIntervalTakeoff => GetInt("update_interval_takeoff", 5);
        public static int UpdateIntervalClimb => GetInt("update_interval_climb", 15);
        public static int UpdateIntervalCruise => GetInt("update_interval_cruise", 30);
        public static int UpdateIntervalDescent => GetInt("update_interval_descent", 15);
        public static int UpdateIntervalApproach => GetInt("update_interval_approach", 5);
        public static int UpdateIntervalOther => GetInt("update_interval_other", 30);

        // Event reporting
        public static bool ReportGearChanges => GetBool("report_gear_changes", true);
        public static bool ReportFlapChanges => GetBool("report_flap_changes", true);
        public static bool ReportSpoilerChanges => GetBool("report_spoiler_changes", true);
        public static bool ReportLightChanges => GetBool("report_light_changes", true);
        public static bool ReportEngineChanges => GetBool("report_engine_changes", true);

        // Fuel tolerances
        /// <summary>Tolerance as integer percentage 0–100 (e.g. 10 = 10%). App.config key: fuel_tolerance_percent.</summary>
        public static double FuelTolerancePercent => GetDouble("fuel_tolerance_percent", 10.0);
        public static double FuelToleranceAbsolute => GetDouble("fuel_tolerance_absolute", 50);

        // phpVMS API
        public static string VmsApiUrl => ConfigurationManager.AppSettings["vms_api_url"] ?? "";

        /// <summary>
        /// Clave del piloto en phpVMS. Es el IKM del que se deriva la clave del sobre de NavData, así
        /// que solo se pasa a la derivación: **no se registra ni se escribe en ningún sitio**.
        /// </summary>
        public static string VmsApiKey => ConfigurationManager.AppSettings["vms_api_key"] ?? "";

        // NavData API — sin valores por defecto: los proporciona la aerolínea virtual
        // y el piloto los introduce en Settings. Cualquier credencial embebida aquí
        // acabaría comiteada al repositorio.
        public static string NavDataApiUrl => ConfigurationManager.AppSettings["navdata_api_url"] ?? "";

        /// <summary>
        /// Base de NavData **efectiva**: la `url` que entrega el sobre de phpVMS **tal cual** —el
        /// contrato dice que es la base completa, no el host— y, solo si el sobre no trae `url` (o
        /// viene vacía), la `navdata_api_url` del `.config`. Los llamantes deben usar esta, no la cruda.
        /// </summary>
        public static string NavDataApiUrlEffective
        {
            get { return NavDataKeyPolicy.ResolveUrl(NavDataKeyState.Url, NavDataApiUrl); }
        }

        /// <summary>
        /// `navdata_api_key` **cruda del `.config`**. Es solo diagnóstico: **no se usa para volar**.
        /// Una clave escrita aquí se ignora (ver <see cref="NavDataApiKeyEffective"/>).
        /// </summary>
        public static string NavDataApiKey => ConfigurationManager.AppSettings["navdata_api_key"] ?? "";

        /// <summary>
        /// Clave de NavData **efectiva**: **solo** la que phpVMS entregó en el sobre cifrado y vive en
        /// memoria (<see cref="NavDataKeyState"/>). Si no hay sobre, **no hay clave** y punto.
        ///
        /// El porqué de que el respaldo se quitara (decisión del mantenedor): la `navdata_api_key` del
        /// `.config` **es la misma clave que se filtró** —viajaba en el `.config` que se descarga del
        /// gestor de ficheros—, así que mientras existiera el respaldo el cliente seguiría volando en
        /// silencio con la clave comprometida y **enmascararía** que el sobre nuevo se ha roto.
        /// No hay fallback: se informa del motivo y se vuela sin NavData.
        ///
        /// No reintroducir aquí una lectura de `navdata_api_key` ni como último recurso.
        /// </summary>
        public static string NavDataApiKeyEffective => NavDataKeyState.Key;

        /// <summary>
        /// Base del proxy de teselas de NavData. **Vacío por defecto**: se deriva de `navdata_api_url`
        /// + `tiles`, así que no hay que configurar nada. Solo se define a mano si el proxy vive en
        /// otro sitio, y entonces debe apuntar a la ruta de teselas.
        /// </summary>
        public static string TileProxyUrl => ConfigurationManager.AppSettings["tile_proxy_url"] ?? "";

        // X-Origin-Domain: se usa el valor explícito de `navdata_api_domain` si está
        // configurado; si no, se deriva del host de vms_api_url (habitualmente el mismo
        // dominio que sirve el servicio NavData).
        public static string NavDataApiDomain
        {
            get
            {
                string configured = ConfigurationManager.AppSettings["navdata_api_domain"];
                if (!string.IsNullOrWhiteSpace(configured)) return configured.Trim();
                try
                {
                    string url = VmsApiUrl;
                    return string.IsNullOrWhiteSpace(url) ? "" : new System.Uri(url).Host;
                }
                catch { return ""; }
            }
        }

        // CARTO basemaps — teselas del mapa. La clave **no es un secreto** (es un token publicable y
        // restringible por web), pero tampoco viaja en la plantilla de distribución: vive en
        // App.config, que es local. Sin ella el mapa se ve con la marca de agua «API key required».
        // Ojo: debe crearse **sin restricción de web** —una app de escritorio no envía `Referer` y
        // CARTO responde 403 a una clave restringida—.
        public static string CartoApiKey => ConfigurationManager.AppSettings["carto_api_key"] ?? "";

        // LittleNavMap database (deprecated — kept for settings migration)
        public static string LnmDbPath => ConfigurationManager.AppSettings["lnm_db_path"] ?? "";

        // Landing log database
        /// <summary>Full path to the landing-log SQLite database (e.g. landing_log.sqlite).</summary>
        public static string LandingLogPath => ConfigurationManager.AppSettings["landing_log_path"] ?? "";

        // OSD Overlay (backing fields allow live updates from SettingsForm)
        private static bool _osdEnabled = GetBool("osd_enabled", true);
        public static bool OsdEnabled
        {
            get => _osdEnabled;
            set => _osdEnabled = value;
        }

        private static bool _osdSoundEnabled = GetBool("osd_sound_enabled", true);
        public static bool OsdSoundEnabled
        {
            get => _osdSoundEnabled;
            set => _osdSoundEnabled = value;
        }

        // Cabin Announcements (backing fields allow live updates from SettingsForm)
        private static bool _cabinAnnouncementsEnabled = GetBool("cabin_announcements_enabled", true);
        public static bool CabinAnnouncementsEnabled
        {
            get => _cabinAnnouncementsEnabled;
            set => _cabinAnnouncementsEnabled = value;
        }

        private static int _cabinAnnouncementsVolume = GetInt("cabin_announcements_volume", 80);
        public static int CabinAnnouncementsVolume
        {
            get => _cabinAnnouncementsVolume;
            set => _cabinAnnouncementsVolume = value;
        }
        private static int _osdDurationSeconds = GetInt("osd_duration_seconds", 4);
        public static int OsdDurationSeconds
        {
            get => _osdDurationSeconds;
            set => _osdDurationSeconds = value;
        }

        public static int OsdScreenIndex => GetInt("osd_screen_index", 1);

        private static int _osdOpacity = GetInt("osd_opacity", 90);
        /// <summary>OSD opacity as integer percentage 10–100 (e.g. 90 = 90%).</summary>
        public static int OsdOpacity
        {
            get => _osdOpacity;
            set => _osdOpacity = value;
        }

        // El OSD de zonas restringidas (Prohibited/Restricted/Danger) es opcional: el aviso
        // es intrusivo por diseño —Crítico, con chiming— y hay pilotos que vuelan en zonas
        // con muchas áreas activas y prefieren seguirlas solo en el log y en el mapa. El log
        // NO se suprime con este ajuste: siempre queda el registro y el polígono en el mapa.
        private static bool _osdAirspaceAlerts = GetBool("osd_airspace_alerts", true);
        public static bool OsdAirspaceAlerts
        {
            get => _osdAirspaceAlerts;
            set => _osdAirspaceAlerts = value;
        }

        // ── RAAS: guía de rodaje giro a giro (v0.9.14) ────────────────────────────
        // `raas_enabled` decide si sale el popup de rodaje al encender la luz de taxi; el ajuste
        // fino (voz sí/no y volumen) se elige en ese popup y se recuerda para el próximo vuelo.
        private static bool _raasEnabled = GetBool("raas_enabled", true);
        public static bool RaasEnabled
        {
            get => _raasEnabled;
            set => _raasEnabled = value;
        }

        private static bool _raasVoiceEnabled = GetBool("raas_voice_enabled", true);
        public static bool RaasVoiceEnabled
        {
            get => _raasVoiceEnabled;
            set => _raasVoiceEnabled = value;
        }

        private static int _raasVolume = GetInt("raas_volume", 80);
        public static int RaasVolume
        {
            get => _raasVolume;
            set => _raasVolume = value;
        }

        // ── Observación de la ruta de rodaje PROPUESTA (`planned`) ────────────────
        // Nace **apagado** y sin campo en Settings: no es una preferencia del piloto, es un
        // interruptor de distribución que se enciende **sin recompilar** el día que NavData
        // publique su almacén `TaxiRoutePlanned`. Su validador de ingesta **rechaza hoy** el
        // cuerpo por `missing_stand` y `route_too_short` (ver
        // `Helpers/TaxiPlannedObservationPolicy.cs`), así que cada vuelo guiado mandaba una
        // petición condenada. Si la clave falta o está mal escrita, `GetBool` cae al `false`:
        // degradar sin datos es la regla del repo.
        public static bool TaxiPlannedObservationEnabled
            => GetBool("taxi_planned_observation_enabled", false);

        /// <summary>Idioma de la interfaz ("es"/"en"): lo usa la voz del RAAS para elegir timbre.</summary>
        public static string Language => ConfigurationManager.AppSettings["language"] ?? "es";

        private static int GetInt(string key, int defaultValue)
        {
            string value = ConfigurationManager.AppSettings[key];
            if (int.TryParse(value, out int result))
                return result;
            return defaultValue;
        }

        private static bool GetBool(string key, bool defaultValue)
        {
            string value = ConfigurationManager.AppSettings[key];
            if (bool.TryParse(value, out bool result))
                return result;
            return defaultValue;
        }

        private static double GetDouble(string key, double defaultValue)
        {
            string value = ConfigurationManager.AppSettings[key];
            if (double.TryParse(value,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out double result))
                return result;
            return defaultValue;
        }
    }
}