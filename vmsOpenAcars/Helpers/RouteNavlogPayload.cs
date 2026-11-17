using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using vmsOpenAcars.Models;

namespace vmsOpenAcars.Helpers
{
    /// <summary>
    /// **El `navlog` del OFP, en una línea, para que el servidor pueda verificar la ruta sin bajar
    /// nada de SimBrief.**
    ///
    /// El porqué: medido el 16/11/2026, `simbrief_id` **no le sirve** a phpVMS tal como está: su
    /// prefile resuelve ese campo con `SimBrief::find()` contra **su propia tabla `simbriefs`**
    /// —confirmado con el stack trace de `attachSimbriefToPirep()` desde `PirepService.php:196`— y
    /// un `request_id` de SimBrief no está ahí, así que el aviso no llega **aunque la ruta cruce
    /// SKR11** (caso real: `193868184`, tramo KAGEM→EVPIP a FL340). El cliente, en cambio, ya tiene
    /// el `navlog` con lat/lon **y altitud** —la altitud es imprescindible: sin ella, sobrevolar una
    /// zona de 3.000 ft a FL340 sería un aviso falso— y mandarlo cuesta ~1,4 KB.
    ///
    /// Formato (una sola cadena, `;` entre puntos, `,` dentro):
    /// <code>IDENT,lat,lon,alt_ft;IDENT,lat,lon,alt_ft;…</code>
    /// Se manda con <see cref="VersionFieldName"/> = <see cref="Version"/>, que es lo que permitirá
    /// cambiar el formato sin adivinar qué le está llegando al servidor.
    ///
    /// **Degrada sin datos**: sin plan (un vuelo retomado no trae `navlog`), con un punto sin
    /// coordenadas o con más puntos que <see cref="MaxPoints"/>, devuelve `null` y el prefile viaja
    /// como siempre. No se manda **media** ruta: una línea recortada describe un plan que no existe y
    /// haría que el servidor avisara de un cruce inventado — o, peor, que no avisara del real.
    /// </summary>
    internal static class RouteNavlogPayload
    {
        /// <summary>Nombre del campo en el cuerpo del prefile.</summary>
        internal const string FieldName = "route_navlog";

        /// <summary>Campo hermano con el formato de la línea. Hoy <see cref="Version"/> = 1.</summary>
        internal const string VersionFieldName = "route_navlog_version";

        /// <summary>Versión del formato. Se sube si cambia la forma de la cadena, no si cambia el dato.</summary>
        internal const int Version = 1;

        /// <summary>
        /// Tope de puntos. Un OFP normal va de 25 a 60; el caso real del 863 tiene **44**. Con 200 el
        /// cuerpo sigue siendo pequeño (~7 KB) y ningún plan de este cliente se queda fuera; si
        /// alguno lo superara, no se manda (ver el resumen de la clase).
        /// </summary>
        internal const int MaxPoints = 200;

        /// <summary>
        /// La línea, o `null` si no hay `navlog` que mandar. Los decimales se recortan a 6 (~11 cm),
        /// que es la precisión con la que viene el propio OFP (`9.423611, -73.904722`) y deja la
        /// cadena corta; la altitud va en pies enteros.
        /// </summary>
        internal static string Build(IList<SimbriefWaypoint> waypoints, int maxPoints = MaxPoints)
        {
            if (waypoints == null || waypoints.Count < 2) return null;
            if (waypoints.Count > (maxPoints > 0 ? maxPoints : MaxPoints)) return null;

            var line = new StringBuilder(waypoints.Count * 34);

            foreach (var waypoint in waypoints)
            {
                // Un punto sin coordenadas invalida la línea entera: saltarlo uniría dos puntos que
                // el plan no une, y eso es inventarse el tramo.
                if (waypoint == null || !IsUsable(waypoint)) return null;

                if (line.Length > 0) line.Append(';');
                line.Append(string.IsNullOrWhiteSpace(waypoint.Ident) ? "?" : waypoint.Ident.Trim());
                line.Append(',').Append(Coordinate(waypoint.Lat));
                line.Append(',').Append(Coordinate(waypoint.Lon));
                line.Append(',').Append(waypoint.AltFt.ToString(CultureInfo.InvariantCulture));
            }

            return line.ToString();
        }

        /// <summary>¿Merece viajar? Atajo para el llamante: `Build(...) != null` sin repetir la regla.</summary>
        internal static bool ShouldSend(IList<SimbriefWaypoint> waypoints)
            => Build(waypoints) != null;

        /// <summary>
        /// Coordenadas fuera de rango, en el (0,0) exacto (el hueco de un campo que no llegó) o sin
        /// número son «no hay dato». El `navlog` de SimBrief siempre las trae, así que esto solo salta
        /// con un plan corrupto — y entonces lo honesto es no mandar nada.
        /// </summary>
        private static bool IsUsable(SimbriefWaypoint waypoint)
        {
            if (double.IsNaN(waypoint.Lat) || double.IsNaN(waypoint.Lon)) return false;
            if (Math.Abs(waypoint.Lat) > 90.0 || Math.Abs(waypoint.Lon) > 180.0) return false;
            if (waypoint.Lat == 0.0 && waypoint.Lon == 0.0) return false;
            return true;
        }

        /// <summary>
        /// Con **punto** decimal siempre: el servidor lee esto con `(float)` y una coma de la cultura
        /// del equipo lo partiría en dos campos. Es el mismo motivo por el que el resto del payload no
        /// usa el formateo por defecto.
        /// </summary>
        private static string Coordinate(double value)
            => value.ToString("0.######", CultureInfo.InvariantCulture);
    }
}
