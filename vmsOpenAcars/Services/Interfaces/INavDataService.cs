using System.Collections.Generic;
using vmsOpenAcars.Db;

namespace vmsOpenAcars.Services.Interfaces
{
    public interface INavDataService
    {
        bool IsAvailable { get; }

        RunwayTouchdownResult FindTouchdownRunway(string airport, double lat, double lon, double heading);
        RunwayTouchdownResult FindTakeoffRunway(string airport, double lat, double lon, double heading);
        RunwayTouchdownResult GetRunwayThreshold(string airport, double lat, double lon, double heading);
        RunwayEntry           FindRunwayEntry(string airport, double lat, double lon, double heading);
        string                FindNearestTaxiway(string airport, double lat, double lon, double heading);
        double                FindTaxiwaySegmentBearing(string airport, string taxiwayName, double lat, double lon);
        string                FindNextIntersection(string airport, double lat, double lon, double heading);
        HoldingPoint          FindHoldingPoint(string airport, double lat, double lon, double heading);
        ParkingSpot           FindNearestParking(string airport, double lat, double lon);
        IlsData               GetIlsForRunway(string airport, string runwayName);
        ApproachInfo          GetApproachType(string airport, string runwayName);
        IList<ApproachFix>    GetApproachFixes(string airport, string runwayName);
        void                  PrefetchAirport(string icao);
    }
}
