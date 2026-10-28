using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using vmsOpenAcars.Core.Flight;
using vmsOpenAcars.Helpers;
using vmsOpenAcars.Models;

namespace vmsOpenACars.Tests
{
    /// <summary>
    /// **Cómo se manda un PIREP a revisión**, tal como lo confirmó el equipo de phpVMS el 27/10/2026:
    /// el estado lo fija el servidor y la marca es un campo, `fields.restricted_airspace`, con la zona
    /// como texto libre («1» si no hay más detalle).
    ///
    /// Hay dos errores que estos tests impiden repetir:
    /// 1. **Mandar `state`**: el cliente lo mandaba a 2 en todos los vuelos —`ACCEPTED` en phpVMS— y con
    ///    `auto_approve_acars = true` en todos los rangos ningún vuelo pasaba por revisión.
    /// 2. **Mandar el campo vacío**: no cuenta, y el campo se omite en un vuelo limpio.
    /// </summary>
    [TestClass]
    public class AirspaceReviewMarkTests
    {
        private static IDictionary<string, object> Payload(int violations, string detail)
        {
            var args = new PirepPayloadArgs
            {
                TotalFlightTimeMinutes   = 200,
                ActualFlightTimeMinutes  = 190,
                PlannedFlightTimeMinutes = 195,
                TotalDistanceNm          = 520.4,
                PlannedDistanceNm        = 525.0,
                BlockFuel                = 8000,
                FuelUsed                 = 6000,
                LandingRateFpm           = -180,
                Score                    = 92,
                BlockOnTime              = new System.DateTime(2026, 10, 27, 18, 40, 0),
                ArrivalAirport           = "TJSJ",
                AirspaceViolations       = violations,
                AirspaceViolationDetail  = detail,
            };

            return (IDictionary<string, object>)PirepBuilder.BuildPayload(args);
        }

        [TestMethod]
        public void UnVueloLimpioNoTocaElEstadoNiMandaElCampo()
        {
            var payload = Payload(0, null);

            // El estado lo fija el servidor. Si volvemos a mandarlo, rompemos su decisión.
            Assert.IsFalse(payload.ContainsKey("state"), "`state` no se manda: lo fija phpVMS");

            // Y el campo de revisión solo aparece cuando hay algo que revisar.
            Assert.IsFalse(payload.ContainsKey("fields"), "sin invasión no va ni `fields` con null");
        }

        [TestMethod]
        public void UnaInvasiónMandaElVueloARevision()
        {
            var payload = Payload(1, "RESTRICTED SKEZ");

            var fields = payload["fields"] as IDictionary<string, string>;
            Assert.IsNotNull(fields, "la marca viaja en `fields`");
            Assert.AreEqual("RESTRICTED SKEZ", fields["restricted_airspace"]);

            // El revisor tiene que ver el motivo sin abrir el campo: va también en `notes`.
            StringAssert.Contains((string)payload["notes"], "AIRSPACE VIOLATION: RESTRICTED SKEZ");
        }

        [TestMethod]
        public void SinDetalleSeMandaUno()
        {
            var payload = Payload(2, "   ");

            var fields = payload["fields"] as IDictionary<string, string>;
            Assert.AreEqual("1", fields["restricted_airspace"], "«1» es su convención para «ocurrió, sin detalle»");
        }

        [TestMethod]
        public void ElNombreDelCampoEsElQuePidioPhpVms()
        {
            Assert.AreEqual("restricted_airspace", PirepFields.RestrictedAirspaceName);
        }

        [TestMethod]
        public void UnValorVacioNoCuenta()
        {
            // «Vacío o "0" no cuenta», así que jamás se manda vacío: se omite.
            Assert.IsNull(PirepFields.ReviewMark(0, "RESTRICTED SKEZ"),
                "sin invasiones no hay marca, aunque llegue un detalle suelto");
        }

        [TestMethod]
        public void LosNumerosDelEstadoSonLosDePhpVms()
        {
            // El enum del modelo estaba inventado (`Paused=1`, `Pending=2`) hasta v0.9.56. Ya no se
            // manda, pero se sigue leyendo cuando `FilePirep()` comprueba si el PIREP quedó archivado.
            Assert.AreEqual(0, (int)PirepState.InProgress);
            Assert.AreEqual(1, (int)PirepState.Pending, "PENDING = 1: es el estado de un vuelo en revisión");
            Assert.AreEqual(2, (int)PirepState.Accepted);
            Assert.AreEqual(7, (int)PirepState.Paused, "PAUSED es 7, no 1");

            Assert.IsFalse(Pirep.IsActiveState((int)PirepState.Pending),
                "un vuelo en revisión ya salió del ciclo del piloto: el fallback de FilePirep lo da por enviado");
            Assert.IsTrue(Pirep.IsActiveState((int)PirepState.Paused));
        }
    }
}
