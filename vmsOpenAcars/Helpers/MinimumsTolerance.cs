namespace vmsOpenAcars.Helpers
{
    /// <summary>
    /// **La bajada de mínimos se marca con banda, no al pie del número.**
    ///
    /// En el PIREP real `YxzZxO92Qnbxlqla` (B738 SSW, MSFS 2024, MPTO→SKCG, v0.9.79) el aviso saltó
    /// por **1 ft**: `206 ft MSL < DA 207 ft (threshold 7 ft + 200)` a las 03:38:53. Los 207 ft son
    /// DA = elevación del umbral (7 ft) + 200 ft CAT I, y 206 ft es una lectura del altímetro, cuya
    /// resolución en el simulador es de unos **10 ft**: la diferencia de 1 ft es el escalón del
    /// instrumento, no una violación. Sin banda, ese aviso saltaría en **todos** los aterrizajes que
    /// crucen la DA con el datum un pie bajo.
    ///
    /// **Por qué 15 ft.** Es 1,5 pasos de la cuantización de ~10 ft: absorbe el redondeo del
    /// altímetro y del datum del escenario sin tapar una bajada real. El umbral efectivo es
    /// `DA − 15 ft` (207 → **192 ft**): a 206 ft no avisa, a 185 ft (22 ft por debajo) sí. El lado
    /// de arriba no se toca: la DA nominal sigue siendo la misma y el aviso solo puede dispararse
    /// **más abajo** que antes, nunca antes.
    ///
    /// Se compara con `<` estricto contra el umbral efectivo, así que una diferencia de exactamente
    /// 15 ft no se marca: el criterio es «**más de** 15 ft por debajo».
    /// </summary>
    internal static class MinimumsTolerance
    {
        /// <summary>
        /// Banda de tolerancia en pies. El porqué de 15 —1,5 pasos del altímetro de ~10 ft— está en
        /// el comentario de la clase, con el caso real de 206 contra 207 ft.
        /// </summary>
        internal const double Ft = 15.0;

        /// <summary>La DA efectiva: la nominal menos la banda. Es la cifra que publica el mensaje.</summary>
        internal static double EffectiveDaFt(double daFt) => daFt - Ft;

        /// <summary>
        /// ¿La altitud está **más de <see cref="Ft"/> pies por debajo** de la DA? La igualdad en el
        /// borde no cuenta: el criterio es estrictamente «más abajo».
        /// </summary>
        internal static bool IsBelowMinimums(double altitudeMslFt, double daFt)
            => altitudeMslFt < EffectiveDaFt(daFt);
    }
}
