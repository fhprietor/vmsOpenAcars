using System.Collections.Generic;
using Newtonsoft.Json;

namespace vmsOpenAcars.Models.NavData
{
    // ── Response wrappers ─────────────────────────────────────────────────────────

    internal class NavRunwaysResponse
    {
        [JsonProperty("runways")]
        public List<NavRunway> Runways { get; set; } = new List<NavRunway>();
    }

    internal class NavTaxiwaysResponse
    {
        [JsonProperty("taxiways")]
        public List<NavTaxiway> Taxiways { get; set; } = new List<NavTaxiway>();
    }

    internal class NavParkingsResponse
    {
        [JsonProperty("parkings")]
        public List<NavParking> Parkings { get; set; } = new List<NavParking>();
    }

    internal class NavHoldShortResponse
    {
        [JsonProperty("holdshort")]
        public List<NavHoldShort> Holdshort { get; set; } = new List<NavHoldShort>();
    }

    internal class NavApproachesResponse
    {
        [JsonProperty("approaches")]
        public List<NavApproach> Approaches { get; set; } = new List<NavApproach>();
    }

    // ── Runway ────────────────────────────────────────────────────────────────────

    internal class NavRunway
    {
        [JsonProperty("name")]                  public string  Name                { get; set; }
        [JsonProperty("heading")]               public double  Heading             { get; set; }
        [JsonProperty("threshold_lat")]         public double  ThresholdLat        { get; set; }
        [JsonProperty("threshold_lon")]         public double  ThresholdLon        { get; set; }
        [JsonProperty("end_lat")]               public double  EndLat              { get; set; }
        [JsonProperty("end_lon")]               public double  EndLon              { get; set; }
        [JsonProperty("length_ft")]             public double  LengthFt            { get; set; }
        [JsonProperty("width_ft")]              public double  WidthFt             { get; set; }
        [JsonProperty("elevation_ft")]          public double  ElevationFt         { get; set; }
        [JsonProperty("offset_threshold_ft")]   public double  OffsetThresholdFt   { get; set; }
        [JsonProperty("has_ils")]               public bool    HasIls              { get; set; }
        [JsonProperty("ils_ident")]             public string  IlsIdent            { get; set; }
        [JsonProperty("ils_freq_mhz")]          public double? IlsFreqMhz          { get; set; }
        [JsonProperty("ils_course")]            public double? IlsCourse           { get; set; }
        [JsonProperty("ils_glideslope_angle")]  public double? IlsGlideslopeAngle  { get; set; }
    }

    // ── Taxiway ───────────────────────────────────────────────────────────────────

    internal class NavTaxiway
    {
        [JsonProperty("name")]      public string Name      { get; set; }
        [JsonProperty("type")]      public string Type      { get; set; }
        [JsonProperty("width_ft")]  public double WidthFt   { get; set; }
        [JsonProperty("start_lat")] public double StartLat  { get; set; }
        [JsonProperty("start_lon")] public double StartLon  { get; set; }
        [JsonProperty("end_lat")]   public double EndLat    { get; set; }
        [JsonProperty("end_lon")]   public double EndLon    { get; set; }
    }

    // ── Parking ───────────────────────────────────────────────────────────────────

    internal class NavParking
    {
        [JsonProperty("name")]       public string  Name      { get; set; }
        [JsonProperty("number")]     public int?    Number    { get; set; }
        [JsonProperty("suffix")]     public string  Suffix    { get; set; }
        [JsonProperty("type")]       public string  Type      { get; set; }
        [JsonProperty("radius_ft")]  public double? RadiusFt  { get; set; }
        [JsonProperty("heading")]    public double? Heading   { get; set; }
        [JsonProperty("has_jetway")] public bool    HasJetway { get; set; }
        [JsonProperty("lat")]        public double  Lat       { get; set; }
        [JsonProperty("lon")]        public double  Lon       { get; set; }
    }

    // ── Hold short ────────────────────────────────────────────────────────────────

    internal class NavHoldShort
    {
        [JsonProperty("runway_name")] public string RunwayName { get; set; }
        [JsonProperty("lat")]         public double Lat        { get; set; }
        [JsonProperty("lon")]         public double Lon        { get; set; }
        [JsonProperty("heading")]     public double Heading    { get; set; }
    }

    // ── Approaches ────────────────────────────────────────────────────────────────

    internal class NavApproach
    {
        [JsonProperty("type")]               public string              Type             { get; set; }
        [JsonProperty("suffix")]             public string              Suffix           { get; set; }
        [JsonProperty("runway")]             public string              Runway           { get; set; }
        [JsonProperty("fix_ident")]          public string              FixIdent         { get; set; }
        [JsonProperty("has_gps_overlay")]    public bool                HasGpsOverlay    { get; set; }
        [JsonProperty("has_vertical_angle")] public bool                HasVerticalAngle { get; set; }
        [JsonProperty("legs")]               public List<NavApproachLeg> Legs            { get; set; } = new List<NavApproachLeg>();
        [JsonProperty("missed_legs")]        public List<NavApproachLeg> MissedLegs      { get; set; } = new List<NavApproachLeg>();
    }

    internal class NavApproachLeg
    {
        [JsonProperty("type")]            public string  Type           { get; set; }
        [JsonProperty("fix")]             public string  Fix            { get; set; }
        [JsonProperty("lat")]             public double? Lat            { get; set; }
        [JsonProperty("lon")]             public double? Lon            { get; set; }
        [JsonProperty("course")]          public double  Course         { get; set; }
        [JsonProperty("distance_nm")]     public double  DistanceNm     { get; set; }
        [JsonProperty("altitude_ft")]     public double  AltitudeFt     { get; set; }
        [JsonProperty("altitude2_ft")]    public double  Altitude2Ft    { get; set; }
        [JsonProperty("alt_descriptor")]  public string  AltDescriptor  { get; set; }
        [JsonProperty("speed_kts")]       public int?    SpeedKts       { get; set; }
        [JsonProperty("vertical_angle")]  public double? VerticalAngle  { get; set; }
        [JsonProperty("is_flyover")]      public bool    IsFlyover      { get; set; }
    }

    // ── Airport info ──────────────────────────────────────────────────────────────

    internal class NavAirportInfo
    {
        [JsonProperty("icao")]                    public string  Icao                 { get; set; }
        [JsonProperty("name")]                    public string  Name                 { get; set; }
        [JsonProperty("elevation_ft")]            public double  ElevationFt          { get; set; }
        [JsonProperty("transition_altitude_ft")]  public double? TransitionAltitudeFt { get; set; }
        [JsonProperty("transition_level_ft")]     public double? TransitionLevelFt    { get; set; }
    }

    // ── Status ────────────────────────────────────────────────────────────────────

    internal class NavStatusResponse
    {
        [JsonProperty("status")]            public string Status         { get; set; }
        [JsonProperty("version")]           public string Version        { get; set; }
        [JsonProperty("airac_cycle")]       public string AiracCycle     { get; set; }
        [JsonProperty("airac_valid_until")] public string AiracValidUntil { get; set; }
    }

    // ── Cabin Announcements ───────────────────────────────────────────────────────

    internal class BriefingCheckResult
    {
        public bool   Available { get; set; }
        public string Version   { get; set; }
    }
}
