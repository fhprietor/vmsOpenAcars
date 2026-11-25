namespace vmsOpenAcars.Helpers
{
    /// <summary>
    /// **La altitud de decisión de una aproximación ILS, con la única fuente que el cliente tiene
    /// de verdad: la elevación del umbral.** El PIREP real `l5QVxBEmJ4yOaY2O` (B738, SKRG→MPTO)
    /// escribió `⚠️ BELOW MINIMUMS: 49 ft MSL < DA 50 ft` en la aproximación: esos **50 ft** no eran
    /// una DA, eran el **TCH** (altura de cruce del umbral) que NavData empezó a publicar en el
    /// campo `glideslope.altitude_ft` en la 0.9.75 y que `ApproachValidator` tomaba como altitud de
    /// decisión. La cuenta comparaba una altitud **MSL** (49 ft) con una altura **sobre el umbral**
    /// (50 ft), en un campo cuya elevación es **134 ft**: un avión a 49 ft MSL estaría bajo tierra.
    ///
    /// **NavData no publica mínimos de carta.** Se revisó `NavIls` (ident, frecuencia, rumbo,
    /// glideslope con `pitch_deg`/`altitude_ft`/lat/lon) y `NavApproach` (tipo, sufijo, pista, fixes,
    /// guía vertical, legs, transiciones): no hay `da_ft`, `dh_ft`, `minimums` ni RVR. Lo único que
    /// hay es la elevación del umbral (`NavRunway.elevation_ft`) y el TCH derivado (`tch_ft`), que
    /// **no es una DA**. Así que la DA es la de **CAT I estándar**: umbral + 200 ft, que es
    /// exactamente la rama de respaldo que ya existía y la que hay que usar siempre.
    ///
    /// **Sin elevación del umbral no hay comparación** (devuelve `null`): no se marca ni se
    /// penaliza. Inventar un 0 convertiría cada aproximación en un «bajo mínimos».
    /// </summary>
    internal static class DecisionAltitude
    {
        /// <summary>
        /// Altura CAT I sobre el umbral: 200 ft. Es el respaldo histórico del cliente y no se mueve.
        /// </summary>
        internal const double CatIAboveThresholdFt = 200.0;

        /// <summary>
        /// DA en pies **MSL** = elevación del umbral + 200 ft, o `null` si no hay elevación
        /// utilizable (0, negativa o `NaN`). No acepta el TCH: ese dato es una altura sobre el
        /// umbral, no una altitud, y mezclarlos fue el aviso falso del PIREP `l5QVxBEmJ4yOaY2O`.
        /// </summary>
        internal static double? FromThresholdElevation(double thresholdElevFt)
        {
            if (double.IsNaN(thresholdElevFt) || thresholdElevFt <= 0.0) return null;
            return thresholdElevFt + CatIAboveThresholdFt;
        }
    }
}
