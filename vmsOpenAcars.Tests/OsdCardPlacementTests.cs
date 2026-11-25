using System.Drawing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using vmsOpenAcars.Helpers;

namespace vmsOpenAcars.Tests
{
    /// <summary>
    /// **La tarjeta cabe en las dos resoluciones que pidió el mantenedor.** A 96 DPI mide
    /// 720×200 y a 192 DPI (4K al 200 %) 1440×400; el anclaje la centra y la pega al borde si
    /// algo no cupiera, en vez de dejar media tarjeta fuera del monitor.
    /// </summary>
    [TestClass]
    public class OsdCardPlacementTests
    {
        [TestMethod]
        public void ElTamanoEscalaConElDpi()
        {
            Assert.AreEqual(new Size(720, 200),  OsdCardPlacement.CardSize(96));
            Assert.AreEqual(new Size(1440, 400), OsdCardPlacement.CardSize(192));
        }

        [TestMethod]
        public void CabeEn1920x1080YEn3840x2160()
        {
            // 1080p al 100 %.
            var fullHd = new Size(1920, 1080);
            var cardHd = OsdCardPlacement.CardSize(96);
            var atHd   = OsdCardPlacement.CenterTop(cardHd, fullHd, 96);
            Assert.IsTrue(atHd.X >= 0 && atHd.Y >= 0);
            Assert.IsTrue(atHd.X + cardHd.Width  <= fullHd.Width);
            Assert.IsTrue(atHd.Y + cardHd.Height <= fullHd.Height);

            // 4K al 200 %: la ventana no se sale por ningún lado.
            var uhd = new Size(3840, 2160);
            var cardUhd = OsdCardPlacement.CardSize(192);
            var atUhd   = OsdCardPlacement.CenterTop(cardUhd, uhd, 192);
            Assert.AreEqual(new Size(1440, 400), cardUhd);
            Assert.IsTrue(atUhd.X >= 0 && atUhd.Y >= 0);
            Assert.IsTrue(atUhd.X + cardUhd.Width  <= uhd.Width);
            Assert.IsTrue(atUhd.Y + cardUhd.Height <= uhd.Height);

            // Y 4K al 100 %, que es el caso de un monitor grande sin escalado.
            var card4K = OsdCardPlacement.CardSize(96);
            var at4K   = OsdCardPlacement.CenterTop(card4K, uhd, 96);
            Assert.IsTrue(at4K.X + card4K.Width  <= uhd.Width);
            Assert.IsTrue(at4K.Y + card4K.Height <= uhd.Height);
        }
    }
}

