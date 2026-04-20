// Models/Pirep.cs
namespace vmsOpenAcars.Models
{
    public class Pirep
    {
        public string Id { get; set; }
        public string FlightNumber { get; set; }
        public string Origin { get; set; }
        public string Destination { get; set; }
        public string AircraftId { get; set; }
        public string AircraftType { get; set; }   
        public double BlockFuel { get; set; }       
        public double FuelUsed { get; set; }        
        public double Distance { get; set; }        
        public int FlightTime { get; set; }
        public int State { get; set; }
        public string Status { get; set; }
        public string CreatedAt { get; set; }
        public string SubmittedAt { get; set; }
        public string UpdatedAt { get; set; }

        public string StateDescription
        {
            get
            {
                switch (State)
                {
                    case 0: return "In Progress";
                    case 1: return "Pending";
                    case 2: return "Accepted";
                    default: return "Unknown";
                }
            }
        }
    }
}