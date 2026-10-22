using Microsoft.VisualStudio.TestTools.UnitTesting;
using vmsOpenAcars.Helpers;
using vmsOpenAcars.Models;

namespace vmsOpenAcars.Tests
{
    /// <summary>
    /// «Pueden moverse por la cabina» solo se dice en crucero.
    ///
    /// La regla sale del PIREP real **`5ow69vzDNW5ZMayB`**: el cliente lo anunció a las 13:19:12,
    /// **dos minutos después del despegue y subiendo a 1 800 fpm**. Aunque la turbulencia hubiera
    /// sido real, ese permiso es falso en subida (la tripulación va amarrada y no hay servicio) y en
    /// descenso dice justo lo contrario de lo que toca.
    ///
    /// La entrada en turbulencia **no** pasa por aquí: una salida turbulenta se anuncia en cualquier
    /// fase en el aire.
    /// </summary>
    [TestClass]
    public class CabinCalloutPolicyTests
    {
        [TestMethod]
        public void SoloEnCruceroSeAvisaDeQueSePuedeCircular()
        {
            Assert.IsTrue(CabinCalloutPolicy.AnnouncesTurbulenceCleared(FlightPhase.Enroute));
        }

        [TestMethod]
        public void EnSubidaNoSeAvisa()
        {
            // El caso exacto del PIREP: 5 200 ft y 1 800 fpm hacia arriba.
            Assert.IsFalse(CabinCalloutPolicy.AnnouncesTurbulenceCleared(FlightPhase.Climb));
            Assert.IsFalse(CabinCalloutPolicy.AnnouncesTurbulenceCleared(FlightPhase.Takeoff));
            Assert.IsFalse(CabinCalloutPolicy.AnnouncesTurbulenceCleared(FlightPhase.TakeoffRoll));
        }

        [TestMethod]
        public void EnDescensoYAproximacionTampoco()
        {
            // En descenso la cabina dice lo contrario: «regresen a sus asientos».
            Assert.IsFalse(CabinCalloutPolicy.AnnouncesTurbulenceCleared(FlightPhase.Descent));
            Assert.IsFalse(CabinCalloutPolicy.AnnouncesTurbulenceCleared(FlightPhase.Approach));
        }

        [TestMethod]
        public void EnElSueloTampoco()
        {
            Assert.IsFalse(CabinCalloutPolicy.AnnouncesTurbulenceCleared(FlightPhase.Idle));
            Assert.IsFalse(CabinCalloutPolicy.AnnouncesTurbulenceCleared(FlightPhase.Boarding));
            Assert.IsFalse(CabinCalloutPolicy.AnnouncesTurbulenceCleared(FlightPhase.TaxiIn));
            Assert.IsFalse(CabinCalloutPolicy.AnnouncesTurbulenceCleared(FlightPhase.OnBlock));
        }
    }
}
