using System;

namespace vmsOpenAcars.Core.Flight
{
    internal sealed class FlightTimer
    {
        internal DateTime ServerCreatedAt    { get; private set; }
        internal DateTime ServerBlockOffTime { get; private set; }
        internal DateTime ServerBlockOnTime  { get; private set; }
        internal bool     IsTimerStarted     { get; private set; }
        internal bool     BlockOffRecorded   { get; private set; }

        internal string CurrentTimerDisplay
        {
            get
            {
                if (!IsTimerStarted) return "00:00:00";
                var e = DateTime.UtcNow - ServerCreatedAt;
                return $"{(int)e.TotalHours:D2}:{e.Minutes:D2}:{e.Seconds:D2}";
            }
        }

        internal TimeSpan CurrentFlightTime =>
            IsTimerStarted ? DateTime.UtcNow - ServerCreatedAt : TimeSpan.Zero;

        internal void Start(DateTime serverCreatedAt)
        {
            ServerCreatedAt    = serverCreatedAt;
            ServerBlockOffTime = default;
            ServerBlockOnTime  = default;
            BlockOffRecorded   = false;
            IsTimerStarted     = true;
        }

        // Used when resuming a flight whose block-off was already recorded in a prior session.
        internal void StartResumed(DateTime serverCreatedAt)
        {
            Start(serverCreatedAt);
            BlockOffRecorded = true;
        }

        internal void RecordBlockOff(DateTime dt)
        {
            ServerBlockOffTime = dt;
            BlockOffRecorded   = true;
        }

        internal void RecordBlockOn(DateTime dt) => ServerBlockOnTime = dt;

        internal void Reset()
        {
            IsTimerStarted     = false;
            BlockOffRecorded   = false;
            ServerCreatedAt    = default;
            ServerBlockOffTime = default;
            ServerBlockOnTime  = default;
        }
    }
}
