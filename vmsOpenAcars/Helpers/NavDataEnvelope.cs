using System;

namespace vmsOpenAcars.Helpers
{
    /// <summary>
    /// Credencial de NavData ya descifrada, tal y como viaja dentro del sobre de phpVMS.
    /// **Es un secreto**: no se registra en logs, no se escribe en el `.config` y no se manda en
    /// telemetría. Solo vive en memoria.
    /// </summary>
    internal sealed class NavDataEnvelope
    {
        /// <summary>URL base del servicio NavData que phpVMS entrega con la clave.</summary>
        public string Url { get; set; }

        /// <summary>Clave de NavData. SECRETO: no registrar nunca.</summary>
        public string Key { get; set; }

        /// <summary>Identificador de la clave: si cambia, la caché de NavData queda obsoleta.</summary>
        public string KeyId { get; set; }

        /// <summary>`issued_at` tal cual vino (se conserva crudo por si no es una fecha ISO).</summary>
        public string IssuedAt { get; set; }

        /// <summary>`expires_at` tal cual vino.</summary>
        public string ExpiresAt { get; set; }

        /// <summary>
        /// `expires_at` interpretado como instante UTC, o `null` si no se pudo interpretar.
        /// Nulo significa «no sé cuándo vence», no «ya venció»: para una credencial de un solo uso por
        /// sesión es más seguro seguir usándola que quedarse sin NavData por un formato de fecha raro.
        /// </summary>
        public DateTime? ExpiresAtUtc { get; set; }
    }
}
