namespace vmsOpenAcars.Db
{
    // ─── Result types ────────────────────────────────────────────────────────────

    public class RunwayTouchdownResult
    {
        public string RunwayName            { get; set; }
        public double ThresholdDistanceFt   { get; set; }
        public double CenterlineDeviationFt { get; set; }
        public double ThresholdLat          { get; set; }
        public double ThresholdLon          { get; set; }
        public double ThresholdHeading      { get; set; }
    }

    public class RunwayEntry
    {
        public string RunwayName  { get; set; }
        public string TaxiwayName { get; set; }
        /// <summary>True when the aircraft entered the runway heading toward the opposite threshold.</summary>
        public bool IsBacktrack { get; set; }
    }

    public class HoldingPoint
    {
        public string RunwayName  { get; set; }
        public string TaxiwayName { get; set; }
    }

    public class ParkingSpot
    {
        public string DisplayName { get; set; }
    }

    // ─── ILS / Approach result types ─────────────────────────────────────────────

    public class IlsData
    {
        public double  FrequencyMhz    { get; set; }
        public double  Course          { get; set; }
        public double  GlideSlopePitch { get; set; }
        public string  RunwayName      { get; set; }
        public double  ThresholdLat    { get; set; }
        public double  ThresholdLon    { get; set; }
        public double  ThresholdElevFt { get; set; }
        // Real glideslope intercept altitude from NavData /ils/ endpoint.
        // When set, used directly as DA instead of ThresholdElevFt + 200 ft.
        public double? GlideslopeAltFt { get; set; }
    }

    public class ApproachInfo
    {
        public int    ApproachId          { get; set; }
        public string Type                { get; set; }
        public string RunwayName          { get; set; }
        public bool   HasVerticalGuidance { get; set; }
    }

    public class ApproachFix
    {
        public string Name       { get; set; }
        public string FixType    { get; set; }
        public double Lat        { get; set; }
        public double Lon        { get; set; }
        public double AltitudeFt { get; set; }
        public bool   IsFlyover  { get; set; }
    }

    // ─── Nearest approach airport (parallel-runway-aware) ────────────────────────

    public class NearestApproachAirportResult
    {
        public string Icao              { get; set; }
        public string Name              { get; set; }
        public double AirportDistanceNm { get; set; }
        public string RunwayName        { get; set; }
        public double RunwayHeading     { get; set; }
        public bool   HasIls            { get; set; }
        public string IlsIdent          { get; set; }
        public double? IlsFreqMhz       { get; set; }
        public double? IlsCourse        { get; set; }
        public double HeadingDiffDeg    { get; set; }
        public double Score             { get; set; }
        public double? CrossTrackNm     { get; set; }
        /// <summary>Straight-line distance from the aircraft to the matched runway's
        /// threshold, in NM (unsigned — it does not say whether the aircraft is before or
        /// after the threshold; that requires projecting onto the runway axis).</summary>
        public double? DistToThresholdNm { get; set; }
    }
}
