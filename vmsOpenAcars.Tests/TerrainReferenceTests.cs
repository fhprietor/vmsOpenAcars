using Microsoft.VisualStudio.TestTools.UnitTesting;
using vmsOpenAcars.Helpers;
using vmsOpenAcars.Models;

namespace vmsOpenAcars.Tests
{
    /// <summary>
    /// **La elevación de referencia del AGL, con el caso real que la obligó a cambiar.**
    ///
    /// En el PIREP **`RBmrBApLOjWlYmO2`** (SKSJ→SKSV, BE58, UTC del 14/10/2026) la fase se quedó en
    /// `Climb` con el avión **rodando y aparcado** en el destino. La referencia se elegía por fase, así
    /// que pasó a ser la del **origen** —SKSJ, **611 ft**— y el cliente publicó un AGL de
    /// **264 ft** (`875 − 611`) con el avión en el suelo, a **0 kt** y con el freno de parqueo puesto.
    /// La traza entera del vuelo lo enseña: **263, 267, 267, 267, 266, 264, 263, 262, 261, 260, 260,
    /// 261, 262** con el `msl` clavado entre 873 y 880 ft.
    ///
    /// Un avión no puede estar a 264 ft y parado a la vez, y ese número falso es lo que hacía que la
    /// fase equivocada **pareciera coherente**. La regla que faltaba: **si el vuelo ya ha tocado
    /// tierra, la referencia es el campo de llegada en cualquier fase**, porque el avión ya está ahí.
    /// </summary>
    [TestClass]
    public class TerrainReferenceTests
    {
        // El plan real del vuelo: SKSJ (611 ft) → SKSV (872 ft).
        private static SimbriefPlan Plan() => new SimbriefPlan
        {
            Origin          = "SKSJ",
            Destination     = "SKSV",
            OriginElevation = 611,
            DestinationElevation = 872,
        };

        /// <summary>
        /// Las cuentas del caso real: la misma altitud publicada —**875 ft MSL**— da **264 ft** de AGL
        /// con la referencia equivocada y **3 ft** con la buena. Es la diferencia entre un avión que
        /// vuela y uno que está aparcado.
        /// </summary>
        [TestMethod]
        public void ConElVueloYaAterrizado_LaMismaAltitudDejaDeDarUnAglFantasma()
        {
            const double altitudeMsl = 875.0;

            double conLaReferenciaDelOrigen = altitudeMsl
                - FlightPhaseHelper.GetTerrainElevation(FlightPhase.Climb, Plan(), hasTouchedDownThisFlight: false);
            double conLaReferenciaDelDestino = altitudeMsl
                - FlightPhaseHelper.GetTerrainElevation(FlightPhase.Climb, Plan(), hasTouchedDownThisFlight: true);

            Assert.AreEqual(264.0, conLaReferenciaDelOrigen, 1e-6,
                "es el AGL fantasma que publicó el vuelo real");
            Assert.AreEqual(3.0, conLaReferenciaDelDestino, 1e-6,
                "y éste es el de verdad: el avión está en el suelo");
        }

        /// <summary>
        /// La regla es del **hecho de haber aterrizado**, no de la fase: con un toque capturado, la
        /// referencia es la llegada en cualquier fase de vuelo —incluidas las que se quedan atrás por un
        /// fallo, que es exactamente lo que hay que cubrir—.
        /// </summary>
        [TestMethod]
        public void ConUnToqueCapturado_LaReferenciaEsLaLlegadaEnCualquierFaseDeVuelo()
        {
            foreach (var phase in new[] { FlightPhase.Takeoff, FlightPhase.Climb,
                                          FlightPhase.Enroute, FlightPhase.Descent })
            {
                Assert.AreEqual(872.0,
                    FlightPhaseHelper.GetTerrainElevation(phase, Plan(), hasTouchedDownThisFlight: true),
                    $"{phase} con el vuelo ya aterrizado: el campo de debajo es el de llegada");
            }
        }

        /// <summary>
        /// Y sin haber aterrizado no cambia nada: la tabla por fase sigue mandando, que es lo que hace
        /// que el AGL del ascenso se mida contra el origen.
        /// </summary>
        [TestMethod]
        public void SinHaberAterrizado_LaTablaPorFaseSigueMandiendo()
        {
            Assert.AreEqual(611.0, FlightPhaseHelper.GetTerrainElevation(FlightPhase.Climb, Plan(), false));
            Assert.AreEqual(611.0, FlightPhaseHelper.GetTerrainElevation(FlightPhase.TaxiOut, Plan(), false));
            Assert.AreEqual(872.0, FlightPhaseHelper.GetTerrainElevation(FlightPhase.Approach, Plan(), false));
            Assert.AreEqual(872.0, FlightPhaseHelper.GetTerrainElevation(FlightPhase.OnBlock, Plan(), false));

            // Sin plan no hay referencia: degrada a 0 en vez de inventarse una elevación.
            Assert.AreEqual(0.0, FlightPhaseHelper.GetTerrainElevation(FlightPhase.Climb, null, true));
        }
    }
}
