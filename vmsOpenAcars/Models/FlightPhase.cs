namespace vmsOpenAcars.Models
{
    /// <summary>
    /// Represents the different operational phases of a flight in chronological order.
    /// </summary>
    public enum FlightPhase
    {
        Idle = 0,

        Boarding,
        Pushback,
        TaxiOut,

        Takeoff,
        Climb,
        Enroute,
        Descent,
        Approach,

        AfterLanding,
        TaxiIn,

        Completed
    }

    /// <summary>
    /// Provides helper and classification methods for <see cref="FlightPhase"/>.
    /// </summary>
    public static class FlightPhaseExtensions
    {
        /// <summary>
        /// Determines whether the aircraft is airborne during the specified phase.
        /// </summary>
        public static bool IsAirborne(this FlightPhase phase)
        {
            return phase == FlightPhase.Takeoff ||
                   phase == FlightPhase.Climb ||
                   phase == FlightPhase.Enroute ||
                   phase == FlightPhase.Descent ||
                   phase == FlightPhase.Approach;
        }

        /// <summary>
        /// Determines whether the aircraft is on the ground.
        /// </summary>
        public static bool IsGroundPhase(this FlightPhase phase)
        {
            return !phase.IsAirborne();
        }

        /// <summary>
        /// Determines if the aircraft is in active movement (Taxi, Takeoff, Flight, etc.)
        /// excluding static phases like Boarding or Arrived.
        /// </summary>
        public static bool IsMoving(this FlightPhase phase)
        {
            return phase != FlightPhase.Boarding &&
                   phase != FlightPhase.Completed;
        }

        /// <summary>
        /// Determines whether the flight is in a pre-departure stage.
        /// </summary>
        public static bool IsPreDeparture(this FlightPhase phase)
        {
            return (int)phase <= (int)FlightPhase.TaxiOut;
        }

        /// <summary>
        /// Determines whether the flight has reached the final destination state.
        /// </summary>
        public static bool IsCompleted(this FlightPhase phase)
        {
            return phase == FlightPhase.Completed;
        }
    }
}