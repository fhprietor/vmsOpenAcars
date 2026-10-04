using System;

namespace vmsOpenAcars.Helpers
{
    /// <summary>
    /// Cómo acabó —o va— el ciclo de la credencial de NavData. Cada valor lleva su clave de idioma
    /// (`NavDataKeyPolicy.ReasonKey`), porque «no hay clave» sin decir **por qué** no deja arreglar nada.
    /// </summary>
    internal enum NavDataKeyOutcome
    {
        /// <summary>Todavía no se ha pedido nada (ni hacía falta).</summary>
        Idle,

        /// <summary>Sobre pedido, abierto y guardado en memoria.</summary>
        Delivered,

        /// <summary>Faltan `vms_api_url` o `vms_api_key`: sin ellos no hay a quién pedir el sobre.</summary>
        MissingConfiguration,

        /// <summary>
        /// El servidor no habla el cifrado pedido: o contestó `400 navdata-unsupported-cipher`, o el
        /// sobre vino con otro `cipher` —su defecto sigue siendo `aes-256-gcm`—, que no sabemos abrir.
        /// </summary>
        UnsupportedCipher,

        /// <summary>`401`: clave ausente/inválida o piloto no `ACTIVE`. No se reintenta.</summary>
        Unauthorized,

        /// <summary>`503 navdata-not-configured`: el staff no la ha configurado. El vuelo sigue sin NavData.</summary>
        NotConfigured,

        /// <summary>Fallo de red, respuesta ilegible o sobre que no abre. Se degrada sin NavData.</summary>
        Failed,
    }

    /// <summary>Qué toca hacer con la credencial de NavData según el estado de la sesión.</summary>
    internal enum NavDataKeyAction
    {
        /// <summary>Hay sobre en memoria y no ha vencido: se sigue usando.</summary>
        UseCachedEnvelope,

        /// <summary>No hay credencial utilizable: toca pedir el sobre a phpVMS.</summary>
        RequestEnvelope,

        /// <summary>Ya se intentó y no se puede: **no reintentar en bucle** en esta sesión.</summary>
        GiveUp,
    }

    /// <summary>
    /// Decisión del ciclo de vida de la credencial de NavData (§6 del documento de phpVMS): cuándo se
    /// usa el sobre en memoria, cuándo se pide y cuándo se deja de intentarlo.
    ///
    /// **La clave, SÓLO del sobre** (decisión del mantenedor). Hasta ahora `Decide` empezaba con «si el
    /// `.config` trae `navdata_api_key`, se usa y no se pregunta a phpVMS». Ese respaldo se quitó por dos
    /// motivos, y los dos son la causa raíz de este cambio:
    /// - **La clave del `.config` es exactamente la que se filtró**: viajaba en el `.config` que se
    ///   descarga del gestor de ficheros. Mientras exista el respaldo, un sobre roto no se nota —
    ///   el cliente sigue volando con la clave vieja **y nadie se entera** de que el mecanismo nuevo
    ///   dejó de funcionar.
    /// - **Enmascara el fallo**: el respaldo convierte un error del servidor en un vuelo aparentemente
    ///   normal. Sin él, «no hay sobre» se ve en el log y se arregla.
    ///
    /// Las tres reglas duras que quedan, y el porqué de cada una:
    /// - **Una petición por sesión** (límite de phpVMS: **30/min por piloto**). El cliente pide el
    ///   sobre **una vez** y lo guarda **en memoria** hasta `expires_at`.
    /// - **No reintentar en bucle** un `401` (clave ausente/inválida o piloto no `ACTIVE`) ni un `400`
    ///   (`navdata-unsupported-cipher`): son fallos deterministas, reintentar solo gasta cuota.
    /// - **`503 navdata-not-configured`** tampoco se reintenta: el staff de la aerolínea no la ha
    ///   configurado todavía, y el vuelo **sigue sin scoring NavData**, exactamente como sin clave.
    ///
    /// Helper puro, con test: la red y el estado en memoria viven en `NavDataKeyProvider`.
    /// </summary>
    internal static class NavDataKeyPolicy
    {
        /// <summary>
        /// Decide qué hacer con el **sobre en memoria**. `configuredKey` es la `navdata_api_key` cruda
        /// del `.config` (**queda documental, se ignora a propósito**: sirve para que el test fije la
        /// regla y para que nadie vuelva a colarla en la decisión). <paramref name="alreadyAttempted"/>
        /// es «ya se pidió el sobre en esta sesión»; <paramref name="expiresAtUtc"/> nulo significa
        /// «no sé cuándo vence» y se trata como vigente: quedarse sin NavData por un formato de fecha
        /// raro sería peor que reutilizar el sobre.
        /// </summary>
        internal static NavDataKeyAction Decide(
            string configuredKey, bool haveEnvelope, DateTime? expiresAtUtc,
            bool alreadyAttempted, DateTime nowUtc)
        {
            // Sin mirar `configuredKey`: una clave escrita en el `.config` se ignora (ver el
            // resumen de la clase). La resolución de la clave efectiva es
            // `AppConfig.NavDataApiKeyEffective`, que solo lee `NavDataKeyState`.
            if (haveEnvelope && (expiresAtUtc == null || expiresAtUtc.Value > nowUtc))
                return NavDataKeyAction.UseCachedEnvelope;

            if (alreadyAttempted) return NavDataKeyAction.GiveUp;

            return NavDataKeyAction.RequestEnvelope;
        }

