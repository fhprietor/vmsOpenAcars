using Newtonsoft.Json;
using System;

namespace vmsOpenAcars.Models
{
    public class AcarsPosition
    {
        public string id { get; set; }
        public string pirep_id { get; set; }
        public int? type { get; set; }
        public int? nav_type { get; set; }
        public int? order { get; set; }
        public string name { get; set; }
        public string status { get; set; }
        public string log { get; set; }
        public double lat { get; set; }
        public double lon { get; set; }
        public double? distance { get; set; }
        public int? heading { get; set; }
        public double? altitude { get; set; }
        public double? altitude_agl { get; set; }
        public double? altitude_msl { get; set; }
        public double? vs { get; set; }
        public int? gs { get; set; }
        public int? ias { get; set; }
        public int? transponder { get; set; }
        public bool? autopilot { get; set; }
        public double? fuel { get; set; }
        public DateTime? sim_time { get; set; }
        public string source { get; set; }
        public DateTime? created_at { get; set; }
        public DateTime? updated_at { get; set; }

        // Campos adicionales para landing/takeoff
        public double? gforce { get; set; }
        public double? pitch { get; set; }
        public double? bank { get; set; }
        public double? spoilers { get; set; }
        public double? flaps { get; set; }
        public int? gear { get; set; }
        public string wind { get; set; }
    }

    public class AcarsPositionUpdate
    {
        public AcarsPosition[] positions { get; set; }
    }

    /// <summary>
    /// Una entrada de mensaje/evento para `POST /api/pireps/{id}/acars/logs` (29/09/2026). Es el
    /// camino limpio para los avisos del RAAS: **el servidor fuerza `type = 2` (LOG)**, así que el
    /// cliente no elige el tipo — que es exactamente lo que nos llevó a mandarlos con `0` y a
    /// contaminar la traza de vuelo, que es de lo que nos avisó phpVMS. Campos del contrato:
    /// `log`, `lat?`, `lon?`, `created_at?`.
    /// </summary>
    public class AcarsLogEntry
    {
        [JsonProperty("log")]        public string    Log       { get; set; }
        [JsonProperty("lat")]        public double?   Lat       { get; set; }
        [JsonProperty("lon")]        public double?   Lon       { get; set; }
        [JsonProperty("created_at")] public DateTime? CreatedAt { get; set; }

        public static AcarsLogEntry At(string log, double lat, double lon)
            => new AcarsLogEntry { Log = log, Lat = lat, Lon = lon };
    }

    public class AcarsLogsUpdate
    {
        [JsonProperty("logs")] public AcarsLogEntry[] Logs { get; set; }
    }
}