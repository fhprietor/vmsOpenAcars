// Models/Pirep.cs
namespace vmsOpenAcars.Models
{
    /// <summary>
    /// Códigos de la columna `state` de un PIREP en phpVMS.
    ///
    /// Solo el valor <c>InProgress = 0</c> está verificado contra producción: <see
    /// cref="Services.ApiService.GetActivePireps"/> filtra activos con `state == 0` y
    /// ese filtro funciona. El resto sigue el orden de la tabla de estados de phpVMS y
    /// coincide con el que ya documentaba <see cref="Pirep.StateDescription"/>.
    ///
    /// Los dos únicos estados "activos" (el PIREP sigue en el ciclo del piloto) son
    /// <c>InProgress</c> y <c>Paused</c>. Todo lo demás significa que el PIREP fue
    /// archivado.
    /// </summary>
        /// <remarks>
        /// Los valores son los de **phpVMS v7** (`app/Enums/PirepState.php`), leídos de su código el
        /// 27/10/2026: `IN_PROGRESS=0, PENDING=1, ACCEPTED=2, CANCELLED=3, DELETED=4, DRAFT=5,
        /// REJECTED=6, PAUSED=7`.
        ///
        /// Hasta v0.9.56 estaban **inventados** (`Paused=1`, `Pending=2`, `Accepted=3`…) y el error
        /// tenía consecuencia: un PIREP en **`Pending` (1)** se leía como «pausado» = activo, así que
        /// el fallback de `FilePirep()` lo tomaba por no archivado — y un vuelo enviado a revisión es
        /// justo el que ahora se manda con `1`.
        /// </remarks>
        public enum PirepState
        {
            InProgress = 0,
            Pending    = 1,
            Accepted   = 2,
            Cancelled  = 3,
            Deleted    = 4,
            Draft      = 5,
            Rejected   = 6,
            Paused     = 7,
        }

    public class Pirep
    {
        public string Id { get; set; }
        public string FlightNumber { get; set; }
        public string Origin { get; set; }
        public string Destination { get; set; }
        public string AircraftId { get; set; }
        public string AircraftType { get; set; }   
        public double BlockFuel { get; set; }       
        public double FuelUsed { get; set; }        
        public double Distance { get; set; }        
        public int FlightTime { get; set; }
        public int State { get; set; }
        public string Status { get; set; }
        public string CreatedAt { get; set; }
        public string SubmittedAt { get; set; }
        public string UpdatedAt { get; set; }

        /// <summary>
        /// True cuando el PIREP sigue en el ciclo activo del piloto (`in_progress` o
        /// `paused`), es decir, NO ha sido archivado. Un PIREP archivado está fuera de
        /// estos dos estados.
        ///
        /// Es la clasificación que usa el fallback de `FilePirep()` cuando phpVMS
        /// archiva el PIREP pero devuelve un código HTTP no-2xx: si el PIREP ya no está
        /// activo, el archivado se produjo y reintentar no aporta nada.
        /// </summary>
        public bool IsActive
            => State == (int)PirepState.InProgress || State == (int)PirepState.Paused;

        /// <summary>
        /// Valor de <see cref="State"/> cuando la respuesta no traía el campo `state`.
        /// Se distingue de <c>InProgress = 0</c> a propósito, porque un DTO sin poblar
        /// vale 0 por defecto y no debe confundirse con un estado leído del servidor.
        /// </summary>
        public const int UnknownState = -1;

        /// <summary>
        /// Clasifica un PIREP a partir del valor de `state`. Devuelve <c>true</c>
        /// (seguir tratándolo como activo) cuando el estado es desconocido o no se pudo
        /// leer, para que un fallo al interpretar la respuesta nunca se convierta en un
        /// "PIREP enviado" falso.
        /// </summary>
        public static bool IsActiveState(int? state)
        {
            if (!state.HasValue) return true;              // sin dato -> no afirmar archivado
            if (state.Value == UnknownState) return true;  // centinela -> idem
            return state.Value == (int)PirepState.InProgress
                || state.Value == (int)PirepState.Paused;
        }

        public string StateDescription
        {
            get
            {
                switch (State)
                {
                    case 0: return "In Progress";
                    case 1: return "Pending";
                    case 2: return "Accepted";
                    default: return "Unknown";
                }
            }
        }
    }
}
