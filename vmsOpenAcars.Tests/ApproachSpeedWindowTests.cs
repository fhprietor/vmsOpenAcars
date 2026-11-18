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

        // ── La Vapp informativa (no es el criterio) ───────────────────────────────

        /// <summary>
        /// **El caso exacto de phpVMS**: Vref 136 con **4,85 kt en cara** → `1,62 → 2`, que cae en el
        /// suelo de 5 → **Vapp 141**. Y el piloto iba a 137, dentro de `[131–151]`. La Vapp es
        /// informativa: **no** mueve la ventana (eso lo fija
        /// <see cref="ApproachSpeedWindowTests.ElCasoReal_Vref136_DejaPasarLos137KtQueAntesPenalizaba"/>).
        /// </summary>
        [TestMethod]
        public void LaVapp_EsLaFormulaDePhpVms_YNoMueveLaVentana()
        {
            Assert.AreEqual(141, ApproachSpeedWindow.Vapp(136, 4.85).Value);

            // El suelo de 5 manda por debajo de 15 kt de viento en cara (15/3 = 5 justo en el borde).
            Assert.AreEqual(141, ApproachSpeedWindow.Vapp(136, 0).Value,     "sin viento, el suelo de 5");
            Assert.AreEqual(141, ApproachSpeedWindow.Vapp(136, 14.9).Value,  "14,9 kt → 5");
            // Y a partir de ahí, un tercio del viento.
            Assert.AreEqual(146, ApproachSpeedWindow.Vapp(136, 30).Value,    "30 kt → +10");
            Assert.AreEqual(151, ApproachSpeedWindow.Vapp(136, 45).Value,    "45 kt → +15, el techo");
            Assert.AreEqual(151, ApproachSpeedWindow.Vapp(136, 90).Value,    "90 kt sigue en el techo");
            // Redondeo alejándose del cero, no el bancario que trae .NET por defecto: 19,5 kt → 6,5 → **7**
            // (el bancario daría 6, y el aditivo saldría 142 en vez de 143).
            Assert.AreEqual(143, ApproachSpeedWindow.Vapp(136, 19.5).Value);
            // Viento en cola: la fórmula que fijaron también cae en el suelo de 5.
            Assert.AreEqual(141, ApproachSpeedWindow.Vapp(136, -12).Value);

            // Y la ventana NO cambia por tener Vapp: sigue siendo la del Vref.
            Assert.AreEqual((131, 151), ApproachSpeedWindow.For(136, A339));
        }

        /// <summary>Sin Vref o sin viento no hay Vapp: no se inventa. Y un dato roto tampoco.</summary>
        [TestMethod]
        public void SinVrefOSinViento_NoHayVapp()
        {
            Assert.IsNull(ApproachSpeedWindow.Vapp(null, 10), "sin Vref no hay Vapp");
            Assert.IsNull(ApproachSpeedWindow.Vapp(5, 10),    "un Vref implausible tampoco");
            Assert.IsNull(ApproachSpeedWindow.Vapp(136, null), "sin dato de viento, `null`");
            Assert.IsNull(ApproachSpeedWindow.Vapp(136, double.NaN), "un NaN no es un viento");
        }

        // ══ El Vref del plan no siempre es el del avión ═══════════════════════════

        /// <summary>
        /// **La Vapp publicada dice de dónde sale el Vref y nunca baja de él.** Caso real: PIREP
        /// **`ez9OqzRnYxbnGLR8`** (B738 PMDG, MSFS 2024, SKBO→SKBG), `Vref` del plan **139 kt**,
        /// viento en cara que da **Vapp 144**. El FMC del avión calculaba **Vref 150** y se aterrizó
        /// con **FLAPS 25**, no con la configuración del plan: un «144» sin nota se lee como la Vapp
        /// del avión, y es **6 kt por debajo de su Vref**.
        ///
        /// `150/139 = 1,079` en velocidad pediría ×1,16 en peso (~160.000 lb) con 138.492 lb reales:
        /// el peso no lo explica, la configuración sí.
        /// </summary>
        [TestMethod]
        public void LaVappPublicada_DiceQueElVrefEsDelPlanYNuncaBajaDeEl()
        {
            Assert.AreEqual("Vapp 144 kt (del plan)", ApproachSpeedWindow.VappText(144, 139, vrefFromPlan: true),
                "el mismo número del registro, ahora con el origen del Vref detrás");

            // Si alguna vez el Vref viene del avión, manda ese y no se marca nada.
            Assert.AreEqual("Vapp 144 kt", ApproachSpeedWindow.VappText(144, 139, vrefFromPlan: false));

            // Y el suelo: una Vapp por debajo del Vref de referencia no se publica más baja.
            Assert.AreEqual("Vapp 139 kt (del plan)", ApproachSpeedWindow.VappText(120, 139, vrefFromPlan: true),
                "un consejo por debajo del Vref de referencia es peor que no darlo");

            Assert.IsNull(ApproachSpeedWindow.VappText(null, 139, true), "sin Vapp no hay línea");
        }

        /// <summary>
        /// **El umbral de los 8 kt sobre el Vref del plan.** Con `Vref 139` la ventana es
        /// `[134–154]` y el umbral de «el Vref de referencia es otro» son **147 kt** (`139 + 8`): a
        /// partir de ahí la velocidad sugiere otra configuración, no un piloto rápido.
        /// </summary>
        [DataTestMethod]
        [DataRow(141, false, "las 141 kt reales del vuelo: se evalúa normal, está en ventana")]
        [DataRow(146, false, "146 kt es 1 kt por debajo del umbral: todavía se evalúa")]
        [DataRow(147, true,  "147 kt = 139 + 8, el umbral")]
        [DataRow(157, true,  "157 kt: la referencia es otra configuración, no un exceso del piloto")]
        [DataRow(131, false, "el lado lento NO se relaja: 131 kt con Vref 139 se evalúa")]
        public void ElUmbralDe8Kt_SoloPerdonaElLadoRapido(int ias, bool explains, string because)
        {
            Assert.AreEqual(explains, ApproachSpeedWindow.PlanVrefExplainsSpeed(ias, 139), because);
        }

        /// <summary>
        /// La exención **necesita un Vref del plan**: sin él la ventana sale de la tabla por tipo y no
        /// hay nada que discutir, así que la velocidad se sigue evaluando como siempre.
        /// </summary>
        [TestMethod]
        public void SinVrefDelPlan_NoHayExencion()
        {
            Assert.IsFalse(ApproachSpeedWindow.PlanVrefExplainsSpeed(200, null));
            Assert.IsFalse(ApproachSpeedWindow.PlanVrefExplainsSpeed(200, 5), "un Vref implausible no cuenta");
        }
    }
}
