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
        [JsonProperty("lat")]                     public double  Lat                  { get; set; }
        [JsonProperty("lon")]                     public double  Lon                  { get; set; }
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

    // ── Navaid (VOR / NDB / DME) ─────────────────────────────────────────────────

    internal class NavNavaid
    {
        [JsonProperty("ident")]         public string  Ident        { get; set; }
        [JsonProperty("type")]          public string  Type         { get; set; }
        [JsonProperty("name")]          public string  Name         { get; set; }
        [JsonProperty("frequency_mhz")] public double? FrequencyMhz { get; set; }
        [JsonProperty("frequency_khz")] public double? FrequencyKhz { get; set; }
        [JsonProperty("lat")]           public double  Lat          { get; set; }
        [JsonProperty("lon")]           public double  Lon          { get; set; }
    }

    // ── Procedures (SID / STAR) ──────────────────────────────────────────────────

    internal class NavProcedureLeg
    {
        [JsonProperty("type")]             public string  Type           { get; set; }
        [JsonProperty("fix")]              public string  Fix            { get; set; }
        [JsonProperty("fix_type")]         public string  FixType        { get; set; }
        [JsonProperty("fix_region")]       public string  FixRegion      { get; set; }
        [JsonProperty("lat")]              public double? Lat            { get; set; }
        [JsonProperty("lon")]              public double? Lon            { get; set; }
        [JsonProperty("course")]           public double? Course         { get; set; }
        [JsonProperty("distance_nm")]      public double? DistanceNm     { get; set; }
        [JsonProperty("altitude_ft")]      public double? AltitudeFt     { get; set; }
        [JsonProperty("altitude2_ft")]     public double? Altitude2Ft    { get; set; }
        [JsonProperty("alt_descriptor")]   public string  AltDescriptor  { get; set; }
        [JsonProperty("speed_kts")]        public int?    SpeedKts       { get; set; }
        [JsonProperty("speed_limit_type")] public string  SpeedLimitType { get; set; }
        [JsonProperty("turn_direction")]   public string  TurnDirection  { get; set; }
        [JsonProperty("is_flyover")]       public bool    IsFlyover      { get; set; }
        [JsonProperty("rnp")]              public double? Rnp            { get; set; }
        // Arco DME (AF) / Radio (RF): presentes solo cuando Type == "AF" o "RF"
        [JsonProperty("dme_radius_nm")]          public double? DmeRadiusNm     { get; set; }
        [JsonProperty("dme_radial")]             public double? DmeRadial       { get; set; }
        [JsonProperty("recommended_fix")]        public string  CenterFix       { get; set; }
        [JsonProperty("recommended_fix_region")] public string  CenterFixRegion { get; set; }
        [JsonProperty("recommended_fix_lat")]    public double? CenterLat       { get; set; }
        [JsonProperty("recommended_fix_lon")]    public double? CenterLon       { get; set; }
    }

    internal class NavProcedure
    {
        [JsonProperty("name")]   public string                Name   { get; set; }
        [JsonProperty("runway")] public string                Runway { get; set; }
        [JsonProperty("legs")]   public List<NavProcedureLeg> Legs   { get; set; } = new List<NavProcedureLeg>();
    }

    internal class NavSidsResponse  { [JsonProperty("sids")]  public List<NavProcedure> Sids  { get; set; } }
    internal class NavStarsResponse { [JsonProperty("stars")] public List<NavProcedure> Stars { get; set; } }

    // ── ILS ───────────────────────────────────────────────────────────────────────

    internal class NavIlsGlideslope
    {
        [JsonProperty("pitch_deg")]   public double  PitchDeg   { get; set; }
        [JsonProperty("altitude_ft")] public double? AltitudeFt { get; set; }
        [JsonProperty("lat")]         public double? Lat        { get; set; }
        [JsonProperty("lon")]         public double? Lon        { get; set; }
        [JsonProperty("range_nm")]    public double? RangeNm    { get; set; }
    }

    internal class NavIls
    {
        [JsonProperty("ident")]            public string           Ident          { get; set; }
        [JsonProperty("type")]             public string           Type           { get; set; }
        [JsonProperty("frequency_mhz")]    public double           FrequencyMhz   { get; set; }
        [JsonProperty("runway")]           public string           Runway         { get; set; }
        [JsonProperty("loc_true_heading")] public double?          LocTrueHeading { get; set; }
        [JsonProperty("loc_width")]        public double?          LocWidth       { get; set; }
        [JsonProperty("mag_var")]          public double?          MagVar         { get; set; }
        [JsonProperty("glideslope")]       public NavIlsGlideslope Glideslope     { get; set; }
    }

    internal class NavIlsResponse
    {
        [JsonProperty("ils")]
        public List<NavIls> Ils { get; set; } = new List<NavIls>();
    }

    // ── Weather ───────────────────────────────────────────────────────────────────

    internal class NavWeather
    {
        [JsonProperty("raw_metar")]     public string  RawMetar     { get; set; }
        [JsonProperty("icao")]          public string  Icao         { get; set; }
        [JsonProperty("wind_dir")]      public int?    WindDir      { get; set; }
        [JsonProperty("wind_speed_kt")] public int?    WindSpeedKt  { get; set; }
        [JsonProperty("wind_gust_kt")]  public int?    WindGustKt   { get; set; }
        [JsonProperty("visibility_m")]  public int?    VisibilityM  { get; set; }
        [JsonProperty("ceiling_ft")]    public int?    CeilingFt    { get; set; }
        [JsonProperty("temperature_c")] public int?    TemperatureC { get; set; }
        [JsonProperty("dewpoint_c")]    public int?    DewpointC    { get; set; }
        [JsonProperty("qnh_hpa")]       public double? QnhHpa       { get; set; }
        [JsonProperty("qnh_inhg")]      public double? QnhInhg      { get; set; }
        [JsonProperty("condition")]     public string  Condition    { get; set; }
    }

    // ── Airport Waypoints (ambient) ───────────────────────────────────────────────

    internal class NavAirportWaypoint
    {
        [JsonProperty("ident")]         public string  Ident        { get; set; }
        [JsonProperty("type")]          public string  Type         { get; set; }  // "named", "VOR-H", "NDB", etc.
        [JsonProperty("lat")]           public double  Lat          { get; set; }
        [JsonProperty("lon")]           public double  Lon          { get; set; }
        [JsonProperty("distance_nm")]   public double  DistanceNm   { get; set; }
        [JsonProperty("frequency_mhz")] public double? FrequencyMhz { get; set; }
        [JsonProperty("frequency_khz")] public double? FrequencyKhz { get; set; }
    }

    internal class NavAirportWaypointsResponse
    {
        [JsonProperty("waypoints")]
        public List<NavAirportWaypoint> Waypoints { get; set; } = new List<NavAirportWaypoint>();
    }

    // ── Cabin Announcements ───────────────────────────────────────────────────────

    internal class BriefingCheckResult
    {
        public bool   Available { get; set; }
        public string Version   { get; set; }
    }
}
