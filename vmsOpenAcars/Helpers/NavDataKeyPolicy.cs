using System;

namespace vmsOpenAcars.Helpers
{
    /// <summary>Qué toca hacer con la credencial de NavData según el estado de la sesión.</summary>
    internal enum NavDataKeyAction
    {
        /// <summary>El `.config` ya trae clave: se usa y **no se pregunta** a phpVMS.</summary>
        UseConfiguredKey,

        /// <summary>Hay sobre en memoria y no ha vencido: se sigue usando.</summary>
        UseCachedEnvelope,

        /// <summary>No hay credencial utilizable: toca pedir el sobre a phpVMS.</summary>
        RequestEnvelope,

        /// <summary>Ya se intentó y no se puede: **no reintentar en bucle** en esta sesión.</summary>
        GiveUp,
    }

    /// <summary>
    /// Decisión del ciclo de vida de la credencial de NavData (§6 del documento de phpVMS): cuándo se
    /// usa lo que hay en el `.config`, cuándo se pide el sobre y cuándo se deja de intentarlo.
    ///
    /// Las tres reglas duras, y el porqué de cada una:
    /// - **Una petición por sesión** (límite de phpVMS: **30/min por piloto**). El cliente pide el
    ///   sobre **una vez** y lo guarda **en memoria** hasta `expires_at`.
    /// - **No reintentar en bucle** un `401` (clave ausente/inválida o piloto no `ACTIVE`) ni un `400`
    ///   (`navdata-unsupported-cipher`): son fallos deterministas, reintentar solo gasta cuota.
    /// - **`503 navdata-not-configured`** tampoco se reintenta: el staff de la aerolínea no la ha
    ///   configurado todavía, y el vuelo **sigue sin scoring NavData**, exactamente como hoy sin clave.
    ///
    /// Helper puro, con test: la red y el estado en memoria viven en `NavDataKeyProvider`.
    /// </summary>
    internal static class NavDataKeyPolicy
    {
        /// <summary>
        /// Decide qué hacer. <paramref name="alreadyAttempted"/> es «ya se pidió el sobre en esta
        /// sesión»; <paramref name="expiresAtUtc"/> nulo significa «no sé cuándo vence» y se trata como
        /// vigente: quedarse sin NavData por un formato de fecha raro sería peor que reutilizar el sobre.
        /// </summary>
        internal static NavDataKeyAction Decide(
            string configuredKey, bool haveEnvelope, DateTime? expiresAtUtc,
            bool alreadyAttempted, DateTime nowUtc)
        {
            if (!string.IsNullOrEmpty(configuredKey)) return NavDataKeyAction.UseConfiguredKey;

            if (haveEnvelope && (expiresAtUtc == null || expiresAtUtc.Value > nowUtc))
                return NavDataKeyAction.UseCachedEnvelope;

            if (alreadyAttempted) return NavDataKeyAction.GiveUp;

            return NavDataKeyAction.RequestEnvelope;
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
