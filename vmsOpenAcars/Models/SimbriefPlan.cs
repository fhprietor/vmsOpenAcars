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
        public string AircraftIcao { get; set; }
        public string Registration { get; set; } // El "Tail Number"

        // Pesos y Combustible (Crucial para el reporte final)
        public double BlockFuel { get; set; }
        public double PayLoad { get; set; }
        public int PaxCount { get; set; }

        // Tiempos
        public int EstTimeEnroute { get; set; } // En segundos

        public string PlannedAltitude { get; set; }
    }
}