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
        bool              HasFlights();
        void              DeleteFlight(int id);
    }
}
