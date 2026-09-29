using System;
using System.Collections.Generic;
using System.Linq;
using vmsOpenAcars.Models.NavData;

namespace vmsOpenAcars.Helpers
{
    /// <summary>
    /// Qué punto de espera está delante del avión y cómo se llama. Puro y con su test, para poder
    /// juzgarlo con los puntos reales de un aeropuerto.
    ///
    /// Existe por dos defectos que se ven en los datos de SKBO:
    ///
    /// · **El filtro por pista tiene que usar `runway_names`, no `runway_name`** (NavData, 29/09/2026).
    ///   Un punto de espera a mitad de pista no es «de 14L» ni «de 32R»: es de la pista **14L/32R**, y
    ///   eso es lo que trae la pareja. Filtrando por la etiqueta del extremo se descartarían los
    ///   puntos de la propia pista que se va a entrar.
    ///
    /// · **Hasta el 29/09/2026 la lista de puntos se deducía por geometría y sobre-generaba.** Para la
    ///   14L publicaba 14, y solo 2 caían a menos de 120 m del eje: los otros 12 eran nodos de las
    ///   paralelas `A`/`A1`/`A2`/`L`, a 139–254 m del eje. Como el avión rueda *sobre* esas calles, la
    ///   distancia avión→punto es de metros y va **hacia** ellos, así que ni el radio ni el filtro de
    ///   rumbo los descartaban: rodando hacia la 14R se avisaba «espera antes de pista **14L**». Ahora
    ///   la respuesta sale de los **tipos de nodo del escenario** (`HSND`/`IHSND`) y la 14L tiene 6
    ///   puntos, pero el filtro por pista sigue siendo la red de seguridad.
    ///
    /// Y el nombre: en un cruce de cuatro calles (`A1`, `A2`, `A3` y `E` en el nodo que el piloto
    /// llama «A3») los cuatro nombres son ciertos: NavData sugiere uno (`A1` hoy) y el piloto dice
    /// `A3`. El aviso útil es **la calle por la que llega el avión**, y solo si esa calle está entre
    /// las del nodo —si no, el nombre vendría de un vecino que no toca el punto de espera—.
    /// </summary>
    internal static class HoldShortSelector
    {
        /// <summary>Distancia máxima para considerar que el punto de espera está delante.</summary>
        internal const double RadiusM = 200.0;

        /// <summary>El punto de espera más cercano que el avión tiene por delante, o <c>null</c>.
        /// <paramref name="runway"/> puede ser <c>null</c> o vacío: entonces no se filtra por pista
        /// (comportamiento anterior), que es lo que queremos cuando aún no sabemos a cuál vamos.</summary>
        internal static NavHoldShort Select(
            IEnumerable<NavHoldShort> holdShorts,
            double lat, double lon, double heading,
            string runway,
            double radiusM = RadiusM)
        {
            if (holdShorts == null) return null;

            bool filterRunway = !string.IsNullOrWhiteSpace(runway);
            NavHoldShort best = null;
            double bestDist = double.MaxValue;

            foreach (var hs in holdShorts)
            {
                if (hs == null) continue;
                if (filterRunway && !ServesRunway(hs, runway)) continue;

                double d = GeoMath.DistanceNm(lat, lon, hs.Lat, hs.Lon) * 1852.0;
                if (d >= radiusM) continue;
                if (d >= bestDist) continue;

                // El `heading` de un hold-short es el EJE DE LA PISTA, no la dirección con la que se
                // llega, así que no sirve para descartar. Lo que discrimina es ir HACIA el punto: si
                // queda de través (rodando en paralelo), no se avisa.
                bool toward = double.IsNaN(heading)
                    || GeoMath.BearingDiffDeg(GeoMath.BearingDeg(lat, lon, hs.Lat, hs.Lon), heading) <= 90.0;
                if (!toward) continue;

                best = hs;
                bestDist = d;
            }

            return best;
        }

        /// <summary>¿Este punto de espera pertenece a la pista a la que vamos? Se pregunta a
        /// `runway_names` (la pareja física) y, si no viene, a `runway_name`.</summary>
        internal static bool ServesRunway(NavHoldShort hs, string runway)
        {
            if (hs == null || string.IsNullOrWhiteSpace(runway)) return false;

            var names = hs.RunwayNames?.Where(n => !string.IsNullOrWhiteSpace(n)).ToList();
            if (names != null && names.Count > 0)
                return names.Any(n => string.Equals(n, runway, StringComparison.OrdinalIgnoreCase));

            return string.Equals(hs.RunwayName, runway, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Cómo se llama el punto de espera en el aviso: la calle por la que llega el avión
        /// si esa calle toca el nodo; si no, la sugerencia de NavData (que al menos se elige por ser
        /// un acceso); si no, la que traíamos.</summary>
        internal static string ForCallout(string currentTaxiway, NavHoldShort hs)
        {
            if (hs == null) return currentTaxiway;

            var names = hs.Taxiways?.Where(n => !string.IsNullOrWhiteSpace(n)).ToList() ?? new List<string>();
            if (names.Count > 0)
            {
                if (!string.IsNullOrWhiteSpace(currentTaxiway)
                    && names.Any(n => string.Equals(n, currentTaxiway, StringComparison.OrdinalIgnoreCase)))
                    return currentTaxiway;   // la calle del avión está en el nodo: es el nombre correcto
                if (!string.IsNullOrWhiteSpace(hs.Taxiway)
                    && names.Any(n => string.Equals(n, hs.Taxiway, StringComparison.OrdinalIgnoreCase)))
                    return hs.Taxiway;       // fuera de la calle del avión: la que NavData marca como acceso
                return names[0];             // último recurso: un nombre del nodo, nunca uno de un vecino
            }

            return !string.IsNullOrWhiteSpace(hs.Taxiway) ? hs.Taxiway : currentTaxiway;
        }
    }
}
