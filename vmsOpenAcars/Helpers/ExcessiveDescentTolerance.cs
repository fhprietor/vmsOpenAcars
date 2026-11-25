namespace vmsOpenAcars.Helpers
{
    /// <summary>
    /// **El descenso excesivo de la compuerta de 1.000 ft AGL tiene 50 fpm de banda.**
    ///
    /// En el PIREP real `YxzZxO92Qnbxlqla` (B738 SSW, MSFS 2024, MPTO→SKCG, v0.9.79) el criterio
    /// saltó a las 03:24:14 con `Excessive descent -1007 fpm (−5)`: **7 fpm** por debajo del umbral
    /// nominal de −1.000 fpm, y costó **5 puntos**. Siete pies por minuto no son una maniobra
    /// inestable; son el ruido de la tasa vertical que publica el simulador. El piloto frustró la
    /// aproximación después, y esa frustración sí fue legítima por otras razones, pero este disparo
    /// no lo era.
    ///
    /// **El umbral efectivo pasa a −1.050 fpm** (los −1.000 nominales menos 50 fpm de margen):
    /// **−1.007 no cuenta** y **−1.200 sigue contando**. El resto de criterios de la compuerta
    /// —velocidad, bank, pitch, gear y flaps— **no se mueven**.
    ///
    /// Se compara con `<` estricto contra el umbral efectivo, así que exactamente −1.050 fpm no se
    /// marca: el criterio es «**más de** 50 fpm por debajo».
    /// </summary>
    internal static class ExcessiveDescentTolerance
    {
        /// <summary>Umbral nominal histórico de la compuerta, en fpm. No se mueve.</summary>
        internal const int ThresholdFpm = -1000;

        /// <summary>
        /// Banda de 50 fpm. Cubre el ruido de la tasa vertical sin perdonar un descenso de verdad:
        /// −1.200 fpm sigue siendo excesivo.
        /// </summary>
        internal const int ToleranceFpm = 50;

        /// <summary>
        /// El umbral que se compara y se publica: −1.050 fpm. Es la cifra que lleva el mensaje para
        /// que el registro sea transparente sobre por qué cuenta o no.
        /// </summary>
        internal const int EffectiveThresholdFpm = ThresholdFpm - ToleranceFpm;

        /// <summary>¿La tasa vertical supera el umbral efectivo hacia abajo?</summary>
        internal static bool IsExcessive(int vsFpm) => vsFpm < EffectiveThresholdFpm;
    }
}
