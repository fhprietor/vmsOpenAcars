using System;
using System.Collections.Generic;
using System.Drawing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using vmsOpenAcars.Helpers;
using vmsOpenAcars.UI.Forms;

namespace vmsOpenAcars.Tests
{
    /// <summary>
    /// **La paleta del OSD es una decisión, no un literal suelto.** El OSD dejó de compartir el
    /// cian de consola de la ventana principal: la referencia es una tarjeta oscura con tinte
    /// morado, texto blanco, secundarios grises y un verde de píldora. Estos tests fijan que no
    /// quede ninguna severidad sin color, que los cuatro colores no se repitan y que el rótulo
    /// y el número no compartan tamaño: es lo que hace legible la tarjeta a 1080p y a 4K.
    /// </summary>
    [TestClass]
    public class OsdStyleTests
    {
        [TestMethod]
        public void TodaSeveridadTieneColor()
        {
            foreach (OsdSeverity severity in Enum.GetValues(typeof(OsdSeverity)))
            {
                Color color = OsdStyle.ColorFor(severity);
                Assert.AreNotEqual(0, color.A, $"{severity} no puede quedar sin color");
            }
        }

        [TestMethod]
        public void LosCuatroColoresDeSeveridadSonDistintos()
        {
            var colors = new HashSet<int>();
            foreach (OsdSeverity severity in Enum.GetValues(typeof(OsdSeverity)))
                Assert.IsTrue(colors.Add(OsdStyle.ColorFor(severity).ToArgb()),
                    $"{severity} comparte color con otra severidad");
            Assert.AreEqual(4, colors.Count);
        }

        [TestMethod]
        public void ElRotuloYElNumeroNoCompartenTamano()
        {
            Assert.AreNotEqual(OsdStyle.CaptionPoints, OsdStyle.ValuePoints);
            Assert.IsTrue(OsdStyle.CaptionPoints > 0f);
            Assert.IsTrue(OsdStyle.ValuePoints > OsdStyle.CaptionPoints,
                "el número de la tarjeta tiene que dominar sobre el rótulo");
        }

        [TestMethod]
        public void LaPildoraUsaElColorDeLaBandaDelG()
        {
            Assert.AreEqual(OsdStyle.BadgeGreen,     OsdStyle.BadgeColor(GForceBand.Smooth));
            Assert.AreEqual(OsdStyle.BadgeGreenSoft, OsdStyle.BadgeColor(GForceBand.Firm));
            Assert.AreEqual(OsdStyle.ColorFor(OsdSeverity.Warning),
                            OsdStyle.BadgeColor(GForceBand.Hard));
            Assert.AreEqual(OsdStyle.ColorFor(OsdSeverity.Critical),
                            OsdStyle.BadgeColor(GForceBand.Severe));
        }

        [TestMethod]
        public void LaFamiliaEsSegoeUiYElTrackingExiste()
        {
            Assert.AreEqual("Segoe UI", OsdStyle.FontFamily);
            Assert.IsTrue(OsdStyle.CaptionTrackingPx > 0f);
            Assert.IsTrue(OsdStyle.StatusTrackingPx > 0f);
        }
    }
}

