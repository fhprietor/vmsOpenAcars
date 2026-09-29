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

        // ── El proxy de NavData (v0.9.18): es el camino, CARTO es el respaldo ────────────────────

        private const string NavApi = "https://navdata.vholar.co/api/v1/";

        [TestMethod]
        public void ProxyBase_AddsTheTilesRouteToTheApiBase()
        {
            Assert.AreEqual("https://navdata.vholar.co/api/v1/tiles",
                            CartoTileUrl.ProxyBase(NavApi, ""));
            Assert.AreEqual("https://navdata.vholar.co/api/v1/tiles",
                            CartoTileUrl.ProxyBase(NavApi, null),
                            "el valor de la API se recorta antes de componer");
        }

        [TestMethod]
        public void ProxyBase_DoesNotDuplicateTheTilesRoute()
        {
            // Un `tile_proxy_url` que ya apunta a la ruta de teselas se respeta tal cual.
            Assert.AreEqual("https://otro.example/tiles",
                            CartoTileUrl.ProxyBase(NavApi, "https://otro.example/tiles/"));
            Assert.AreEqual("https://otro.example/tiles",
                            CartoTileUrl.ProxyBase(NavApi, "https://otro.example/tiles"));
        }

        [TestMethod]
        public void ProxyBase_WithoutAnApiUrl_IsEmpty()
        {
            // Sin base no hay proxy que intentar: el llamante va directo a CARTO.
            Assert.AreEqual("", CartoTileUrl.ProxyBase("", ""));
            Assert.AreEqual("", CartoTileUrl.ProxyBase(null, null));
        }

        [TestMethod]
        public void ProxyTile_BuildsTheUrlThatNavDataDocuments()
        {
            // {navdata_api_url}tiles/{style}/{z}/{x}/{y}.png?key=…&origin_domain=…
            Assert.AreEqual(
                "https://navdata.vholar.co/api/v1/tiles/dark_all/14/4736/8200.png?key=vhr-abc&origin_domain=vholar.co",
                CartoTileUrl.ProxyTile("https://navdata.vholar.co/api/v1/tiles", "dark_all",
                                       14, 4736, 8200, "vhr-abc", "vholar.co"));
        }

        [TestMethod]
        public void ProxyTile_WithoutAKey_StillCarriesTheDomain()
        {
            // Con la clave vacía el proxy responde 401 (medido), pero la URL se construye igual: la
            // decisión de intentarlo o no la toma el llamante, no el compositor de la URL.
            Assert.AreEqual(
                "https://navdata.vholar.co/api/v1/tiles/light_all/12/1184/2050.png?origin_domain=vholar.co",
                CartoTileUrl.ProxyTile("https://navdata.vholar.co/api/v1/tiles", "light_all",
                                       12, 1184, 2050, "", "vholar.co"));
        }

        [TestMethod]
        public void ProxyTile_WithoutBaseOrStyle_IsEmpty()
        {
            Assert.AreEqual("", CartoTileUrl.ProxyTile("", "light_all", 1, 1, 1, "k", "d"));
            Assert.AreEqual("", CartoTileUrl.ProxyTile("https://x/tiles", "", 1, 1, 1, "k", "d"));
        }

        [TestMethod]
        public void Mask_HidesTheKeyAndNothingElse()
        {
            // Lo que se registra en el log: la URL sin la credencial, y con el resto intacto para
            // poder diagnosticar (qué estilo, qué tesela, qué dominio).
            string url = "https://navdata.vholar.co/api/v1/tiles/dark_all/14/4736/8200.png" +
                         "?key=vhr-secreto-123&origin_domain=vholar.co";
            string masked = CartoTileUrl.Mask(url);

            Assert.IsFalse(masked.Contains("vhr-secreto-123"), "la clave no puede sobrevivir al enmascarado");
            Assert.IsTrue(masked.Contains("key=***"));
            Assert.IsTrue(masked.Contains("origin_domain=vholar.co"), "el resto se conserva para diagnosticar");
            Assert.IsTrue(masked.Contains("/dark_all/14/4736/8200.png"));
        }

        [TestMethod]
        public void Mask_WithoutAKey_LeavesTheUrlAlone()
        {
            Assert.AreEqual(DarkTile, CartoTileUrl.Mask(DarkTile));
        }
    }
}
