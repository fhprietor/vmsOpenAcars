namespace vmsOpenAcars.Models
{
    /// <summary>
    /// Represents the different operational phases of a flight in chronological order.
    /// </summary>
    public enum FlightPhase
    {
        /// <summary> Aircraft is at the gate, engines off or boarding. </summary>
        Boarding = 0,

        /// <summary> Aircraft is being pushed back from the gate. </summary>
        Pushback = 1,

        /// <summary> Taxiing from the gate to the departure runway. </summary>
        TaxiOut = 2,

        /// <summary> Takeoff roll and initial rotation. </summary>
        Takeoff = 3,

        /// <summary> Initial climb after liftoff. </summary>
        Climb = 4,

        /// <summary> Cruising or proceeding along the planned route. </summary>
        Enroute = 5,

        /// <summary> Descending from cruise altitude. </summary>
        Descent = 6,

        /// <summary> Executing final approach procedures. </summary>
        Approach = 7,

        /// <summary> Touchdown and landing roll. </summary>
        Landing = 8,

        /// <summary> Taxiing from the runway to the arrival gate. </summary>
        TaxiIn = 9,

        /// <summary> Aircraft reached the gate, engines off. </summary>
        Arrived = 10
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
                   phase != FlightPhase.Arrived;
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
            return phase == FlightPhase.Arrived;
        }
    }
}