using System;

namespace vmsOpenAcars.Helpers
{
    /// <summary>
    /// Único sitio donde vive la credencial de NavData entregada por phpVMS, y vive **en memoria**:
    /// nunca se escribe en el `.config`, ni en un log, ni en telemetría.
    ///
    /// El porqué: la `navdata_api_key` **estaba publicada** en el `.config` que se descarga del gestor
    /// de ficheros, así que cualquiera podía leerla. La entregada en el sobre se guarda solo en memoria
    /// hasta `expires_at` y se pierde al cerrar la aplicación — que es justo lo que se quiere: si el
    /// `.config` del piloto se copia a otro equipo, no viaja ninguna credencial.
    ///
    /// Es un contenedor puro, sin red ni WinForms. Lo llena `NavDataKeyProvider` y lo consultan
    /// `AppConfig` (clave efectiva: **la única** fuente de la clave) y el proxy de teselas del mapa.
    /// Si no hay sobre, no hay clave: no existe respaldo del `.config`.
    /// </summary>
    internal static class NavDataKeyState
    {
        private static readonly object Gate = new object();
        private static NavDataEnvelope _envelope;

        /// <summary>Guarda el sobre recién abierto. Pisa el anterior, que ya no sirve.</summary>
        internal static void Store(NavDataEnvelope envelope)
        {
            lock (Gate) _envelope = envelope;
        }

        /// <summary>Olvida la credencial (por ejemplo al cerrar el vuelo o al rotar la clave).</summary>
        internal static void Clear()
        {
            lock (Gate) _envelope = null;
        }

        /// <summary>Copia defensiva del sobre, o `null` si todavía no ha llegado.</summary>
        internal static NavDataEnvelope Current
        {
            get { lock (Gate) return _envelope; }
        }

        /// <summary>Clave de NavData, o cadena vacía. SECRETO: no registrar.</summary>
        internal static string Key => Current?.Key ?? "";

        /// <summary>URL base entregada por phpVMS, o cadena vacía.</summary>
        internal static string Url => Current?.Url ?? "";
    }
}
