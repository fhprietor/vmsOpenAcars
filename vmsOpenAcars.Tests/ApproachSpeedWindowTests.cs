using Microsoft.VisualStudio.TestTools.UnitTesting;
using vmsOpenAcars.Helpers;
using vmsOpenAcars.Models;

namespace vmsOpenAcars.Tests
{
    /// <summary>
    /// **La ventana de velocidad de la compuerta de 1.000 ft AGL.** Hasta v0.9.70 salía solo del
    /// tipo de avión, y eso contradice el Vref del plan que el cliente ya publica.
    ///
    /// El caso que abrió esto es **real y medido**: PIREP ESSA→LIRF, **A339**, `vref_kt = 136`,
    /// `landing_weight_lbs = 400.100`; el piloto iba a **137 kt** (Vref+1). La tabla por categoría da
    /// **[140–185] kt** para cualquier wide-body, así que la ventana estaba **3 kt por encima de su
    /// propio Vref** y el gate cobró −5 por «fuera de [140–185]». phpVMS lo reportó el 11/10/2026.
    ///
    /// Cada frontera se prueba en sus dos lados, y el camino sin Vref se fija aparte: **degradar sin
    /// datos** no es opcional — un plan sin TLR (o un vuelo retomado, que no tiene OFP) no puede
    /// quedarse sin ventana.
    /// </summary>
    [TestClass]
    public class ApproachSpeedWindowTests
    {
        private const string A339 = "A339";   // wide-body: la tabla da 140–185
        private const string Atr  = "AT76";   // turboprop: la tabla da 90–135

        // ── Con Vref: la ventana es del peso ──────────────────────────────────────

        /// <summary>
        /// El caso que originó el cambio: **137 kt con Vref 136 son 136 + 1**, dentro de
        /// `[131–151]`. Con la ventana del tipo (140–185) quedaba fuera y costaba −5.
        /// </summary>
        [TestMethod]
        public void ElCasoReal_Vref136_DejaPasarLos137KtQueAntesPenalizaba()
        {
            var (min, max) = ApproachSpeedWindow.For(136, A339);

            Assert.AreEqual(131, min, "Vref − 5");
            Assert.AreEqual(151, max, "Vref + 15");
            Assert.IsTrue(137 >= min && 137 <= max,
                "137 kt es Vref+1: el vuelo que abrió esto NO puede salir penalizado");

            // Y la ventana vieja, para que se vea el fallo que se corrige: el mismo avión, el mismo
            // pilotaje, 3 kt por encima del Vref del plan.
            var (oldMin, oldMax) = AircraftPerformanceTable.GetApproachSpeedRange(A339);
            Assert.AreEqual(140, oldMin, "la tabla por tipo sigue dando 140–185");
            Assert.IsFalse(137 >= oldMin,
                "con la tabla por tipo, 137 kt caía FUERA por abajo: ese es el −5 que se corrige");
        }

        /// <summary>Las dos fronteras, en sus dos lados: `131` entra y `130` no; `151` entra y `152` no.</summary>
        [DataTestMethod]
        [DataRow(130, false, "1 kt por debajo del margen")]
        [DataRow(131, true,  "el borde inferior entra")]
        [DataRow(151, true,  "el borde superior entra")]
        [DataRow(152, false, "1 kt por encima del margen")]
        public void LasFronteras_SonInclusivas(int ias, bool inside, string because)
        {
            var (min, max) = ApproachSpeedWindow.For(136, A339);
            Assert.AreEqual(inside, ias >= min && ias <= max, because);
        }

        // ── Sin Vref: la tabla de siempre, intacta ────────────────────────────────

        /// <summary>
        /// Sin plan (o sin TLR) no hay Vref, y entonces la ventana **es la de antes**, sin cambios:
        /// un wide-body sigue en 140–185 y un turboprop en 90–135. Si esto se moviera, cambiaría la
        /// puntuación de todos los vuelos sin OFP sin que nadie lo hubiera pedido.
        /// </summary>
        [TestMethod]
        public void SinVref_MandaLaTablaPorTipo()
        {
            Assert.AreEqual((140, 185), ApproachSpeedWindow.For(null, A339));
            Assert.AreEqual((90, 135),  ApproachSpeedWindow.For(null, Atr));
            // Tipo desconocido: el defecto genérico de la tabla, no una excepción.
            Assert.AreEqual((140, 185), ApproachSpeedWindow.For(null, "ZZZZ"));
        }

        // ── Vref implausible: no se construye una ventana sin sentido ────────────

        /// <summary>
        /// Un «Vref 5» daría `[0–20]` kt y un «Vref 900» daría `[895–915]`: los dos son números que
        /// no son un Vref (campo en otra unidad, plan corrupto). Ahí se degrada a la tabla, que es lo
        /// que dicta la regla de la casa — **no se suprime la comprobación ni se inventa la ventana**.
        /// </summary>
        [DataTestMethod]
        [DataRow(0,   "cero")]
        [DataRow(-20, "negativo")]
        [DataRow(5,   "demasiado bajo para ser un Vref")]
        [DataRow(59,  "justo por debajo del rango plausible")]
        [DataRow(201, "justo por encima del rango plausible")]
        [DataRow(900, "absurdo")]
        public void UnVrefImplausible_DegradaALaTabla(int vref, string because)
        {
            Assert.IsFalse(ApproachSpeedWindow.IsUsable(vref), because);
            Assert.AreEqual((140, 185), ApproachSpeedWindow.For(vref, A339), because);
        }

        /// <summary>Los bordes del rango plausible sí valen: 60 y 200 son Vref de avión real.</summary>
        [TestMethod]
        public void LosBordesDelRangoPlausible_SonUsables()
        {
            Assert.IsTrue(ApproachSpeedWindow.IsUsable(60));
            Assert.IsTrue(ApproachSpeedWindow.IsUsable(200));
            Assert.AreEqual((55, 75),   ApproachSpeedWindow.For(60,  A339));
            Assert.AreEqual((195, 215), ApproachSpeedWindow.For(200, Atr),
                "con Vref manda el Vref aunque el tipo sea otro: 200 kt en un ATR es raro, pero la ventana es del peso");
        }
    }
}
