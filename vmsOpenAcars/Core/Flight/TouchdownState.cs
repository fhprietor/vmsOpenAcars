using System;

namespace vmsOpenAcars.Core.Flight
{
    internal sealed class TouchdownState
    {
        /// <summary>
        /// Runway geometry for the touchdown, swapped in as a single immutable reference.
        /// The three values are only meaningful together and are published from a
        /// Task.Run (TelemetryCoordinator.LookupRunwayData) while the polling thread and
        /// FilePirep read them. Assigning them as three separate fields allowed a reader
        /// to observe a half-updated set (new runway name against the previous distance);
        /// publishing one reference makes the update atomic — a reader sees either the
        /// whole previous set or the whole new one.
        /// </summary>
        internal sealed class RunwayGeometry
        {
            public readonly double DistanceFt;
            public readonly double CenterlineDeviationFt;
            public readonly string RunwayName;

            public RunwayGeometry(double distanceFt, double centerlineDeviationFt, string runwayName)
            {
                DistanceFt            = distanceFt;
                CenterlineDeviationFt = centerlineDeviationFt;
                RunwayName            = runwayName;
            }
        }

        public bool     Captured;
        public DateTime Timestamp = DateTime.MinValue;
        public int?     Fpm;
        public double   Pitch, Bank, GForce;
        public double   Lat, Lon, HeadingDeg;

        private volatile RunwayGeometry _geometry;

        public double DistanceFt            => _geometry?.DistanceFt ?? 0;
        public double CenterlineDeviationFt => _geometry?.CenterlineDeviationFt ?? 0;
        public string RunwayName            => _geometry?.RunwayName;

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
            => _geometry = new RunwayGeometry(distFt, deviationFt, runwayName);

        // Partial reset on touch-and-go: keep Fpm/GForce for scoring, clear runway data
        public void ResetRunwayData()
        {
            Captured  = false;
            _geometry = null;
        }

        public void Reset()
        {
            Captured              = false;
            Timestamp             = DateTime.MinValue;
            Fpm                   = null;
            Pitch = Bank = GForce = 0;
            Lat = Lon = HeadingDeg = 0;
            _geometry             = null;
        }
    }
}
