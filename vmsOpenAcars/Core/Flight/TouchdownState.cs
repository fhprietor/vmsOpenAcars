using System;

namespace vmsOpenAcars.Core.Flight
{
    internal sealed class TouchdownState
    {
        public bool     Captured;
        public DateTime Timestamp = DateTime.MinValue;
        public int?     Fpm;
        public double   Pitch, Bank, GForce;
        public double   Lat, Lon, HeadingDeg;
        public double   DistanceFt, CenterlineDeviationFt;
        public string   RunwayName;

        public void Capture(int fpm, double pitch, double bank, double gforce,
                            double lat, double lon, double heading)
        {
            Captured   = true;
            Timestamp  = DateTime.UtcNow;
            Fpm        = fpm;
            Pitch      = pitch;
            Bank       = bank;
            GForce     = gforce;
            Lat        = lat;
            Lon        = lon;
            HeadingDeg = heading;
        }

        public void SetRunwayData(double distFt, double deviationFt, string runwayName)
        {
            DistanceFt            = distFt;
            CenterlineDeviationFt = deviationFt;
            RunwayName            = runwayName;
        }

        // Partial reset on touch-and-go: keep Fpm/GForce for scoring, clear runway data
        public void ResetRunwayData()
        {
            Captured              = false;
            DistanceFt            = 0;
            CenterlineDeviationFt = 0;
            RunwayName            = null;
        }

        public void Reset()
        {
            Captured              = false;
            Timestamp             = DateTime.MinValue;
            Fpm                   = null;
            Pitch = Bank = GForce = 0;
            Lat = Lon = HeadingDeg = 0;
            DistanceFt = CenterlineDeviationFt = 0;
            RunwayName            = null;
        }
    }
}
