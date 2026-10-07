using System.Collections.Generic;
using vmsOpenAcars.Models;

namespace vmsOpenAcars.Services.Interfaces
{
    public interface ILandingLogService
    {
        bool IsAvailable { get; }

        int               SaveFlight(FlightRecord record, IList<ApproachTrackPoint> track);
        List<FlightRecord> GetFlights();
        List<ApproachTrackPoint> GetTrackPoints(int flightId);

        /// <summary>
        /// La traza **fina** del flare (10 Hz en los últimos ~1 500 ft), en su propia tabla. Se
        /// guarda aparte de `approach_track` para no mezclar dos ritmos de muestreo distintos.
        /// Devuelve cuántas muestras se escribieron; `0` si no había ninguna o si falló.
        /// </summary>
        int SaveFlareTrack(int flightId, IList<FlareTrackPoint> samples);

        /// <summary>Las muestras del flare de un vuelo. **Vacía** si ese vuelo no tiene traza fina.</summary>
        List<FlareTrackPoint> GetFlareTrack(int flightId);

        bool              HasFlights();
        void              DeleteFlight(int id);
    }
}
