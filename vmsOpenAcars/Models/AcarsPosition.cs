using System;

namespace vmsOpenAcars.Models
{
    /// <summary>
    /// Represents an ACARS position record sent to the phpVMS server
    /// Directly corresponds to the 'acars' table structure in the database
    /// </summary>
    public class AcarsPosition
    {
        /// <summary>
        /// Unique record identifier (UUID)
        /// Optional: phpVMS auto-generates it if not provided
        /// </summary>
        public string id { get; set; }

        /// <summary>
        /// ID of the associated PIREP for this position
        /// Note: Usually sent in the URL, not in the request body
        /// </summary>
        public string pirep_id { get; set; }

        /// <summary>
        /// Type of ACARS record
        /// <list type="bullet">
        /// <item><term>0</term><description>FLIGHT_PATH - Normal flight position</description></item>
        /// <item><term>1</term><description>LOG - Log message</description></item>
        /// <item><term>2</term><description>EVENT - Special event</description></item>
        /// </list>
        /// </summary>
        public int? type { get; set; }

        /// <summary>
        /// Navigation type used at this point
        /// <list type="bullet">
        /// <item><term>0</term><description>Normal</description></item>
        /// <item><term>1</term><description>VOR</description></item>
        /// <item><term>2</term><description>NDB</description></item>
        /// <item><term>3</term><description>GPS</description></item>
        /// <item><term>4</term><description>FMS</description></item>
        /// </list>
        /// </summary>
        public int? nav_type { get; set; }

        /// <summary>
        /// Sequential order of positions within the flight
        /// Increments with each update to maintain correct sequence
        /// </summary>
        public int? order { get; set; }

        /// <summary>
        /// Descriptive name of the current point
        /// Examples: "TAKEOFF", "CLIMB", "ENROUTE", "WAYPOINT", "APPROACH"
        /// </summary>
        public string name { get; set; }

        /// <summary>
        /// Current flight phase or status
        /// Uses 3-letter codes according to ICAO/AIDX standard
        /// Examples: "BST" (Boarding), "TXI" (Taxi), "ENR" (Enroute), "ARR" (Arrived)
        /// </summary>
        public string status { get; set; }

        /// <summary>
        /// Log message associated with this position
        /// Typically used only for LOG type records (type = 1)
        /// </summary>
        public string log { get; set; }

        /// <summary>
        /// Current latitude in decimal degrees
        /// Positive values for North, negative for South
        /// </summary>
        public double lat { get; set; }

        /// <summary>
        /// Current longitude in decimal degrees
        /// Positive values for East, negative for West
        /// </summary>
        public double lon { get; set; }

        /// <summary>
        /// Distance traveled since the last reported position
        /// Expressed in kilometers (km)
        /// </summary>
        public double? distance { get; set; }

        /// <summary>
        /// Current magnetic heading in degrees (0-360)
        /// </summary>
        public int? heading { get; set; }

        /// <summary>
        /// Current altitude in feet above mean sea level (MSL)
        /// </summary>
        public double? altitude { get; set; }

        /// <summary>
        /// Current altitude above ground level (AGL) in feet
        /// More accurate than MSL during approaches
        /// </summary>
        public double? altitude_agl { get; set; }

        /// <summary>
        /// Current altitude above mean sea level (MSL) in feet
        /// Synonym for 'altitude' for compatibility
        /// </summary>
        public double? altitude_msl { get; set; }

        /// <summary>
        /// Current vertical speed in feet per minute (fpm)
        /// Positive values for climbing, negative for descending
        /// </summary>
        public double? vs { get; set; }

        /// <summary>
        /// Current ground speed in knots
        /// </summary>
        public int? gs { get; set; }

        /// <summary>
        /// Current indicated airspeed in knots
        /// </summary>
        public int? ias { get; set; }

        /// <summary>
        /// Current transponder code (e.g., 1200, 2000, 7000)
        /// </summary>
        public int? transponder { get; set; }

        /// <summary>
        /// Indicates whether autopilot is engaged
        /// </summary>
        public bool? autopilot { get; set; }

        /// <summary>
        /// Current fuel flow in pounds per hour (lbs/h)
        /// </summary>
        public double? fuel_flow { get; set; }

        /// <summary>
        /// Total remaining fuel in pounds (lbs)
        /// </summary>
        public double? fuel { get; set; }

        /// <summary>
        /// Simulation time (Zulu) for this position
        /// Format: DateTime in UTC
        /// </summary>
        public DateTime? sim_time { get; set; }

        /// <summary>
        /// Source or program that generated this position
        /// Example: "vmsOpenAcars"
        /// </summary>
        public string source { get; set; }

        /// <summary>
        /// Fuerza G durante el aterrizaje (en G)
        /// Calculado a partir del vertical speed
        /// </summary>
        public double? gforce { get; set; }

        /// <summary>
        /// Pitch del avión en grados (positivo = nariz arriba)
        /// </summary>
        public double? pitch { get; set; }

        /// <summary>
        /// Bank/Roll del avión en grados (positivo = ala derecha abajo)
        /// </summary>
        public double? bank { get; set; }

        /// <summary>
        /// Record creation timestamp (server-generated)
        /// </summary>
        public DateTime? created_at { get; set; }

        /// <summary>
        /// Record last update timestamp (server-generated)
        /// </summary>
        public DateTime? updated_at { get; set; }
    }

    /// <summary>
    /// Container for sending multiple ACARS positions in a single request
    /// Structure required by the api/pireps/{id}/acars/position endpoint
    /// </summary>
    public class AcarsPositionUpdate
    {
        /// <summary>
        /// Array of ACARS positions to send
        /// Usually contains a single position per update
        /// </summary>
        public AcarsPosition[] positions { get; set; }
    }
}