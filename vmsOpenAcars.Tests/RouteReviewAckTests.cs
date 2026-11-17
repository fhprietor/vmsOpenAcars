using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using vmsOpenAcars.Core.Flight;
using vmsOpenAcars.Helpers;

namespace vmsOpenACars.Tests
{
    /// <summary>
    /// **El acuse del aviso de ruta en el PIREP**: `fields.route_ack = 1` cuando el piloto fue
    /// advertido de que su plan cruzaba una zona restringida y decidió volar.
    ///
    /// Lo pidió phpVMS el 16/11/2026 y su motivo es la revisión posterior: **no es lo mismo volar a
    /// sabiendas que sin aviso**. Se comprueba aquí que el campo
    /// * viaja con ese nombre exacto, el que acordaron;
    /// * **no aparece** cuando no hubo aviso aceptado (nada de `route_ack = 0` ni de campos vacíos);
    /// * convive con la marca de revisión `restricted_airspace` sin pisarla.
    /// </summary>
    [TestClass]
    public class RouteReviewAckTests
    {
        private static IDictionary<string, object> Payload(bool acknowledged,
                                                           int violations = 0,
                                                           string detail = null)
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
                BlockOnTime              = new System.DateTime(2026, 11, 16, 18, 40, 0),
                ArrivalAirport           = "SKCG",
                AirspaceViolations       = violations,
                AirspaceViolationDetail  = detail,
                RouteAcknowledged        = acknowledged,
            };

            return (IDictionary<string, object>)PirepBuilder.BuildPayload(args);
        }

        private static IDictionary<string, string> Fields(IDictionary<string, object> payload)
            => payload.TryGetValue("fields", out object value)
                ? value as IDictionary<string, string>
                : null;

        [TestMethod]
        public void ElNombreDelCampoEsElQuePidioPhpVms()
        {
            Assert.AreEqual("route_ack", PirepFields.RouteAckName);
        }

        [TestMethod]
        public void ConAcuseElPirepLlevaRouteAckUno()
        {
            var fields = Fields(Payload(acknowledged: true));

            Assert.IsNotNull(fields, "el acuse viaja en `fields`");
            Assert.AreEqual("1", fields["route_ack"]);
            Assert.IsFalse(fields.ContainsKey(PirepFields.RestrictedAirspaceName),
                "un vuelo sin invasión no lleva la marca de revisión");
        }

        [TestMethod]
        public void SinAcuseNoSeMandaElCampo()
        {
            var payload = Payload(acknowledged: false);

            Assert.IsNull(Fields(payload), "sin aviso aceptado no va ni `fields`");
            Assert.IsFalse(payload.ContainsKey("fields"));
        }

        [TestMethod]
        public void ElAcuseYLaMarcaDeRevisionConviven()
        {
            // El caso completo: fue advertido antes de despegar Y acabó invadiendo la zona.
            var fields = Fields(Payload(acknowledged: true, violations: 1, detail: "RESTRICTED SKR11"));

            Assert.AreEqual("RESTRICTED SKR11", fields[PirepFields.RestrictedAirspaceName]);
            Assert.AreEqual("1", fields["route_ack"]);
            Assert.AreEqual(2, fields.Count, "no se cuela ningún campo más");
        }

        [TestMethod]
        public void LaMarcaSolaSigueFuncionandoComoAntes()
        {
            var fields = Fields(Payload(acknowledged: false, violations: 1, detail: "PROHIBITED SKP35"));

            Assert.AreEqual("PROHIBITED SKP35", fields[PirepFields.RestrictedAirspaceName]);
            Assert.IsFalse(fields.ContainsKey("route_ack"));
        }

        [TestMethod]
        public void UnAcuseSinAvisoNoEsUnAcuse()
        {
            // La decisión de apuntarlo la toma `FlightManager` (solo si hubo `RouteWarning`), pero el
            // helper tampoco inventa: `false` es `null`, no un campo vacío.
            Assert.IsNull(PirepFields.RouteAckMark(false));
            Assert.AreEqual("1", PirepFields.RouteAckMark(true).Single().Value);
        }
    }
}
