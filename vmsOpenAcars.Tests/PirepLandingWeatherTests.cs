using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using vmsOpenAcars.Core.Flight;
using vmsOpenAcars.Helpers;

namespace vmsOpenAcars.Tests
{
    /// <summary>
    /// **La meteo del aterrizaje en el PIREP.**
    ///
    /// El mecanismo es el `notes`: es texto libre, ya se usaba en el prefile y **no depende de que
    /// nadie cree nada**. Los otros tres campos que llevamos (`Departure Runway`, `Arrival Runway`,
    /// `Taxi Route`) son campos personalizados de `pirep_fields` que la aerolínea creó en phpVMS; uno
    /// nuevo exige exactamente lo mismo, y una clave que no existe **no da error**: se ignora en
    /// silencio. Por eso el camino estructurado existe pero va apagado
    /// (`AppConfig.PirepLandingWeatherFieldEnabled`), y su nombre —el que habría que crear en
    /// phpVMS— queda fijado aquí.
    ///
    /// La línea real de referencia es la del vuelo 36 del logbook del piloto (SKBO 14R).
    /// </summary>
    [TestClass]
    public class PirepLandingWeatherTests
    {
        private const string MetarSkbo =
            "METAR SKBO 250100Z 01003KT 350V110 9999 BKN080 15/11 Q1028 NOSIG";

        [TestMethod]
        public void LaLineaDeMeteo_EntraEnElNotesDelPirep()
        {
            var wind = WindComponents.Compute(10, 3, null, 127.231532474263);
            string line = LandingWeatherLine.Build(MetarSkbo, null, wind);

            var payload = (Dictionary<string, object>)PirepBuilder.BuildPayload(new PirepPayloadArgs
            {
                TotalFlightTimeMinutes   = 95,
                ActualFlightTimeMinutes  = 88,
                PlannedFlightTimeMinutes = 90,
                TotalDistanceNm          = 402.4,
                PlannedDistanceNm        = 400.0,
                BlockFuel                = 5000,
                FuelUsed                 = 3200,
                LandingRateFpm           = -152,
                Score                    = 91,
                ArrivalAirport           = "SKBO",
                LandingWeatherLine       = line,
            });

            var notes = (string)payload["notes"];
            Assert.AreEqual(
                "vmsOpenAcars Report - Total: 95 min, Flight: 88 min, Dist: 402.4 NM | " +
                "WX LANDING: " + MetarSkbo + " | WIND 010/03 | HW -1 XW -3",
                notes);
        }

        [TestMethod]
        public void SinMeteo_ElNotesEsExactamenteElDeSiempre()
        {
            // Degradar sin datos: sin touchdown no hay línea y el `notes` no cambia ni un carácter.
            var payload = (Dictionary<string, object>)PirepBuilder.BuildPayload(new PirepPayloadArgs
            {
                TotalFlightTimeMinutes   = 95,
                ActualFlightTimeMinutes  = 88,
                PlannedFlightTimeMinutes = 90,
                TotalDistanceNm          = 402.4,
                PlannedDistanceNm        = 400.0,
                ArrivalAirport           = "SKBO",
            });

            Assert.AreEqual(
                "vmsOpenAcars Report - Total: 95 min, Flight: 88 min, Dist: 402.4 NM",
                (string)payload["notes"]);
        }

        [TestMethod]
        public void ElCampoEstructurado_TieneNombreYNoSeMandaSiNoHayMeteo()
        {
            // El nombre que hay que crear en phpVMS para tenerlo estructurado en vez de en el `notes`.
            Assert.AreEqual("Landing Weather", PirepFields.LandingWeatherName);

            // La sobrecarga de 3 argumentos (la que usan el prefile y la ruta de rodaje) NO lo manda:
            // el camino estructurado no se activa solo.
            Assert.IsFalse(PirepFields.Build("14L", "14R", "F E M A A3").ContainsKey("Landing Weather"));

            // Y con la línea sale con el nombre, no con un slug.
            var fields = PirepFields.Build(null, null, null, "WX LANDING: METAR SKBO 250100Z 01003KT");
            Assert.AreEqual(1, fields.Count);
            Assert.AreEqual("WX LANDING: METAR SKBO 250100Z 01003KT", fields["Landing Weather"]);

            // Sin línea, el campo se omite (no se manda vacío, que borraría lo que hubiera).
            Assert.AreEqual(0, PirepFields.Build(null, null, null, "   ").Count);
        }

        [TestMethod]
        public void LaLineaDeMeteo_SeColapsaAUnaSolaLinea()
        {
            var fields = PirepFields.Build(null, null, null, "WX LANDING: METAR X\r\n| WIND 010/03");
            Assert.AreEqual("WX LANDING: METAR X | WIND 010/03", fields["Landing Weather"]);
            Assert.IsFalse(fields["Landing Weather"].Contains("\n"));
        }
    }
}
