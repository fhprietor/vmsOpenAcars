using Microsoft.VisualStudio.TestTools.UnitTesting;
using vmsOpenAcars.Helpers;

namespace vmsOpenAcars.Tests
{
    /// <summary>
    /// Las dos tablas del autobrake, **copiadas del documento de FSUIPC7** (`FSUIPC7 Offsets
    /// Status.pdf`), que es lo que el cliente no hacía: leía `0x0C46` (`AUTOBRAKES ACTIVE`, una bandera
    /// de «está actuando» y sin `Responded` en MSFS) con la tabla del **selector**, y encima el enum y
    /// `GetAutobrakeName` tenían el **0 y el 1 cambiados**. Con las dos equivocaciones compensándose,
    /// el registro decía `Autobrake: RTO` en **todos** los vuelos.
    /// </summary>
    [TestClass]
    public class AutobrakeSelectorTests
    {
        [TestMethod]
        public void LaTablaDelPanelEsLaDelDocumento()
        {
            // 0x2F80 · "Panel autobrake switch": 0=RTO, 1=Off, 2=brake1, 3=brake2, 4=brake3, 5=max.
            Assert.AreEqual("RTO", AutobrakeSelector.PanelLabel(0));
            Assert.AreEqual("OFF", AutobrakeSelector.PanelLabel(1));
            Assert.AreEqual("1",   AutobrakeSelector.PanelLabel(2));
            Assert.AreEqual("2",   AutobrakeSelector.PanelLabel(3));
            Assert.AreEqual("3",   AutobrakeSelector.PanelLabel(4));
            Assert.AreEqual("MAX", AutobrakeSelector.PanelLabel(5));
        }

        [TestMethod]
        public void ElCeroEsRtoYNoOff()
        {
            // Este es el par que estaba invertido: el valor 0 **no** es «Off».
            Assert.AreEqual("RTO", AutobrakeSelector.PanelLabel(0));
            Assert.AreNotEqual(AutobrakeSelector.PanelLabel(0), AutobrakeSelector.PanelLabel(1));
        }

        [TestMethod]
        public void UnValorFueraDeTablaSePublicaCrudo()
        {
            Assert.AreEqual("RAW=9", AutobrakeSelector.PanelLabel(9),
                "mejor decir lo que se leyó que inventar una posición");
            Assert.IsFalse(AutobrakeSelector.IsKnown(AutobrakeSelector.PanelLabel(9)));
        }

        [TestMethod]
        public void LaTablaDelA320NeoVaAparte()
        {
            // 0x0260 · "Current Autobrake setting for A320Neo only": 0=Off, 1=Lo, 2=Med, 3=Max.
            Assert.AreEqual("OFF", AutobrakeSelector.AirbusLabel(0));
            Assert.AreEqual("LO",  AutobrakeSelector.AirbusLabel(1));
            Assert.AreEqual("MED", AutobrakeSelector.AirbusLabel(2));
            Assert.AreEqual("MAX", AutobrakeSelector.AirbusLabel(3));
            Assert.AreEqual("RAW=4", AutobrakeSelector.AirbusLabel(4));
        }

        [TestMethod]
        public void SinDatoNoEsUnAjuste()
        {
            Assert.IsFalse(AutobrakeSelector.IsKnown(AutobrakeSelector.NoData));
            Assert.IsFalse(AutobrakeSelector.IsKnown(null));
            Assert.IsFalse(AutobrakeSelector.IsKnown(""));
            Assert.IsTrue(AutobrakeSelector.IsKnown("RTO"));
            Assert.IsTrue(AutobrakeSelector.IsKnown("MAX"));
        }
    }
}
