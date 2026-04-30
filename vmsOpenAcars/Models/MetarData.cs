using System;

namespace vmsOpenAcars.Models
{
    public enum MetarCondition { Unknown, VMC, MVMC, IMC }
    public enum MetarFetchState { Idle, Fetching, Current, Error }

    public class MetarData
    {
        public string StationLabel  { get; set; }
        public string RequestedIcao { get; set; }
        public string FetchedIcao   { get; set; }
        public string Raw           { get; set; }
        public MetarCondition Condition { get; set; } = MetarCondition.Unknown;
        public double? VisibilityKm { get; set; }
        public int?    CeilingFt    { get; set; }
        public int?    WindDir      { get; set; }
        public int?    WindSpeedKt  { get; set; }
        public int?    WindGustKt   { get; set; }
        public double? TempC        { get; set; }
        public double? DewPointC    { get; set; }
        public double? QnhHpa       { get; set; }
        public string  WxString     { get; set; }
        public string  Trend        { get; set; }
        public DateTime FetchedAt   { get; set; }
    }
}
