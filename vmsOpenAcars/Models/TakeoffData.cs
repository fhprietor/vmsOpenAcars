using System;

namespace vmsOpenAcars.Models
{
    /// <summary>
    /// Datos capturados en el momento exacto del despegue (liftoff)
    /// </summary>
    public class TakeoffData
    {
        public DateTime Timestamp { get; set; }
        public double LatitudeDeg { get; set; }
        public double LongitudeDeg { get; set; }
        public double AltitudeMeters { get; set; }
        public double RotationIasKt { get; set; }
        public double HeadingDeg { get; set; }
        public double PitchDeg { get; set; }
        public double BankDeg { get; set; }
        public double Eng1N1Pct { get; set; }
        public double Eng2N1Pct { get; set; }
        public double FlapsPosition { get; set; }
        public double OatCelsius { get; set; }
        public double WindSpeedKt { get; set; }
        public double WindDirDeg { get; set; }
        public double GroundSpeedKt { get; set; }
    }
}