using System;

namespace vmsOpenAcars.Helpers
{
    /// <summary>
    /// **Qué avisos de espacio aéreo se pintan en el OSD.**
    ///
    /// Dos problemas reales detrás, los dos del PIREP `nk2a96dl5vO0B2ab` (SKBO→TJSJ, B738, 27/10/2026):
    ///
    /// 1. **La casilla no callaba lo que el piloto creía.** `osd_airspace_alerts` se llamaba «Zonas
    ///    restringidas» y en la v0.9.55 solo silenciaba las **zonas acotadas** (`AIRSPACE …`,
    ///    `AIRSPACE AHEAD`, `ABOVE … DO NOT DESCEND`); las entradas en CTR/TMA/ATZ/RMZ se pintaban
    ///    **siempre**. En su llegada a San Juan fueron **19 avisos entre 19:01 y 19:36**, y **cuatro
    ///    en los últimos tres minutos de la final** (`CTR SAN JUAN LUIS MUNOZ MARIN … CLASS C`,
    ///    `TMA SAN JUAN LUIS MUNOZ MARIN`). El interruptor no mentía: cubría menos de lo que su
    ///    rótulo prometía. Desde v0.9.56 cubre **todos**.
    ///
    /// 2. **Casi todos esos 19 sobraban de todas formas.** Eran `CTA … CLASS E5`, `CTA SAN JUAN LOW`,
    ///    `CTA PONCE CLASS E2`: **fronteras de clase E**, que no piden ninguna acción y no cambian
    ///    nada de lo que hace el piloto. Esas van **solo al registro**, con el interruptor encendido o
    ///    apagado. Las `CTR`/`TMA`/`ATZ`/`RMZ` sí se quedan: llevan frecuencia y autorización.
    ///
    /// El **registro** y el **polígono del mapa** nunca dependen de esto: el piloto que apaga el OSD
    /// sigue teniendo la traza y el aviso escrito.
    /// </summary>
    internal static class AirspaceOsdPolicy
    {
        /// <summary>
        /// Tipos que **no** van al OSD en ningún caso: su cruce no pide nada al piloto. La `CTA`
        /// (área de control, aquí todas de clase E) es el caso medido.
        /// </summary>
        internal static bool IsLogOnly(string type)
            => string.Equals(type, "CTA", StringComparison.OrdinalIgnoreCase);
    }
}
