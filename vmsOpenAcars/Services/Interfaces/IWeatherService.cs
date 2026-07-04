using System.Threading.Tasks;

namespace vmsOpenAcars.Services.Interfaces
{
    public interface IWeatherService
    {
        Task<double?> GetQnhMbAsync(string icao);
        Task<string>  GetRawMetarAsync(string icao);
    }
}
