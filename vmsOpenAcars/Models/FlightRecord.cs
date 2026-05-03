using System;

namespace vmsOpenAcars.Models
{
    public class FlightRecord
    {
        public int    Id                { get; set; }
        public string FlightNumber      { get; set; }
        public string Origin            { get; set; }
        public string Destination       { get; set; }
        public string RunwayName        { get; set; }
        public DateTime FlightDate      { get; set; }
        public int    LandingRateFpm    { get; set; }
        public double GForce            { get; set; }
        public double TouchdownDistFt   { get; set; }
        public double CenterlineDevFt   { get; set; }
        public int    Score             { get; set; }
        public string MetarRaw          { get; set; }

        public string DisplayDate        => FlightDate.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
        public string DisplayLandingRate => $"{LandingRateFpm} fpm";
        public string DisplayRoute       => $"{Origin} → {Destination}";
        public string DisplayScore       => $"{Score}/100";
    }
}
