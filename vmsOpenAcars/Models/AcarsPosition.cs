// Models/AcarsPosition.cs
using System;

namespace vmsOpenAcars.Models
{
    public class AcarsPosition
    {
        // Campos de la tabla acars
        public string id { get; set; }  // Opcional, phpVMS lo genera
        public string pirep_id { get; set; } // Se envía en URL, no necesario aquí
        public int? type { get; set; } // Tipo de posición (0=normal, 1=despegue, etc.)
        public int? nav_type { get; set; } // Tipo de navegación
        public int? order { get; set; } // Orden secuencial
        public string name { get; set; } // Nombre del punto (ej. "WAYPOINT", "TAKEOFF")
        public string status { get; set; } // Estado (enroute, etc.)
        public string log { get; set; } // Mensaje de log
        public double lat { get; set; }
        public double lon { get; set; }
        public double? distance { get; set; } // Distancia recorrida desde último punto
        public int? heading { get; set; }
        public double? altitude { get; set; }
        public double? altitude_agl { get; set; }
        public double? altitude_msl { get; set; }
        public double? vs { get; set; }
        public int? gs { get; set; }
        public int? ias { get; set; }
        public int? transponder { get; set; }
        public bool? autopilot { get; set; }
        public double? fuel_flow { get; set; }
        public double? fuel { get; set; } // Combustible (coincide con tabla)
        public DateTime? sim_time { get; set; }
        public string source { get; set; }
        public DateTime? created_at { get; set; }
        public DateTime? updated_at { get; set; }
    }

    public class AcarsPositionUpdate
    {
        public AcarsPosition[] positions { get; set; }
    }
}