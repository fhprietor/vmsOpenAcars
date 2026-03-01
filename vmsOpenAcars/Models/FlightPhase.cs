namespace vmsOpenAcars.Models
{
    /// <summary>
    /// Represents the different operational phases of a flight.
    /// </summary>
    /// <remarks>
    /// The numeric values are explicitly defined to ensure stability when
    /// persisting to a database or serializing.
    /// 
    /// The phases follow the chronological order of a typical commercial flight.
    /// </remarks>
    public enum FlightPhase
    {
        /// <summary>
        /// Passengers are boarding and pre-flight preparations are in progress.
        /// Aircraft is parked at the departure gate.
        /// </summary>
        Boarding = 0,

        /// <summary>
        /// Aircraft is pushing back from the gate.
        /// </summary>
        Pushback = 1,

        /// <summary>
        /// Aircraft is taxiing from the gate to the departure runway.
        /// </summary>
        TaxiOut = 2,

        /// <summary>
        /// Aircraft is performing the takeoff roll and initial climb.
        /// </summary>
        Takeoff = 3,

        /// <summary>
        /// Aircraft is cruising or proceeding along the planned route.
        /// </summary>
        Enroute = 4,

        /// <summary>
        /// Aircraft is descending and executing approach procedures.
        /// </summary>
        Approach = 5,

        /// <summary>
        /// Aircraft has touched down and is completing the landing roll.
        /// </summary>
        Landing = 6,

        /// <summary>
        /// Aircraft is taxiing from the runway to the arrival gate.
        /// </summary>
        TaxiIn = 7,

        /// <summary>
        /// Aircraft has reached the gate and the flight is completed.
        /// </summary>
        Arrived = 8
    }

    /// <summary>
    /// Provides helper and classification methods for <see cref="FlightPhase"/>.
    /// </summary>
    public static class FlightPhaseExtensions
    {
        /// <summary>
        /// Determines whether the aircraft is airborne during the specified phase.
        /// </summary>
        /// <param name="phase">The current flight phase.</param>
        /// <returns>
        /// True if the aircraft is airborne; otherwise, false.
        /// </returns>
        public static bool IsAirborne(this FlightPhase phase)
        {
            return phase == FlightPhase.Takeoff ||
                   phase == FlightPhase.Enroute ||
                   phase == FlightPhase.Approach;
        }

        /// <summary>
        /// Determines whether the aircraft is on the ground during the specified phase.
        /// </summary>
        /// <param name="phase">The current flight phase.</param>
        /// <returns>
        /// True if the aircraft is on the ground; otherwise, false.
        /// </returns>
        public static bool IsGroundPhase(this FlightPhase phase)
        {
            return !phase.IsAirborne();
        }

        /// <summary>
        /// Determines whether the flight has been completed.
        /// </summary>
        /// <param name="phase">The current flight phase.</param>
        /// <returns>
        /// True if the flight has arrived at the gate; otherwise, false.
        /// </returns>
        public static bool IsCompleted(this FlightPhase phase)
        {
            return phase == FlightPhase.Arrived;
        }

        /// <summary>
        /// Determines whether the flight is in a pre-departure phase.
        /// </summary>
        /// <param name="phase">The current flight phase.</param>
        /// <returns>
        /// True if the aircraft has not yet taken off; otherwise, false.
        /// </returns>
        public static bool IsPreDeparture(this FlightPhase phase)
        {
            return phase == FlightPhase.Boarding ||
                   phase == FlightPhase.Pushback ||
                   phase == FlightPhase.TaxiOut;
        }
    }
}