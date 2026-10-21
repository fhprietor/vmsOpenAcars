using System;
using System.Collections.Generic;

namespace vmsOpenAcars.Helpers
{
    /// <summary>
    /// Decide en qué idioma se locuta un anuncio de cabina, **según las voces que el equipo tiene
    /// instaladas**, no según el país de la aerolínea.
    ///
    /// El motivo es que SAPI solo sabe leer con la voz que hay: en un Windows en inglés sin el
    /// paquete de idioma español, pedir `es` hacía que `SelectVoiceByHints` fallara y la voz por
    /// defecto leyera **texto español con fonética inglesa**. En un RAAS eso es una palabra mal
    /// dicha; en un anuncio de quince segundos es inaceptable. La regla es la contraria: si el
    /// equipo no tiene voz para el idioma pedido pero sí para el otro, se cambia **el texto**, no
    /// la voz. Eso obliga a construir la frase después de resolver el idioma, que es exactamente
    /// lo que hace `CabinAnnouncementService`.
    ///
    /// Sin lista de voces (no se pudo enumerar) devuelve el idioma pedido: es el mejor esfuerzo y
    /// reproduce el comportamiento anterior, que es preferible a callar por una suposición.
    /// </summary>
    internal static class SapiVoicePolicy
    {
        /// <summary>Idiomas con plantilla propia. Añadir uno exige texto en `CabinAnnouncementScript`.</summary>
        internal static readonly string[] SupportedLanguages = { "es", "en" };

        /// <summary>
        /// Devuelve `es` o `en` —nunca otra cosa— porque son los dos idiomas con plantilla.
        /// </summary>
        /// <param name="requested">Idioma deseado por la aerolínea (`es`/`en` u otro).</param>
        /// <param name="installedCultures">Nombres de cultura de las voces instaladas (`es-ES`, `en-US`…).</param>
        internal static string ResolveLanguage(string requested, IEnumerable<string> installedCultures)
        {
            string wanted = Normalize(requested);

            if (installedCultures == null) return wanted;

            var cultures = new List<string>();
            foreach (string culture in installedCultures)
                if (!string.IsNullOrWhiteSpace(culture)) cultures.Add(culture.Trim().ToLowerInvariant());

            // No se pudo enumerar (lista vacía): no hay base para cambiar de idioma.
            if (cultures.Count == 0) return wanted;

            if (HasLanguage(cultures, wanted)) return wanted;

            string other = wanted == "es" ? "en" : "es";
            if (HasLanguage(cultures, other)) return other;

            // Solo hay voces de otros idiomas: se locuta en el pedido con la voz por defecto y el
            // servicio lo avisa en el log, porque el resultado va a sonar raro.
            return wanted;
        }

        /// <summary>Normaliza a los dos idiomas soportados; cualquier otra cosa cae a inglés.</summary>
        internal static string Normalize(string language)
        {
            if (string.IsNullOrWhiteSpace(language)) return "en";
            string v = language.Trim().ToLowerInvariant();
            return v == "es" ? "es" : "en";
        }

        private static bool HasLanguage(List<string> cultures, string language)
        {
            foreach (string culture in cultures)
                if (culture == language || culture.StartsWith(language + "-", StringComparison.Ordinal))
                    return true;
            return false;
        }
    }
}
