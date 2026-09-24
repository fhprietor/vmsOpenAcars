using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using vmsOpenAcars.Services.Http;
using vmsOpenAcars.Services.Interfaces;

namespace vmsOpenAcars.Services
{
    /// <summary>
    /// Obtiene datos meteorológicos reales (METAR) para scoring de QNH.
    /// Ruta primaria: NavData /weather/{icao}/ (pre-parseado, misma fuente aviationweather.gov).
    /// Fallback: aviationweather.gov directo si NavData no está disponible.
    /// </summary>
    public class WeatherService : IWeatherService
    {
        // Último QNH bueno conocido por aeropuerto, con la marca de cuándo se obtuvo.
        // El QNH es una magnitud METEOROLÓGICA, no estática: cachearlo indefinidamente
        // permitía comparar el altímetro del avión contra un valor de días atrás y
        // penalizar por una diferencia que ya no existe. Pasado el TTL se trata como
        // dato no disponible (null), que el scoring interpreta como "no se pudo
        // comprobar" y NO penaliza — el lado conservador.
        private static readonly ConcurrentDictionary<string, (double Qnh, DateTime FetchedAt)> _qnhCache =
            new ConcurrentDictionary<string, (double, DateTime)>(StringComparer.OrdinalIgnoreCase);

        // Un METAR es válido ~1 hora y los servicios lo emiten cada 30 min, así que más
        // allá de eso el valor cacheado no representa el QNH actual.
        private static readonly TimeSpan QnhCacheTtl = TimeSpan.FromHours(1);

        private const string MetarApiUrl =
            "https://aviationweather.gov/api/data/metar?format=json&taf=false&ids=";

        /// <summary>
        /// Obtiene el QNH real del aeropuerto.
        /// Intenta NavData primero (qnh_hpa pre-parseado); si falla, consulta
        /// aviationweather.gov directamente. En último recurso devuelve el último valor
        /// cacheado exitoso, siempre que esté dentro del TTL.
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
                        CacheQnh(key, qnh);
                        return qnh;
                    }
                }
            }
            catch { }

            // Fallback: aviationweather.gov directo
            try
            {
                string json = await HttpClientProvider.Metar.GetStringAsync(MetarApiUrl + key).ConfigureAwait(false);
                var arr = JArray.Parse(json);
                if (arr.Count > 0)
                {
                    double? altimHpa = arr[0]["altim"]?.Value<double?>();
                    if (altimHpa >= 850 && altimHpa <= 1084)
                    {
                        double result = Math.Round(altimHpa.Value, 0);
                        CacheQnh(key, result);
                        return result;
                    }
                }
            }
            catch { }

            return GetCachedQnh(key);
        }

        private static void CacheQnh(string key, double qnh)
            => _qnhCache[key] = (qnh, DateTime.UtcNow);

        /// <summary>
        /// Devuelve el QNH cacheado solo si sigue dentro del TTL. Un valor caducado se
        /// descarta (y se elimina de la caché) en lugar de usarse para puntuar.
        /// </summary>
        private static double? GetCachedQnh(string key)
        {
            if (!_qnhCache.TryGetValue(key, out var entry)) return null;

            if (DateTime.UtcNow - entry.FetchedAt > QnhCacheTtl)
            {
                _qnhCache.TryRemove(key, out _);
                return null;
            }

            return entry.Qnh;
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
                string json = await HttpClientProvider.Metar.GetStringAsync(MetarApiUrl + key).ConfigureAwait(false);
                var arr = JArray.Parse(json);
                return arr.Count > 0 ? arr[0]["rawOb"]?.ToString() : null;
            }
            catch { return null; }
        }
    }
}
