using System.Collections.Generic;
using vmsOpenAcars.Models;

namespace vmsOpenAcars.Helpers
{
    /// <summary>
    /// "¿Está el avión donde su propio plan de vuelo dice que debería estar?"
    ///
    /// El OFP de SimBrief trae el navlog completo con las coordenadas de cada fix
    /// (<see cref="SimbriefWaypoint"/>), así que la llegada planificada es una polilínea
    /// medible. Esta es la comprobación más directa de todas las que decide si un match del
    /// endpoint es un desvío real: si el avión está dentro del corredor de la llegada que
    /// él mismo presentó, entonces está exactamente donde el plan dice, y que el matcher
    /// nombre otro aeródromo solo puede significar que la llegada pasa cerca de él.
    ///
    /// Medido con el OFP real del vuelo SKRG→SKBQ (el del falso SKTL): los puntos de su
    /// llegada planificada dan 0 NM, y el avión se mantuvo a 25–33 NM de esa traza durante
    /// todo el descenso, porque se había ido hacia SKCG. Es decir, la regla <b>no</b> habría
    /// tocado ese caso (los seis filtros geométricos son los que lo resuelven) y solo actúa
    /// en el escenario opuesto: volando la llegada presentada, donde un "desvío" a otro
    /// campo es casi siempre el aeródromo que la propia llegada sobrevuela.
    /// </summary>
    internal static class RouteCorridor
    {
        /// <summary>Ancho lateral del corredor, en NM, a cada lado de la traza planificada.</summary>
        internal const double ArrivalCorridorNm = 5.0;

        /// <summary>Los últimos NM de la traza planificada que cuentan como "la llegada".
        /// Más allá de eso el avión sigue en ruta y la comparación pierde sentido.</summary>
        internal const double ArrivalWindowNm = 40.0;

        /// <summary>
        /// Porción de la traza planificada que pertenece a la llegada: los últimos
        /// <see cref="ArrivalWindowNm"/> NM contados hacia atrás desde el último fix, más el fix
        /// anterior como ancla.
        ///
        /// Se define por distancia y no por las banderas <c>IsSidStar</c>/<c>Stage</c> porque los
        /// navlogs reales no las marcan de forma uniforme: en el OFP del vuelo SKRG→SKBQ el fix
        /// de transición LOLUD llegaba con <c>is_sid_star = 0</c> y <c>stage = DSC</c> pese a
        /// pertenecer a la llegada, así que filtrar por bandera dejaba la llegada reducida a un
        /// tramo de 11 NM.
        ///
        /// Devuelve una lista vacía cuando el plan no sirve para esto (sin navlog, o con menos
        /// de dos fixes). Nunca lanza.
        /// </summary>
        internal static List<SimbriefWaypoint> ArrivalFixes(IList<SimbriefWaypoint> waypoints)
        {
            var result = new List<SimbriefWaypoint>();
            if (waypoints == null || waypoints.Count < 2) return result;

            double accumulatedNm = 0.0;
            int start = waypoints.Count - 1;

            for (int i = waypoints.Count - 1; i > 0; i--)
            {
                accumulatedNm += GeoMath.DistanceNm(
                    waypoints[i - 1].Lat, waypoints[i - 1].Lon,
                    waypoints[i].Lat,     waypoints[i].Lon);

                start = i - 1;
                if (accumulatedNm >= ArrivalWindowNm) break;
            }

            for (int i = start; i < waypoints.Count; i++) result.Add(waypoints[i]);
            return result;
        }

        /// <summary>
        /// Distancia lateral mínima, en NM, del punto dado a la llegada planificada. Devuelve
        /// null cuando el plan no permite calcularla — quien llama debe tratar eso como "no se
        /// puede juzgar", no como "está fuera".
        /// </summary>
        internal static double? DistanceFromArrivalNm(
            IList<SimbriefWaypoint> waypoints, double lat, double lon)
        {
            List<SimbriefWaypoint> arrival = ArrivalFixes(waypoints);
            if (arrival.Count < 2) return null;

            double best = double.MaxValue;
            for (int i = 0; i < arrival.Count - 1; i++)
            {
                double d = GeoMath.DistanceToSegmentNm(
                    lat, lon,
                    arrival[i].Lat,     arrival[i].Lon,
                    arrival[i + 1].Lat, arrival[i + 1].Lon);

                if (d < best) best = d;
            }
            return best;
        }

        /// <summary>True cuando el avión está dentro del corredor de su llegada planificada.
        /// Sin plan utilizable devuelve false: no se suprime nada por una suposición.</summary>
        internal static bool IsOnArrival(
            IList<SimbriefWaypoint> waypoints, double lat, double lon,
            double corridorNm = ArrivalCorridorNm)
        {
            double? distance = DistanceFromArrivalNm(waypoints, lat, lon);
            return distance.HasValue && distance.Value <= corridorNm;
        }
    }
}
