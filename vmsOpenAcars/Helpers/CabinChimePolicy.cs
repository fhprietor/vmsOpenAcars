namespace vmsOpenAcars.Helpers
{
    /// <summary>
    /// **Qué PA lleva el chime de aviso previo y cuál no.** Todos los anuncios de cabina lo llevan
    /// —es lo que separa una locución de una conversación— menos el de pushback.
    ///
    /// Medido en el PIREP real `3b0oBlXD5ZolKPM4` (AT76, SKUC→SKBO, 05/11/2026): el chime de
    /// `Resources/Audio/chime_warning.wav` dura **2,93 s**, y con el freno liberado a **01:57:06** el
    /// log dejaba la voz a **01:57:09** (+3,0 s). En un PA que tiene que sonar **antes** de que el
    /// avión se mueva, casi tres segundos son la diferencia entre avisar y narrar lo que ya pasó.
    /// La decisión vive en un helper puro para poder probarla sin tarjeta de sonido.
    /// </summary>
    internal static class CabinChimePolicy
    {
        /// <summary>El único PA que se locuta directamente, sin chime.</summary>
        internal static bool ShouldPlayChime(string phase) => phase != "pushback";
    }
}
