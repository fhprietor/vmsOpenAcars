using System;
using System.Windows.Forms;
using GMap.NET.MapProviders;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using vmsOpenAcars.Helpers;
using vmsOpenAcars.UI.Forms;

namespace vmsOpenAcars.Tests
{
    /// <summary>
    /// Crédito de las teselas del mapa. Es una **obligación de las fuentes** —las condiciones de las
    /// basemaps de CARTO exigen acreditar «CARTO and OpenStreetMap» en cada mapa, y las de ESRI su
    /// imagen—, y hasta v0.9.16 el cliente no mostraba ninguno. Estos tests son la garantía de que
    /// ningún proveedor del combo pueda quedarse sin crédito.
    /// </summary>
    [TestClass]
    public class MapAttributionTests
    {
        [TestMethod]
        public void For_UsesTheProviderOwnCredit()
        {
            Assert.AreEqual("© OpenStreetMap contributors · © CARTO",
                            MapAttribution.For("© OpenStreetMap contributors · © CARTO"));
            Assert.AreEqual("© Esri", MapAttribution.For("  © Esri  "));
        }

        [TestMethod]
        public void For_WithoutCredit_CreditsEverySource()
        {
            // Sin crédito se acredita a todas las fuentes: acreditar de más no incumple, de menos sí.
            Assert.AreEqual(MapAttribution.Fallback, MapAttribution.For(null));
            Assert.AreEqual(MapAttribution.Fallback, MapAttribution.For(""));
            Assert.AreEqual(MapAttribution.Fallback, MapAttribution.For("   "));
        }

        [TestMethod]
        public void EveryMapProviderOfTheCombo_CarriesItsOwnCredit()
        {
            var providers = new GMapProvider[]
            {
                CartoLightProvider.Instance,     // Street (Carto)
                CartoDarkProvider.Instance,      // Dark (Carto) — el del combo por defecto
                EsriSatelliteProvider.Instance,  // Satellite (ESRI)
            };

            foreach (var p in providers)
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(p.Copyright),
                    $"el proveedor '{p.Name}' no publica crédito y el rótulo del mapa quedaría vacío");
                Assert.AreEqual(p.Copyright, MapAttribution.For(p.Copyright),
                    $"'{p.Name}' debe enseñar su propio crédito, no el de reserva");
            }
        }

        [TestMethod]
        public void CartoProviders_CreditBothSourcesThatTheirTermsRequire()
        {
            StringAssert.Contains(MapAttribution.CartoCredit, "OpenStreetMap");
            StringAssert.Contains(MapAttribution.CartoCredit, "CARTO");
            Assert.AreEqual(MapAttribution.CartoCredit, CartoDarkProvider.Instance.Copyright,
                "los dos estilos de CARTO son las mismas basemaps y acreditan igual");
        }

        [TestMethod]
        public void AttributionLabel_IsTransparentToTheMouse()
        {
            // El rótulo va encima del control del mapa, en la esquina inferior derecha. Si se comiera
            // los clics, **esa esquina dejaría de arrastrar el mapa**: por eso responde
            // `HTTRANSPARENT` (−1) al test de impacto. Se comprueba sin abrir ninguna ventana.
            const int WM_NCHITTEST = 0x0084;

            using (var label = new AttributionLabel())
            {
                var wndProc = typeof(AttributionLabel).GetMethod(
                    "WndProc",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                Assert.IsNotNull(wndProc, "AttributionLabel tiene que sobrescribir WndProc");

                var args = new object[] { new Message { Msg = WM_NCHITTEST } };
                wndProc.Invoke(label, args);

                // `WndProc` recibe el Message por referencia: la invocación por reflexión escribe el
                // resultado de vuelta en el array de argumentos.
                var result = (Message)args[0];
                Assert.AreEqual(new IntPtr(-1), result.Result,
                    "el test de impacto tiene que atravesar el rótulo para que el mapa siga arrastrándose");
            }
        }
    }
}
