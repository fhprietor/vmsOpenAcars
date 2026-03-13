namespace vmsOpenAcars.Models
{
    public class Pilot
    {
        public int Id { get; set; }
        public string PilotId { get; set; } // Ej: VHL001
        public string Name { get; set; }
        public int AirlineId { get; set; }
        public string AirlineName { get; set; }
        public string Rank { get; set; }
        public string CurrentAirport { get; set; } // Útil para validar Jumpseat
        // Para uso local (coordenadas del aeropuerto)
        public double? CurrentAirportLat { get; set; }
        public double? CurrentAirportLon { get; set; }

    }
}