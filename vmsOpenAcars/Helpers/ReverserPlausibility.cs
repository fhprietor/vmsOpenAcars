using System;

namespace vmsOpenAcars.Helpers
{
    /// <summary>
    /// **¿Esta lectura de reversa es un despliegue de verdad?** El offset `0x207C`/`0x217C` lo
    /// publican los addons con matices que no son la reversa, y en el rodaje de llegada se ve el
    /// disparate.
    ///
    /// El caso medido es el PIREP real **`ez9OqzRnYxbnGLR8`** (B738 PMDG, MSFS 2024, SKBO→SKBG):
    ///
    /// ```
    /// 02:26:15  Reversa: desplegada  ENG1 11%  ENG2 2%          ← el toque. Correcto.
    /// 02:26:37  Reversa: máximo del rodaje  ENG1 100.0%  ENG2 100.0%
    ///           — cool-down 180s requeridos                        ← imposible
    /// ```
    ///
    /// Un **100 %** de reversa en el rodaje de llegada no es un despliegue: las reversas se
    /// guardan antes de los 60–80 kt y a esa altura el avión va a velocidad de rodaje. Es el offset
    /// publicando otra cosa —el PMDG lo deja a 100 cuando el sistema está armado, no desplegado—.
    /// Y no es cosmético: un máximo falso de 100 % entra en el resumen del PIREP y arrastra el
    /// aviso de cool-down de 180 s.
    ///
    /// **La regla, y por qué son estas dos condiciones.** Una reversa solo cuenta como desplegada
    /// **en el suelo** (lo exige el llamante: `IsOnGround`), **por encima de
    /// <see cref="MinDeploySpeedKt"/>** y **dentro de los primeros
    /// <see cref="MaxDeploySecondsAfterTouchdown"/> segundos** tras el toque:
    ///
    /// - **40 kt de suelo.** La reversa se usa en la carrera de frenada, que empieza al toque
    ///   (~140 kt en un 737) y termina muy por encima de esta cifra; a velocidad de rodaje nadie
    ///   la tiene desplegada. 40 kt deja fuera el rodaje y deja dentro todo el despliegue real,
    ///   incluido el `11 % / 2 %` del toque que **sí** vale.
    /// - **30 s desde el toque.** A máxima frenada las reversas están guardadas bastante antes; el
    ///   máximo ya está registrado y lo que llegue después es ruido del offset. 30 s cubre la
    ///   frenada completa de una pista larga sin abrir la puerta al rodaje.
    ///
    /// **Degrada sin datos**: un GS que no llega (`NaN`) o un instante de toque desconocido no
    /// bloquean la lectura —deciden las otras reglas—, porque nunca se suprime una acción por una
    /// suposición (misma regla de la casa que en los `*Plausible*` de `NavDataService`).
    /// </summary>
    internal static class ReverserPlausibility
    {
        /// <summary>
        /// GS mínima para que una lectura de reversa sea un despliegue. En la carrera de frenada de
        /// un 737 se pasa de 140 kt a ~40 kt con las reversas fuera; el rodaje va por debajo.
        /// </summary>
        internal const double MinDeploySpeedKt = 40.0;

        /// <summary>
        /// Ventana tras el toque en la que la reversa todavía puede estar desplegada. Después, el
        /// despliegue ya se registró y el offset solo puede estar publicando otra cosa.
        /// </summary>
        internal const double MaxDeploySecondsAfterTouchdown = 30.0;

        /// <summary>
        /// ¿La lectura puede contar como despliegue? `groundSpeedKt` y `secondsSinceTouchdown`
        /// aceptan `NaN`/`double.MaxValue` como «sin dato», y entonces **no bloquean**.
        /// </summary>
        internal static bool IsPlausibleDeployment(double groundSpeedKt, double secondsSinceTouchdown)
        {
            if (!double.IsNaN(groundSpeedKt) && !double.IsInfinity(groundSpeedKt)
                && groundSpeedKt < MinDeploySpeedKt)
                return false;

            // `double.MaxValue` es el centinela del llamante para «no hubo toque» (`BuildPhaseInput`),
            // así que también es «sin dato»: no se puede fechar la ventana y no se bloquea.
            bool knownSeconds = !double.IsNaN(secondsSinceTouchdown)
                             && !double.IsInfinity(secondsSinceTouchdown)
                             && secondsSinceTouchdown < double.MaxValue;
            if (knownSeconds && secondsSinceTouchdown > MaxDeploySecondsAfterTouchdown)
                return false;

            return true;
        }
    }
}
