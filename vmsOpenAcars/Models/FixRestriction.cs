namespace vmsOpenAcars.Models
{
    /// <summary>
    /// Altitude and speed restriction attached to a SID/STAR fix from NavData procedure legs.
    /// </summary>
    public class FixRestriction
    {
        // Altitude fields
        public double? AltFt    { get; set; }   // primary altitude (ft)
        public double? Alt2Ft   { get; set; }   // secondary altitude for BETWEEN range
        public string  AltDescr { get; set; }   // "+", "-", "A"/"@", "B"

        // Speed fields
        public int?   SpeedKts  { get; set; }
        public string SpdType   { get; set; }   // "max", "min", or null = at

        /// <summary>
        /// Returns true if there is any restriction to display.
        /// </summary>
        public bool HasAny => AltFt.HasValue || SpeedKts.HasValue;

        /// <summary>
        /// Human-readable altitude text used on the map label and OSD.
        /// Examples: "7000A", "FL180", "4000B6000", "9000-"
        /// </summary>
        public string AltText()
        {
            if (!AltFt.HasValue) return null;

            int alt = (int)AltFt.Value;
            string primary = alt >= 18000 ? $"FL{alt / 100}" : alt.ToString();

            switch (AltDescr)
            {
                case "+":  return primary + "A";                            // at or above — line below
                case "-":  return primary + "B";                            // at or below — line above
                case "A":
                case "@":  return primary;                                   // at exactly
                case "B":                                                     // between range
                    if (Alt2Ft.HasValue)
                    {
                        int alt2 = (int)Alt2Ft.Value;
                        string second = alt2 >= 18000 ? $"FL{alt2 / 100}" : alt2.ToString();
                        return $"{primary}A{second}B";
                    }
                    return primary;
                default:   return primary;
            }
        }

        /// <summary>
        /// Human-readable speed text used on the map label and OSD.
        /// Example: "250 kts"
        /// </summary>
        public string SpdText()
        {
            if (!SpeedKts.HasValue) return null;
            string prefix = SpdType == "max" ? "≤" : SpdType == "min" ? "≥" : "";
            return $"{prefix}{SpeedKts} kts";
        }

        /// <summary>
        /// Single-line OSD summary, e.g. "7000A  250 kts"
        /// </summary>
        public string OsdLine()
        {
            string a = AltText();
            string s = SpdText();
            if (a != null && s != null) return $"{a}  {s}";
            return a ?? s;
        }
    }
}
