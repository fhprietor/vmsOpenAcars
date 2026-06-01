namespace vmsOpenAcars.Models
{
    public class SimbriefWaypoint
    {
        public string Ident     { get; set; }
        public string Type      { get; set; }  // apt, wpt, vor, ndb, dme, latlon
        public double Lat       { get; set; }
        public double Lon       { get; set; }
        public string Airway    { get; set; }  // airway name, SID, STAR, DCT (via_airway en SimBrief)
        public string Stage     { get; set; }  // CLB, CRZ, DSC
        public int    AltFt     { get; set; }
        public bool   IsSidStar { get; set; }  // is_sid_star == "1" en SimBrief navlog
        public string         Freq        { get; set; }
        public double?        MagTrack    { get; set; }  // magnetic track to next fix (SimBrief "track")
        public FixRestriction Restriction { get; set; }
    }
}
