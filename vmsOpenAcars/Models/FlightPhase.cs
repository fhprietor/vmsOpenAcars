namespace vmsOpenAcars.Models
{
    /// <summary>
    /// Represents the different operational phases of a flight in chronological order.
    /// These phases are used throughout the application to track flight progress,
    /// update UI elements, and send status information to phpVMS.
    /// </summary>
    /// <remarks>
    /// The numeric values (starting from 0) allow for comparisons and ordering,
    /// but the order is logical rather than strictly numerical.
    /// </remarks>
    public enum FlightPhase
    {
        /// <summary>
        /// No active flight - initial state before flight start or after completion/cancellation.
        /// </summary>
        Idle = 0,

        /// <summary>
        /// Passengers are boarding the aircraft.
        /// </summary>
        Boarding,

        /// <summary>
        /// Aircraft being pushed back from the gate.
        /// </summary>
        Pushback,

        /// <summary>
        /// Taxiing from gate to departure runway.
        /// </summary>
        TaxiOut,

        /// <summary>
        /// Takeoff roll and initial climb.
        /// </summary>
        Takeoff,

        /// <summary>
        /// Climbing to cruise altitude.
        /// </summary>
        Climb,

        /// <summary>
        /// Cruising at altitude between climb and descent.
        /// </summary>
        Enroute,

        /// <summary>
        /// Descending from cruise altitude towards destination.
        /// </summary>
        Descent,

        /// <summary>
        /// Approaching the destination airport, preparing for landing.
        /// </summary>
        Approach,

        /// <summary>
        /// Final approach and landing phase.
        /// </summary>
        Landing,

        /// <summary>
        /// Aircraft has touched down but still on runway.
        /// </summary>
        Landed,

        /// <summary>
        /// Immediately after touchdown, before taxi.
        /// </summary>
        AfterLanding,

        /// <summary>
        /// Taxiing from arrival runway to gate.
        /// </summary>
        TaxiIn,

        /// <summary>
        /// Flight has arrived at gate, engines shut down.
        /// </summary>
        Arrived,

        /// <summary>
        /// Flight is completed and ready for PIREP submission.
        /// </summary>
        Completed
    }

    /// <summary>
    /// Provides helper and classification methods for <see cref="FlightPhase"/>.
    /// These extension methods simplify phase checks throughout the application.
    /// </summary>
    public static class FlightPhaseExtensions
    {
        /// <summary>
        /// Determines whether the aircraft is airborne during the specified phase.
        /// </summary>
        /// <param name="phase">The flight phase to evaluate.</param>
        /// <returns>True if the phase represents an airborne state; otherwise, false.</returns>
        /// <remarks>
        /// Airborne phases include: Takeoff, Climb, Enroute, Descent, Approach.
        /// All other phases are considered ground phases.
        /// </remarks>
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
        /// <param name="phase">The flight phase to evaluate.</param>
        /// <returns>True if the phase represents a ground state; otherwise, false.</returns>
        /// <remarks>
        /// This is the logical inverse of <see cref="IsAirborne(FlightPhase)"/>.
        /// </remarks>
        public static bool IsGroundPhase(this FlightPhase phase)
        {
            return !phase.IsAirborne();
        }

        /// <summary>
        /// Determines if the aircraft is in active movement (Taxi, Takeoff, Flight, etc.)
        /// excluding static phases like Boarding or Arrived.
        /// </summary>
        /// <param name="phase">The flight phase to evaluate.</param>
        /// <returns>True if the aircraft is moving; false if stationary.</returns>
        /// <remarks>
        /// Static phases are Boarding and Arrived. All other phases involve some form of movement.
        /// </remarks>
        public static bool IsMoving(this FlightPhase phase)
        {
            return phase != FlightPhase.Boarding &&
                   phase != FlightPhase.Arrived;
        }

        /// <summary>
        /// Determines whether the flight is in a pre-departure stage.
        /// </summary>
        /// <param name="phase">The flight phase to evaluate.</param>
        /// <returns>True if the flight is in pre-departure (Idle through TaxiOut).</returns>
        /// <remarks>
        /// Pre-departure phases are those before takeoff: Idle, Boarding, Pushback, TaxiOut.
        /// This is useful for validating if certain actions (like flight plan changes) are still allowed.
        /// </remarks>
        public static bool IsPreDeparture(this FlightPhase phase)
        {
            return (int)phase <= (int)FlightPhase.TaxiOut;
        }

        /// <summary>
        /// Determines whether the flight has reached the final destination state.
        /// </summary>
        /// <param name="phase">The flight phase to evaluate.</param>
        /// <returns>True if the phase is Completed; otherwise, false.</returns>
        /// <remarks>
        /// The Completed phase indicates the flight is finished and ready for PIREP submission.
        /// </remarks>
        public static bool IsCompleted(this FlightPhase phase)
        {
            return phase == FlightPhase.Completed;
        }
    }
}