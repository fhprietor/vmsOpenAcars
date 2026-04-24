using System;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace vmsOpenAcars.Services
{
    /// <summary>
    /// Obtiene datos meteorológicos reales (METAR) desde aviationweather.gov.
    /// API pública OACI — sin autenticación, cubre aeropuertos colombianos y worldwide.
    /// </summary>
    public class WeatherService
    {
        private static readonly HttpClient _http = new HttpClient();

        private const string MetarApiUrl =
            "https://aviationweather.gov/api/data/metar?format=json&taf=false&ids=";

        // Factor de conversión: 1 inHg = 33.8639 hPa
        private const double InHgToHPa = 33.8639;

        static WeatherService()
        {
            _http.DefaultRequestHeaders.Add("User-Agent", "vmsOpenAcars/1.0");
            _http.Timeout = TimeSpan.FromSeconds(8);
        }

        // ── API pública ───────────────────────────────────────────────────────

        /// <summary>
        /// Obtiene el QNH real del aeropuerto desde el último METAR disponible.
        /// </summary>
        /// <param name="icao">Código ICAO del aeropuerto (ej. "SKRG", "SKBO").</param>
        /// <returns>QNH en hPa redondeado a 1 decimal, o <c>null</c> si no disponible.</returns>
        public async Task<double?> GetQnhMbAsync(string icao)
        {
            if (string.IsNullOrWhiteSpace(icao)) return null;
            try
            {
                string json = await _http.GetStringAsync(MetarApiUrl + icao.ToUpperInvariant());
                var arr = JArray.Parse(json);
                if (arr.Count == 0) return null;

                // altim viene en inHg → convertir a hPa
                double? altimInHg = arr[0]["altim"]?.Value<double?>();
                if (altimInHg == null) return null;

                return Math.Round(altimInHg.Value * InHgToHPa, 1);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Obtiene el METAR raw completo del aeropuerto.
        /// </summary>
        /// <param name="icao">Código ICAO del aeropuerto.</param>
        /// <returns>String del METAR (ej. "SKRG 231500Z 03008KT..."), o <c>null</c> si no disponible.</returns>
        public async Task<string> GetRawMetarAsync(string icao)
        {
            if (string.IsNullOrWhiteSpace(icao)) return null;
            try
            {
                string json = await _http.GetStringAsync(MetarApiUrl + icao.ToUpperInvariant());
                var arr = JArray.Parse(json);
                return arr.Count > 0 ? arr[0]["rawOb"]?.ToString() : null;
            }
            catch
            {
                return null;
            }
        }
    }
}