using System;

namespace vmsOpenAcars.Models
{
    /// <summary>
    /// Datos capturados en el momento exacto del aterrizaje (touchdown)
    /// </summary>
    public class TouchdownData
    {
        public DateTime Timestamp { get; set; }
        public double LatitudeDeg { get; set; }
        public double LongitudeDeg { get; set; }
        public double AltitudeMeters { get; set; }
        public double VerticalSpeedFpm { get; set; }
        public double GroundSpeedKt { get; set; }
        public double IasKt { get; set; }
        public double HeadingDeg { get; set; }
        public double PitchDeg { get; set; }
        public double BankDeg { get; set; }
        public double GForcePeak { get; set; }
        public double GForceAtTouch { get; set; }
        public double FlapsPosition { get; set; }
        public double SpoilersPosition { get; set; }
        public double GearPosition { get; set; }
        public double Eng1N1Pct { get; set; }
        public double Eng2N1Pct { get; set; }
        public double Eng1ReverserPct { get; set; }
        public double Eng2ReverserPct { get; set; }
        public double BrakeLeft { get; set; }
        public double BrakeRight { get; set; }
        public int AutobrakeSetting { get; set; }
        public double OatCelsius { get; set; }
        public double WindSpeedKt { get; set; }
        public double WindDirDeg { get; set; }
    }
}