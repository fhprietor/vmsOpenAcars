using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using vmsOpenAcars.Models;

namespace vmsOpenAcars.Services.Interfaces
{
    public interface IApiService
    {
        string     BaseUrl    { get; }
        HttpClient HttpClient { get; }

        Task<List<Flight>>                           GetPilotBids();
        Task<string>                                 GetBidIdForFlight(string flightId);
        Task<bool>                                   DeleteBid(string bidId);
        Task<bool>                                   UpdatePirep(string pirepId, object data);
        Task<bool>                                   DeletePirep(string pirepId);
        Task<bool>                                   FilePirep(string pirepId, object finalData);
        Task<(string pirepId, System.DateTime serverCreatedAt)> PrefileFlight(SimbriefPlan plan, Pilot pilot);
        Task<Pirep>                                  GetPirepDetail(string pirepId);
        Task<List<Pirep>>                            GetActivePireps();
        Task<bool>                                   DeletePirepById(string pirepId);
        Task<bool>                                   SendPositionUpdate(string pirepId, object telemetry);
        Task<List<AcarsPosition>>                    GetPirepAcarsAsync(string pirepId);
        Task<(Pilot Data, string Error)>             GetPilotData();
        Task<string>                                 GetNearestAirport(double latitude, double longitude);
        Task                                         MovePilotAsync(string airportIcao);
    }
}
