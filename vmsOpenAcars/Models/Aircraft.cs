namespace vmsOpenAcars.Models
{
    /// <summary>
    /// Represents an aircraft in the phpVMS virtual airline system.
    /// Contains all relevant information about a specific aircraft including registration,
    /// type, location, and availability status.
    /// </summary>
    public class Aircraft
    {
        /// <summary>
        /// Gets or sets the unique identifier for the aircraft in the phpVMS database.
        /// This is typically an auto-incremented integer or UUID.
        /// </summary>
        public string Id { get; set; }
        public int AircraftId => int.TryParse(Id, out int id) ? id : 0;
        /// <summary>
        /// Gets or sets the aircraft registration/tail number.
        /// Example: "HK1701", "N665VH", "G-EZAC"
        /// </summary>
        public string Registration { get; set; }

        /// <summary>
        /// Gets or sets the aircraft type code (ICAO type designator).
        /// Example: "B738", "A320", "BE58", "C172"
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// Gets or sets the subfleet name this aircraft belongs to.
        /// Subfleets group similar aircraft (e.g., "Baron 58", "Boeing 737-800")
        /// </summary>
        public string Subfleet { get; set; }

        /// <summary>
        /// Gets or sets the ICAO code of the airport where this aircraft is currently located.
        /// Example: "SKBO", "KJFK", "EGLL"
        /// </summary>
        public string CurrentAirport { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this aircraft is available for flight assignment.
        /// An aircraft may be unavailable due to maintenance, being in use, or other reasons.
        /// </summary>
        public bool IsAvailable { get; set; }

        /// <summary>
        /// Gets or sets the hexadecimal color code or identifier associated with this aircraft.
        /// This can be used for visual representation in maps or UI elements.
        /// Example: "e4a6b23a", "#FF5733"
        /// </summary>
        public string HexCode { get; set; }

        /// <summary>
        /// Returns a string representation of the aircraft, combining registration and type.
        /// Used primarily for display in UI lists and dropdowns.
        /// </summary>
        /// <returns>Formatted string: "Registration (Type)"</returns>
        /// <example>
        /// For an aircraft with registration "HK1701" and type "BE58", returns "HK1701 (BE58)"
        /// </example>
        public override string ToString() => $"{Registration} ({Type})";
    }
}