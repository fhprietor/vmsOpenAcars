using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using vmsOpenAcars.Helpers;
using vmsOpenAcars.Core.Helpers;

namespace vmsOpenAcars.Services.Http
{
    /// <summary>
    /// Provides shared, long-lived HttpClient instances per external domain group.
    /// HttpClient is designed to be reused across the application lifetime to avoid
    /// socket exhaustion and benefit from connection pooling.
    /// </summary>
    internal static class HttpClientProvider
    {
        /// <summary>IVAO API (whazzup feed, ATC data). Used by IvaoService and AirspaceMonitorService.</summary>
        public static readonly HttpClient Ivao;

        /// <summary>Aviation weather sources (aviationweather.gov). Used by MetarService and WeatherService.</summary>
        public static readonly HttpClient Metar;

        /// <summary>NavData API (runway, procedure, airspace data). Used by NavDataClient.</summary>
        public static readonly HttpClient NavData;

        /// <summary>General external requests (GitHub releases, update downloads).</summary>
        public static readonly HttpClient General;

        /// <summary>
        /// SimBrief and OFP/PDF downloads. Deliberately separate from the phpVMS client:
        /// reusing the authenticated phpVMS HttpClient here would send the pilot's phpVMS
        /// API key to third-party hosts.
        /// </summary>
        public static readonly HttpClient Simbrief;

        static HttpClientProvider()
        {
            ServicePointManager.SecurityProtocol =
                SecurityProtocolType.Tls12 | SecurityProtocolType.Tls13;

            Ivao = new HttpClient { Timeout = TimeSpan.FromSeconds(25) };
            Ivao.DefaultRequestHeaders.Add("User-Agent", "vmsOpenAcars/1.0");

            Metar = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
            Metar.DefaultRequestHeaders.Add("User-Agent", "vmsOpenAcars/1.0");

            NavData = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
            NavData.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));
            // `User-Agent` con la versión real: es lo que phpVMS pide para poder auditar quién consume
            // NavData (`vmsOpenACars/<versión>`, y no el `1.0` fijo de los otros clientes).
            NavData.DefaultRequestHeaders.Add("User-Agent", "vmsOpenACars/" + AppInfo.Version);
            // `X-API-Key` NO va como cabecera por defecto: desde el cambio de phpVMS la clave de NavData
            // ya no está en el `.config`, llega **después del arranque** en el sobre cifrado, y
            // `DefaultRequestHeaders` no se puede tocar una vez enviada la primera petición (lanza
            // `InvalidOperationException`). La pone por petición `NavDataRequest.ApplyAuth`.
            string navDomain = AppConfig.NavDataApiDomain;
            if (!string.IsNullOrEmpty(navDomain)) NavData.DefaultRequestHeaders.Add("X-Origin-Domain", navDomain);

            General = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            General.DefaultRequestHeaders.Add("User-Agent", "vmsOpenAcars-Updater");

            Simbrief = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            Simbrief.DefaultRequestHeaders.Add("User-Agent", "vmsOpenAcars/1.0");
        }
    }
}
