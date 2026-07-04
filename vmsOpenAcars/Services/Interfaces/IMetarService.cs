using System;
using System.Drawing;
using System.Threading.Tasks;
using vmsOpenAcars.Models;

namespace vmsOpenAcars.Services.Interfaces
{
    public interface IMetarService
    {
        MetarFetchState State       { get; }
        MetarData[]     CurrentMetars { get; }

        event Action<MetarData[]>    OnMetarUpdated;
        event Action<MetarFetchState> OnStateChanged;
        event Action<string, Color>  OnLog;

        void SetStations(string origin, string dest, string alternate);
        void UpdatePosition(double lat, double lon);
        Task FetchNowAsync();
    }
}
