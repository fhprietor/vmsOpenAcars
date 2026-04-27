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

        static WeatherService()
        {
            _http.DefaultRequestHeaders.Add("User-Agent", "vmsOpenAcars/1.0");
            _http.Timeout = TimeSpan.FromSeconds(8);
        }

        /// <summary>
        /// Obtiene el QNH real del aeropuerto desde el último METAR disponible.
        /// aviationweather.gov devuelve altim directamente en hPa — no requiere conversión.
        /// </summary>
        /// <param name="icao">Código ICAO del aeropuerto (ej. "SKRG", "SKBO").</param>
        /// <returns>QNH en hPa redondeado a 1 decimal, o null si no disponible o fuera de rango.</returns>
        public async Task<double?> GetQnhMbAsync(string icao)
        {
            if (string.IsNullOrWhiteSpace(icao)) return null;
            try
            {
                string json = await _http.GetStringAsync(MetarApiUrl + icao.ToUpperInvariant());
                var arr = JArray.Parse(json);
                if (arr.Count == 0) return null;

                // altim ya viene en hPa — NO convertir desde inHg
                double? altimHpa = arr[0]["altim"]?.Value<double?>();
                if (altimHpa == null) return null;

                // Sanidad: QNH válido está entre 850 y 1084 hPa
                if (altimHpa < 850 || altimHpa > 1084) return null;

                return Math.Round(altimHpa.Value, 0);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Obtiene el METAR raw completo del aeropuerto.
        /// </summary>
        public async Task<string> GetRawMetarAsync(string icao)
        {
            if (string.IsNullOrWhiteSpace(icao)) return null;
            try
            {
                string json = await _http.GetStringAsync(MetarApiUrl + icao.ToUpperInvariant());
                var arr = JArray.Parse(json);
                return arr.Count > 0 ? arr[0]["rawOb"]?.ToString() : null;
            }
            catch { return null; }
        }
    }
}