namespace vmsOpenAcars.Models
{
    /// <summary>
    /// Represents a pilot in the phpVMS virtual airline system.
    /// Contains personal information, rank, location data, and credentials.
    /// </summary>
    public class Pilot
    {
        /// <summary>
        /// Gets or sets the unique identifier for the pilot in the phpVMS database.
        /// This is typically an auto-incremented integer used for internal references.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Gets or sets the pilot's alphanumeric identifier/callsign.
        /// Example: "VHL001", "VHR055", "DLH123"
        /// </summary>
        /// <remarks>
        /// This is the public-facing pilot ID used in flight plans and ACARS communications.
        /// </remarks>
        public string PilotId { get; set; }

        /// <summary>
        /// Gets or sets the full name of the pilot.
        /// Example: "Franklin P", "John Smith"
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the airline ID this pilot belongs to.
        /// Used for permissions and airline-specific operations.
        /// </summary>
        public int AirlineId { get; set; }

        /// <summary>
        /// Gets or sets the name of the airline this pilot is associated with.
        /// Example: "Vholar Virtual", "Delta Virtual"
        /// </summary>
        public string AirlineName { get; set; }

        /// <summary>
        /// Gets or sets the pilot's current rank/position within the virtual airline.
        /// Example: "Captain", "First Officer", "Senior Captain"
        /// </summary>
        /// <remarks>
        /// Rank may affect which aircraft and flights the pilot can operate.
        /// </remarks>
        public string Rank { get; set; }

        /// <summary>
        /// Gets or sets the ICAO code of the airport where the pilot is currently located according to phpVMS.
        /// Example: "SKBO", "SKBG", "KJFK"
        /// </summary>
        /// <remarks>
        /// This is used for validating jumpseat operations and ensuring the pilot is at the correct departure airport.
        /// </remarks>
        public string CurrentAirport { get; set; }

        /// <summary>
        /// Gets or sets the latitude of the current airport (for local validation purposes).
        /// Used to calculate distance between the pilot's assigned location and actual simulator position.
        /// </summary>
        /// <remarks>
        /// This is populated either from the phpVMS API or from local fallback coordinates.
        /// </remarks>
        public double? CurrentAirportLat { get; set; }

        /// <summary>
        /// Gets or sets the longitude of the current airport (for local validation purposes).
        /// Used together with <see cref="CurrentAirportLat"/> to validate GPS position.
        /// </summary>
        public double? CurrentAirportLon { get; set; }

        /// <summary>
        /// IVAO VID (numeric). Zero means not configured in phpVMS.
        /// Field source: users.ivao_id via phpVMS API.
        /// </summary>
        public int IvaoId { get; set; }
    }
}