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

        // NavData API
        public static string NavDataApiUrl => ConfigurationManager.AppSettings["navdata_api_url"] ?? "https://navdata.vholar.co/api/v1/";
        public static string NavDataApiKey => ConfigurationManager.AppSettings["navdata_api_key"] ?? "vhr-1c4c4be385814eed";

        // X-Origin-Domain derived from the configured phpVMS URL (e.g. "vholar.co")
        public static string NavDataApiDomain
        {
            get
            {
                try
                {
                    string url = VmsApiUrl;
                    return string.IsNullOrWhiteSpace(url) ? "" : new System.Uri(url).Host;
                }
                catch { return ""; }
            }
        }

        // LittleNavMap database (deprecated — kept for settings migration)
        public static string LnmDbPath => ConfigurationManager.AppSettings["lnm_db_path"] ?? "";

        // Landing log database
        /// <summary>Full path to the landing-log SQLite database (e.g. landing_log.sqlite).</summary>
        public static string LandingLogPath => ConfigurationManager.AppSettings["landing_log_path"] ?? "";

        // OSD Overlay
        public static bool OsdEnabled      => GetBool("osd_enabled",       true);
        public static bool OsdSoundEnabled => GetBool("osd_sound_enabled", true);
        public static int  OsdDurationSeconds => GetInt("osd_duration_seconds", 4);
        public static int  OsdScreenIndex     => GetInt("osd_screen_index",     1);
        /// <summary>OSD opacity as integer percentage 10–100 (e.g. 90 = 90%).</summary>
        public static int  OsdOpacity         => GetInt("osd_opacity", 90);

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