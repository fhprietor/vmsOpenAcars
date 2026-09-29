using Microsoft.VisualStudio.TestTools.UnitTesting;
using vmsOpenAcars.Helpers;

namespace vmsOpenAcars.Tests
{
    /// <summary>
    /// La clave de CARTO en la URL de teselas. El formato lo documenta CARTO —mismo endpoint con el
    /// parámetro `key`— y es lo que quita la marca de agua «API key required» que aparece desde que
    /// retiraron el acceso sin clave. Ver <see cref="CartoTileUrl"/>.
    /// </summary>
    [TestClass]
    public class CartoTileUrlTests
    {
        private const string DarkTile =
            "https://a.basemaps.cartocdn.com/dark_all/10/300/400.png";

        [TestMethod]
        public void WithKey_AddsTheKeyAsDocumentedByCarto()
        {
            Assert.AreEqual(DarkTile + "?key=abc123", CartoTileUrl.WithKey(DarkTile, "abc123"));
            Assert.AreEqual(DarkTile + "?key=abc123", CartoTileUrl.WithKey(DarkTile, "  abc123  "),
                            "la clave se recorta antes de usarla");
        }

        [TestMethod]
        public void WithKey_WithoutAKey_LeavesTheUrlAlone()
        {
            // Sin clave el mapa se ve con la marca de agua, pero no se rompe: no se inventa nada.
            Assert.AreEqual(DarkTile, CartoTileUrl.WithKey(DarkTile, ""));
            Assert.AreEqual(DarkTile, CartoTileUrl.WithKey(DarkTile, null));
            Assert.AreEqual(DarkTile, CartoTileUrl.WithKey(DarkTile, "   "));
        }

        [TestMethod]
        public void WithKey_OnAUrlThatAlreadyHasAQuery_UsesAmpersand()
        {
            Assert.AreEqual(DarkTile + "?v=2&key=k1", CartoTileUrl.WithKey(DarkTile + "?v=2", "k1"));
        }

        [TestMethod]
        public void WithKey_EscapesWhatNeedsEscaping()
        {
            // Una clave con caracteres raros no puede romper la consulta.
            Assert.AreEqual(DarkTile + "?key=a%26b", CartoTileUrl.WithKey(DarkTile, "a&b"));
        }
    }
}