        /// <summary>
        /// Clave de idioma del motivo por el que la sesión se quedó sin credencial. Va aquí —y no en el
        /// proveedor— porque es una decisión pura sobre el resultado, y así se puede fijar con un test:
        /// «no hay clave» sin motivo no se puede diagnosticar.
        ///
        /// `Delivered`/`Idle` (y cualquier valor futuro) devuelven cadena vacía: no hay nada que contar.
        /// </summary>
        internal static string ReasonKey(NavDataKeyOutcome outcome)
        {
            switch (outcome)
            {
                case NavDataKeyOutcome.MissingConfiguration: return "NavDataReason_MissingConfig";
                case NavDataKeyOutcome.Unauthorized:         return "NavDataReason_Unauthorized";
                case NavDataKeyOutcome.NotConfigured:        return "NavDataReason_NotConfigured";
                case NavDataKeyOutcome.UnsupportedCipher:    return "NavDataReason_UnsupportedCipher";
                case NavDataKeyOutcome.Failed:               return "NavDataReason_Failed";
                default:                                     return "";
            }
        }

        /// <summary>
        /// ¿Hay que invalidar la caché de NavData? Solo si **cambió el `key_id`**: la caché está indexada
        /// por AIRAC, así que una rotación de clave dentro del mismo ciclo dejaría datos servidos con la
        /// credencial anterior. Sin `key_id` anterior no se purga nada (degradar sin datos).
        /// </summary>
        internal static bool CacheMustBePurged(string previousKeyId, string newKeyId)
            => !string.IsNullOrEmpty(previousKeyId)
               && !string.Equals(previousKeyId, newKeyId, StringComparison.Ordinal);

        /// <summary>
        /// Base de NavData que hay que usar: **la `url` del sobre TAL CUAL** —el contrato de phpVMS dice
        /// que es la base **completa** (p. ej. `https://navdata.vholar.co/api/v1`), no el host—, y solo
        /// si el sobre no la trae (o viene vacía) se cae a la `navdata_api_url` del `.config`.
        ///
        /// Sin normalizar ni completar nada por nuestra cuenta: si la entrega trae el host sin `/api/v1`
        /// —como la del 02/10/2026—, las llamadas fallarán y eso es un **desajuste de su setting** que
        /// hay que reportarles, no un caso a tolerar en silencio. Se recortan solo espacios de los
        /// extremos; ninguna ruta se añade ni se quita.
        /// </summary>
        internal static string ResolveUrl(string deliveredUrl, string configuredUrl)
        {
            if (!string.IsNullOrWhiteSpace(deliveredUrl)) return deliveredUrl.Trim();
            return (configuredUrl ?? "").Trim();
        }

        /// <summary>
        /// ¿La `url` que entrega el sobre es la misma que la `navdata_api_url` del `.config`? Se compara
        /// ignorando la barra final y sin distinguir mayúsculas, porque la diferencia entre
        /// `https://navdata.vholar.co/api/v1` y `.../api/v1/` no significa nada. Sirve para **reportar**
        /// la entrega (y detectar que phpVMS ha movido el servicio); la base que se usa es la del sobre,
        /// la decida <see cref="ResolveUrl"/>.
        /// </summary>
        internal static bool UrlMatches(string deliveredUrl, string configuredUrl)
        {
            if (string.IsNullOrWhiteSpace(deliveredUrl) || string.IsNullOrWhiteSpace(configuredUrl))
                return false;
            return string.Equals(deliveredUrl.Trim().TrimEnd('/'),
                                 configuredUrl.Trim().TrimEnd('/'),
                                 StringComparison.OrdinalIgnoreCase);
        }
    }
}
