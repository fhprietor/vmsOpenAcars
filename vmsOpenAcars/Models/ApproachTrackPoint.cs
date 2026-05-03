namespace vmsOpenAcars.Models
{
    public class ApproachTrackPoint
    {
        public int    FlightId    { get; set; }
        public int    SeqNo       { get; set; }
        public double Lat         { get; set; }
        public double Lon         { get; set; }
        public double AltFt       { get; set; }
        public double AglFt       { get; set; }
        public double IasKt       { get; set; }
        public double VsFpm       { get; set; }
        public double HeadingDeg  { get; set; }
        public double DistNm      { get; set; }   // distance before threshold (positive = approaching)
        public double LateralFt   { get; set; }   // signed: positive = right of centreline
    }
}
