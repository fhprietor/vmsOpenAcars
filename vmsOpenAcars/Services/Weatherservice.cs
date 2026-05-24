using System;
using System.Collections.Concurrent;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace vmsOpenAcars.Services
{
    /// <summary>
    /// Obtiene datos meteorológicos reales (METAR) para scoring de QNH.
    /// Ruta primaria: NavData /weather/{icao}/ (pre-parseado, misma fuente aviationweather.gov).
    /// Fallback: aviationweather.gov directo si NavData no está disponible.
    /// </summary>
    public class WeatherService
    {
        private static readonly HttpClient _http = new HttpClient();
        private static readonly ConcurrentDictionary<string, double> _qnhCache =
            new ConcurrentDictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        private const string MetarApiUrl =
            "https://aviationweather.gov/api/data/metar?format=json&taf=false&ids=";

        static WeatherService()
        {
            _http.DefaultRequestHeaders.Add("User-Agent", "vmsOpenAcars/1.0");
            _http.Timeout = TimeSpan.FromSeconds(8);
        }

        /// <summary>
        /// Obtiene el QNH real del aeropuerto.
        /// Intenta NavData primero (qnh_hpa pre-parseado); si falla, consulta
        /// aviationweather.gov directamente. En último recurso devuelve el
        /// último valor cacheado exitoso para el aeropuerto.
        /// </summary>
        public async Task<double?> GetQnhMbAsync(string icao)
        {
            if (string.IsNullOrWhiteSpace(icao)) return null;
            string key = icao.ToUpperInvariant();

            // Ruta primaria: NavData (misma fuente, datos pre-parseados)
            try
            {
                var weather = await NavDataClient.GetWeatherAsync(key).ConfigureAwait(false);
                if (weather?.QnhHpa.HasValue == true)
                {
                    double qnh = Math.Round(weather.QnhHpa.Value, 0);
                    if (qnh >= 850 && qnh <= 1084)
                    {
                        _qnhCache[key] = qnh;
                        return qnh;
                    }
                }
            }
            catch { }

            // Fallback: aviationweather.gov directo
            try
            {
                string json = await _http.GetStringAsync(MetarApiUrl + key).ConfigureAwait(false);
                var arr = JArray.Parse(json);
                if (arr.Count > 0)
                {
                    double? altimHpa = arr[0]["altim"]?.Value<double?>();
                    if (altimHpa >= 850 && altimHpa <= 1084)
                    {
                        double result = Math.Round(altimHpa.Value, 0);
                        _qnhCache[key] = result;
                        return result;
                    }
                }
            }
            catch { }

            return _qnhCache.TryGetValue(key, out double fallback) ? fallback : (double?)null;
        }

        /// <summary>
        /// Obtiene el METAR raw completo del aeropuerto.
        /// </summary>
        public async Task<string> GetRawMetarAsync(string icao)
        {
            if (string.IsNullOrWhiteSpace(icao)) return null;
            string key = icao.ToUpperInvariant();

            // Ruta primaria: NavData (raw_metar incluido en el response)
            try
            {
                var weather = await NavDataClient.GetWeatherAsync(key).ConfigureAwait(false);
                if (!string.IsNullOrEmpty(weather?.RawMetar))
                    return weather.RawMetar;
            }
            catch { }

            // Fallback: aviationweather.gov directo
            try
            {
                string json = await _http.GetStringAsync(MetarApiUrl + key).ConfigureAwait(false);
                var arr = JArray.Parse(json);
                return arr.Count > 0 ? arr[0]["rawOb"]?.ToString() : null;
            }
            catch { return null; }
        }
    }
}
