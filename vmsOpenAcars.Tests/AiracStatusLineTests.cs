using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using vmsOpenAcars.Helpers;

namespace vmsOpenAcars.Tests
{
    /// <summary>
    /// **La fecha de vigencia del AIRAC que se pinta en la línea de arranque**, en `yyyy-MM-dd`.
    ///
    /// El campo `airac_valid_until` de NavData **no tiene formato fijado por contrato**: el cliente lo
    /// compara con `DateTime.TryParse` y en las respuestas se ha visto tanto `2026-11-12` como un
    /// instante ISO completo. Por eso lo que se prueba aquí es la **tolerancia del helper**, no una
    /// respuesta de NavData: no hay fixture de la API, porque el formato del campo puede cambiar sin
    /// que nadie avise.
    ///
    /// El formato de salida no es cosmético: `12/11/2026` es el 12 de noviembre en España y el 11 de
    /// diciembre en Estados Unidos. `2026-11-12` se lee igual desde los dos lados del Atlántico.
    /// </summary>
    [TestClass]
    public class AiracStatusLineTests
    {
        [TestMethod]
        public void FechaIsoSimple_SaleTalCual()
        {
            Assert.AreEqual("2026-11-12", AiracStatusLine.FormatValidUntil("2026-11-12"));
        }

        [TestMethod]
        public void IsoConHoraYZ_ConservaElDiaUtc()
        {
            // Un `DateTime` con `DateTimeStyles.None` convertiría el instante a la hora local del
            // equipo y en un huso al oeste de Greenwich devolvería 2026-11-11, un día antes del real.
            // El helper usa `DateTimeOffset` justo para que esto no dependa de dónde corra el test.
            Assert.AreEqual("2026-11-12", AiracStatusLine.FormatValidUntil("2026-11-12T00:00:00Z"));
            Assert.AreEqual("2026-11-12", AiracStatusLine.FormatValidUntil("2026-11-12T23:59:59Z"));
        }

        [TestMethod]
        public void IsoConDesplazamientoHorario_NoCambiaDeDia()
        {
            // NavData podría publicar el instante con su propio offset; la fecha que interesa es la
            // del texto, no la que resultaría de normalizarla a UTC.
            Assert.AreEqual("2026-11-12", AiracStatusLine.FormatValidUntil("2026-11-12T00:00:00+02:00"));
        }

        [TestMethod]
        public void SinDato_DevuelveVacio()
        {
            // El que llama decide entonces la variante sin fecha: nunca se imprime «hasta » vacío.
            Assert.AreEqual(string.Empty, AiracStatusLine.FormatValidUntil(null));
            Assert.AreEqual(string.Empty, AiracStatusLine.FormatValidUntil(string.Empty));
            Assert.AreEqual(string.Empty, AiracStatusLine.FormatValidUntil("   "));
        }

        [DataTestMethod]
        [DataRow("n/a",        DisplayName = "El relleno que se usa cuando el campo no viene")]
        [DataRow("?",          DisplayName = "Un interrogante suelto")]
        [DataRow("--",         DisplayName = "Guiones sin fecha")]
        [DataRow("2026-13-45", DisplayName = "Números con forma de fecha pero fuera de rango")]
        [DataRow("no aplica",  DisplayName = "Texto libre")]
        public void Basura_DevuelveVacioSinLanzar(string raw)
        {
            // Degradar sin dato, no reventar la línea de arranque: esta llamada va dentro del Task que
            // informa de NavData y una excepción ahí se llevaría por delante el aviso entero.
            Assert.AreEqual(string.Empty, AiracStatusLine.FormatValidUntil(raw));
        }

        [DataTestMethod]
        [DataRow("2026-11-12")]
        [DataRow("2026-11-12T00:00:00Z")]
        [DataRow("2026-01-01")]
        [DataRow("2027-12-31T23:59:59Z")]
        public void CuandoParsea_ElResultadoSiempreSonDiezCaracteres(string raw)
        {
            // `yyyy-MM-dd` mide 10: si alguien cambiara el formato de salida por uno dependiente de la
            // cultura, el ancho cambiaría y este test lo diría.
            string formatted = AiracStatusLine.FormatValidUntil(raw);
            Assert.AreEqual(10, formatted.Length, $"«{raw}» no salió en yyyy-MM-dd: «{formatted}»");
            Assert.AreEqual(formatted, DateTime.ParseExact(formatted, "yyyy-MM-dd",
                System.Globalization.CultureInfo.InvariantCulture).ToString("yyyy-MM-dd"));
        }
    }
}
