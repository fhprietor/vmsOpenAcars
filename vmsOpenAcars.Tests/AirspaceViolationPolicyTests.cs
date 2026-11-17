using Microsoft.VisualStudio.TestTools.UnitTesting;
using vmsOpenAcars.Helpers;

namespace vmsOpenACars.Tests
{
    /// <summary>
    /// **Qué zonas acotadas mandan el PIREP a revisión.** Lo pidió phpVMS en el mensaje del
    /// 16/11/2026: solo cuentan `Restricted` y `Prohibited`; **`Danger` se registra pero no avisa**.
    ///
    /// El caso medido por ellos: de los **10 PIREP** que el cliente marcó, **2 eran `Danger`**, o sea
    /// que dos vuelos fueron a revisión sin haber invadido nada. Y degradar sin datos es la otra
    /// mitad: un tipo que no llega (o vacío) **no** es una falta.
    /// </summary>
    [TestClass]
    public class AirspaceViolationPolicyTests
    {
        [TestMethod]
        public void SoloRestringidaYProhibidaSonViolacion()
        {
            Assert.IsTrue(AirspaceViolationPolicy.IsViolation("Restricted"));
            Assert.IsTrue(AirspaceViolationPolicy.IsViolation("Prohibited"));

            // El tipo llega de NavData y no siempre con las mismas mayúsculas.
            Assert.IsTrue(AirspaceViolationPolicy.IsViolation("restricted"));
            Assert.IsTrue(AirspaceViolationPolicy.IsViolation(" prohibited "));
        }

        [TestMethod]
        public void DangerNoEsUnaViolacion()
        {
            Assert.IsFalse(AirspaceViolationPolicy.IsViolation("Danger"),
                "Danger es advertencia: en la auditoría de 10 PIREP marcados, 2 eran Danger");
            Assert.IsTrue(AirspaceViolationPolicy.IsAdvisory("Danger"),
                "pero el aviso en pantalla sí se pinta");
            Assert.IsFalse(AirspaceViolationPolicy.IsAdvisory("Restricted"));
        }

        [TestMethod]
        public void LoQueNoEsZonaAcotadaNoEsViolacion()
        {
            Assert.IsFalse(AirspaceViolationPolicy.IsViolation("CTR"));
            Assert.IsFalse(AirspaceViolationPolicy.IsViolation("TMA"));
            Assert.IsFalse(AirspaceViolationPolicy.IsViolation("CTA"));
        }

        [TestMethod]
        public void SinDatoNoSeMarcaNada()
        {
            Assert.IsFalse(AirspaceViolationPolicy.IsViolation(null));
            Assert.IsFalse(AirspaceViolationPolicy.IsViolation(""));
            Assert.IsFalse(AirspaceViolationPolicy.IsViolation("   "));
        }
    }
}
