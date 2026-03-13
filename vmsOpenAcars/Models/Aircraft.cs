// Models/Aircraft.cs
namespace vmsOpenAcars.Models
{
    public class Aircraft
    {
        public string Id { get; set; }
        public string Registration { get; set; }
        public string Type { get; set; }
        public string Subfleet { get; set; }
        public string CurrentAirport { get; set; }
        public bool IsAvailable { get; set; }
        public string HexCode { get; set; }

        // Para mostrar en UI
        public override string ToString() => $"{Registration} ({Type})";
    }
}