using System;
using System.Drawing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using vmsOpenAcars.Helpers;
using vmsOpenAcars.UI;
using vmsOpenAcars.UI.Forms;

namespace vmsOpenAcars.Tests
{
    /// <summary>
    /// **El pintado se prueba con un `Bitmap`, nunca con el `Form`.** Instanciar `OsdOverlayForm`
    /// crearía una ventana real en la máquina de tests; por eso el renderer vive aparte y aquí se
    /// comprueba lo que de verdad importa: que la tarjeta tiene su tamaño, que las esquinas
    /// redondeadas dejan el píxel de fuera transparente y que dentro hay texto pintado.
    /// </summary>
    [TestClass]
    public class OsdCardRendererTests
    {
        [TestMethod]
        public void LaTarjetaDelToque_SePintaConEsquinasRedondeadas()
        {
            var payload = new OsdCardText.OsdCardPayload
            {
                Severity       = OsdSeverity.Success,
                Icao           = "SKCG",
                AirportName    = "Rafael Núñez",
                TouchdownUtc   = new DateTime(2026, 11, 24, 3, 39, 0, DateTimeKind.Utc),
                LandingRateFpm = -219,
                GForce         = 1.21,
                IasKt          = 138,
                GroundSpeedKt  = 141,
                PitchDeg       = 3.5,
                BankDeg        = 0.0,
            };

            using (var bmp = OsdCardRenderer.RenderCard(payload, 96))
            {
                Assert.AreEqual(720, bmp.Width);
                Assert.AreEqual(200, bmp.Height);
                Assert.AreEqual(0,   bmp.GetPixel(0, 0).A, "la esquina queda fuera de la Region");
                Assert.AreEqual(255, bmp.GetPixel(bmp.Width / 2, bmp.Height / 2).A);
                Assert.IsTrue(TieneTextoClaro(bmp), "la tarjeta no puede salir vacía");
            }
        }

        [TestMethod]
        public void LaPlantillaSimple_TambienSePintaYSeMideSola()
        {
            Size size = OsdCardRenderer.MeasureSimple("PENALTY  NAV LIGHTS  −5 PTS",
                                                      OsdSeverity.Warning, 96);
            Assert.IsTrue(size.Width >= 420, "el ancho mínimo mantiene la tarjeta legible");
            Assert.AreEqual(90, size.Height);

            using (var bmp = OsdCardRenderer.RenderSimple("PENALTY  NAV LIGHTS  −5 PTS",
                                                          OsdSeverity.Warning, 96))
            {
                Assert.AreEqual(size, bmp.Size);
                Assert.AreEqual(0, bmp.GetPixel(0, 0).A);
                Assert.IsTrue(TieneTextoClaro(bmp));
            }
        }

        [TestMethod]
        public void LaTarjetaEscalaAlDpiReal()
        {
            var payload = new OsdCardText.OsdCardPayload { Icao = "EHAM" };
            using (var bmp = OsdCardRenderer.RenderCard(payload, 192))
            {
                Assert.AreEqual(1440, bmp.Width);
                Assert.AreEqual(400,  bmp.Height);
            }
        }

        /// <summary>¿Hay algún píxel casi blanco? Es la firma de que el texto se dibujó.</summary>
        private static bool TieneTextoClaro(Bitmap bmp)
        {
            for (int y = 0; y < bmp.Height; y += 2)
            for (int x = 0; x < bmp.Width; x += 2)
            {
                Color c = bmp.GetPixel(x, y);
                if (c.A > 200 && c.R > 180 && c.G > 180 && c.B > 180) return true;
            }
            return false;
        }
    }
}

