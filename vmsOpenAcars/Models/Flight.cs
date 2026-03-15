// Models/Flight.cs
using System.Collections.Generic;

namespace vmsOpenAcars.Models
{
    /// <summary>
    /// Representa un vuelo en el sistema phpVMS, ya sea desde una reserva o desde la lista de vuelos disponibles
    /// </summary>
    public class Flight
    {
        public string Id { get; set; }
        public string FlightNumber { get; set; }
        public string Airline { get; set; }
        public string Departure { get; set; }
        public string Arrival { get; set; }
        public string AircraftType { get; set; } // Podría ser el tipo principal, pero ahora usaremos la lista
        public int Distance { get; set; } // En millas náuticas (NM)
        public int FlightTime { get; set; }
        public string Route { get; set; }
        public int RequiredRank { get; set; }
        public bool IsAvailable { get; set; }
        public int Level { get; set; }
        public string BidId { get; set; }

        // Nuevas propiedades
        public List<string> AllowedAircraftTypes { get; set; } = new List<string>();
        public string AllowedAircraftTypesDisplay { get; set; } // Para mostrar en UI

        public override string ToString() => $"{Airline}{FlightNumber} → {Arrival} ({AllowedAircraftTypesDisplay})";
    }
}