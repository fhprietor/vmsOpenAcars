using System.Collections.Generic;
using System.Threading.Tasks;
using vmsOpenAcars.Models;

namespace vmsOpenAcars.Services.Interfaces
{
    public interface IApiService
    {
        string     BaseUrl    { get; }
        // Nota: a propósito NO se expone HttpClient. El cliente de phpVMS lleva la
        // API key del piloto en las cabeceras por defecto; exponerlo permitía (y
        // ocurría) reutilizarlo contra hosts de terceros — SimBrief, descarga del
        // PDF del OFP — filtrando la credencial. Usar siempre un cliente de
        // Services/Http/HttpClientProvider según el destino.

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
        /// <summary>Mensajes y eventos del vuelo (`type = 2`, LOG): avisos del RAAS y líneas informativas.</summary>
        Task<bool>                                   SendAcarsLogs(string pirepId, AcarsLogEntry[] logs);
        Task<List<AcarsPosition>>                    GetPirepAcarsAsync(string pirepId);
        Task<(Pilot Data, string Error)>             GetPilotData();

        // ── Accesos encapsulados a rutas de phpVMS no cubiertas por métodos propios ──
        // Existen para que los servicios de dominio (p. ej. PhpVmsFlightService) puedan
        // consultar la API sin recibir el HttpClient autenticado. `path` es relativo a
        // la base URL; devuelve el cuerpo en texto, o null si la respuesta no fue 2xx.
        Task<string> GetAsync(string path);
        // (bool Success, string Body): Body incluye el mensaje del servidor también en
        // error, para poder reportarlo; Success refleja el código de estado HTTP.
        Task<(bool Success, string Body)> PostJsonAsync(string path, object payload);

        // GET que devuelve TAMBIÉN el código HTTP. `GetAsync` colapsa cualquier no-2xx a null,
        // y con eso no se puede decidir lo que necesita el despacho de SimBrief: un 404 de
        // ruta (= el endpoint no está desplegado → respaldo local) se ve igual que un 404
        // `flight_not_found` (= el vuelo ya no existe → decírselo al piloto). StatusCode 0
        // significa «no hubo respuesta» (timeout, DNS, red): degradar, nunca lanzar.
        Task<(int StatusCode, string Body)> GetWithStatusAsync(string path);
    }
}
