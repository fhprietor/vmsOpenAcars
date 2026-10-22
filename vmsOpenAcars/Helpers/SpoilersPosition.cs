namespace vmsOpenAcars.Helpers
{
    /// <summary>
    /// Convierte la **posición** de los spoilers que publica FSUIPC en un estado utilizable.
    ///
    /// Existe porque leer el offset como un booleano era el error: la tabla oficial de FSUIPC7 lo
    /// documenta así —
    ///
    /// ```
    /// 0x0BD0  4  SPOILERS HANDLE POSITION    (0-16383)
    /// 0x0BD4  4  SPOILERS LEFT POSITION      "Spoiler Left position indicator (0-16383)"
    /// 0x0BD8  4  SPOILERS RIGHT POSITION     "Spoiler Right position indicator (0-16383)"
    /// 0x0BCC  4  SPOILERS ARMED              "Spoilers arm (0=off, 1=arm for auto deployment)"
    /// ```
    ///
    /// Hasta v0.9.49 el cliente hacía `0x0BD8 > 0 || 0x0BCC > 0`, y con eso:
    /// - **una posición de 1 sobre 16383 —el 0,006 %— contaba como «EXTENDIDOS»**, así que el temblor
    ///   del indicador de un par de cuentas escribía una línea tras otra (PIREP `5ow69vzDNW5ZMayB`:
    ///   26 líneas en 44 s de final, y rachas en el ascenso con el avión impecable);
    /// - **estar armado contaba como desplegado**, y armar los spoilers antes de aterrizar es lo normal.
    ///
    /// La regla que queda: se despliegan a partir del **10 %** de la posición (1 638 de 16 383). Es un
    /// umbral de verdad —el despliegue automático de la toma va al 100 %, y el freno aerodinámico manual
    /// por encima de eso— y el ruido de unas cuentas no lo cruza nunca.
    /// </summary>
    internal static class SpoilersPosition
    {
        /// <summary>Fondo de escala del indicador (0–16383, el rango documentado por FSUIPC7).</summary>
        internal const int RawMax = 16383;

        /// <summary>Posición a partir de la cual se considera desplegado (10 %).</summary>
        internal const double MinDeployedFraction = 0.10;

        /// <summary>Posición 0–16383 → fracción 0,0–1,0, recortando lo que llegue fuera de rango.</summary>
        internal static double FromRaw(int raw)
        {
            if (raw <= 0) return 0.0;
            if (raw >= RawMax) return 1.0;
            return (double)raw / RawMax;
        }

        /// <summary>¿La fracción indica un despliegue de verdad?</summary>
        internal static bool IsDeployed(double fraction) => fraction >= MinDeployedFraction;
    }
}
