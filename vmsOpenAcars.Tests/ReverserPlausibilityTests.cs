using Microsoft.VisualStudio.TestTools.UnitTesting;
using vmsOpenAcars.Helpers;

namespace vmsOpenAcars.Tests
{
    /// <summary>
    /// **La reversa del rodaje no es una reversa.** Caso real: PIREP **`ez9OqzRnYxbnGLR8`** (B738
    /// PMDG, MSFS 2024, SKBO→SKBG).
    ///
    /// ```
    /// 02:26:15  Reversa: desplegada  ENG1 11%  ENG2 2%              ← el toque. VÁLIDO.
    /// 02:26:37  Reversa: máximo del rodaje  ENG1 100.0%  ENG2 100.0%  ← imposible
    ///           — cool-down 180s requeridos
    /// ```
    ///
    /// El 100 % de las 02:26:37 llegó **22 s después del toque y con el avión ya rodando**: es el
    /// offset del PMDG publicando otra cosa, no un despliegue. La regla lo ignora por dos puertas
    /// —velocidad de la carrera de frenada y ventana tras el toque— y el 11 % / 2 % del toque sigue
    /// valiendo, que es lo que no se puede romper.
    ///
    /// Las dos cifras de velocidad son **las de los dos momentos** del mismo vuelo: la carrera de
    /// frenada de un 737 en la 14L de SKBG va de ~140 kt al toque a ~40 kt con las reversas ya
    /// guardadas, y el rodaje de llegada a ese campo se hace a 10–20 kt.
    /// </summary>
    [TestClass]
    public class ReverserPlausibilityTests
    {
        /// <summary>El despliegue real del toque: 140 kt y 0 s. **Tiene que valer.**</summary>
        [TestMethod]
        public void ElDespliegueDelToque_11PorCiento_EsPlausible()
        {
            Assert.IsTrue(ReverserPlausibility.IsPlausibleDeployment(140, 0),
                "el 11 % / 2 % de las 02:26:15 es la reversa de verdad");
        }

        /// <summary>
        /// El caso malo: 100 % a **12 kt** y 22 s después del toque. La velocidad sola ya lo tumba;
        /// y con la ventana de 30 s tampoco pasaría mucho después.
        /// </summary>
        [TestMethod]
        public void El100PorCientoRodando_NoEsUnDespliegue()
        {
            Assert.IsFalse(ReverserPlausibility.IsPlausibleDeployment(12, 22),
                "a 12 kt nadie lleva las reversas desplegadas: es el offset del PMDG");
        }

        // ── Las dos puertas, en sus bordes ───────────────────────────────────────

        [DataTestMethod]
        [DataRow(39.9, false, 0,     "justo por debajo del suelo de velocidad")]
        [DataRow(40.0, true,  0,     "el suelo de 40 kt entra")]
        [DataRow(40.1, true,  0,     "por encima del suelo")]
        [DataRow(120.0, true, 0,     "carrera de frenada normal")]
        public void LaVelocidad_DecideEnElSueloDe40Nudos(double gs, bool plausible, double secs, string because)
        {
            Assert.AreEqual(plausible, ReverserPlausibility.IsPlausibleDeployment(gs, secs), because);
        }

        [DataTestMethod]
        [DataRow(80.0, 29.9, true,  "dentro de la ventana de 30 s")]
        [DataRow(80.0, 30.0, true,  "el borde de 30 s entra")]
        [DataRow(80.0, 31.0, false, "pasados los 30 s ya se registró el máximo: es ruido")]
        public void LaVentanaTrasElToque_EsDe30Segundos(double gs, double secs, bool plausible, string because)
        {
            Assert.AreEqual(plausible, ReverserPlausibility.IsPlausibleDeployment(gs, secs), because);
        }

        // ── Degradar sin datos ───────────────────────────────────────────────────

        /// <summary>
        /// Sin GS o sin instante de toque **no se bloquea**: la regla de la casa es que un filtro de
        /// plausibilidad no puede suprimir una acción por una suposición. Si el dato falta, deciden
        /// las otras reglas (el umbral de 10 % y el cool-down).
        /// </summary>
        [TestMethod]
        public void SinDatos_NoBloquea()
        {
            Assert.IsTrue(ReverserPlausibility.IsPlausibleDeployment(double.NaN, 10),
                "sin GS, decide el resto de la cadena");
            Assert.IsTrue(ReverserPlausibility.IsPlausibleDeployment(120, double.MaxValue),
                "sin instante de toque, decide el resto de la cadena");
            Assert.IsTrue(ReverserPlausibility.IsPlausibleDeployment(120, double.NaN));
        }
    }
}
