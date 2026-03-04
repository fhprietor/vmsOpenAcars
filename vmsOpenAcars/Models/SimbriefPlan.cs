namespace vmsOpenAcars.Models
{
    public class SimbriefPlan
    {
        // Información del Vuelo
        public string FlightNumber { get; set; }
        public string Airline { get; set; }
        public string Origin { get; set; }
        public string Destination { get; set; }
        public string Route { get; set; }

        // Aeronave
        public string Aircraft { get; set; }
        public string Registration { get; set; } // El "Tail Number"

        // Pesos y Combustible (Crucial para el reporte final)
        public double BlockFuel { get; set; }
        public double PayLoad { get; set; }
        public int PaxCount { get; set; }
        // En SimbriefPlan.cs (Añade esto si no lo tienes)
        public double DepartureFuel { get; set; }

        // Tiempos
        public int EstTimeEnroute { get; set; } // En segundos

        public string PlannedAltitude { get; set; }

        public string AircraftIcao { get; set; } // Añade esta línea

        // Si usas Newtonsoft.Json o System.Text.Json y el JSON de la API
        // trae un nombre distinto, usa un atributo:
        // [JsonProperty("icao_type")] 
        // public string AircraftICAO { get; set; }
    }
}