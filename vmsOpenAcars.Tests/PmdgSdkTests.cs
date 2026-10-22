using Microsoft.VisualStudio.TestTools.UnitTesting;
using vmsOpenAcars.Helpers;

namespace vmsOpenAcars.Tests
{
    /// <summary>
    /// El bloque SDK de PMDG, que es donde están de verdad el AP, el autobrake y los carteles de un
    /// PMDG.
    ///
    /// Nace del vuelo real **`4Vz9nwaKKgQpYrEl`** (PMDG 738, v0.9.51, SKBG→SKRG): las doce banderas
    /// estándar de MSFS salieron **todas a 0** con el avión en crucero a 23 990 ft y 240°, y el piloto
    /// **sí** había movido el autobrake y los carteles. La causa: esos offsets son del **AP del
    /// simulador** y el 738 de PMDG vuela con su propio AFDS. Los offsets de aquí salen de
    /// `Offset Mapping for PMDG 737.pdf` y `…777.pdf`.
    /// </summary>
    [TestClass]
    public class PmdgSdkTests
    {
        [TestMethod]
        public void LaFamiliaSeDetectaPorTituloOporIcao()
        {
            Assert.AreEqual(PmdgFamily.B737, PmdgSdk.FamilyFrom("PMDG 737-800", null));
            Assert.AreEqual(PmdgFamily.B737, PmdgSdk.FamilyFrom("737-800", "B738"));
            Assert.AreEqual(PmdgFamily.B777, PmdgSdk.FamilyFrom("PMDG 777-200LR", "B777"));
            Assert.AreEqual(PmdgFamily.B777, PmdgSdk.FamilyFrom(null, "B77W"));
            Assert.AreEqual(PmdgFamily.None, PmdgSdk.FamilyFrom("Airbus A320neo", "A20N"));
            Assert.AreEqual(PmdgFamily.None, PmdgSdk.FamilyFrom("Cessna 172", "C172"));
            Assert.AreEqual(PmdgFamily.None, PmdgSdk.FamilyFrom(null, null));
        }

        [TestMethod]
        public void BloqueApagadoNoEnganchaNiPublicaModos()
        {
            // Sin `EnableDataBroadcast` todo lee 0. El cliente tiene que callarse, no suponer.
            var r = new PmdgSdkRaw { Family = PmdgFamily.B737, AutobrakeRaw = 0, SeatBeltsRaw = 0, NoSmokingRaw = 0 };

            Assert.IsFalse(PmdgSdk.IsEngaged(r));
            Assert.AreEqual("----", PmdgSdk.NavMode(r));
            Assert.AreEqual("----", PmdgSdk.VertMode(r));
            StringAssert.Contains(PmdgSdk.Describe(r), "sin AP");
        }

        [TestMethod]
        public void ElCasoDelVueloRealEnCrucero()
        {
            // 738 en crucero: CMD A enganchado, LNAV y ALT HOLD en el MCP. Esto es lo que la tabla
            // estándar no pudo decir en `4Vz9nwaKKgQpYrEl`.
            var r = new PmdgSdkRaw
            {
                Family = PmdgFamily.B737,
                IndicationPowered = true,
                CmdA = true,
                Lnav = true,
                AltHold = true,
                AutobrakeRaw = 3,
                SeatBeltsRaw = 1,
                NoSmokingRaw = 2,
            };

            Assert.IsTrue(PmdgSdk.IsEngaged(r));
            Assert.AreEqual("LNAV", PmdgSdk.NavMode(r));
            Assert.AreEqual("ALT", PmdgSdk.VertMode(r));
            Assert.AreEqual("2", PmdgSdk.AutobrakeLabel(r), "el selector no cambia de tabla");
        }

        [TestMethod]
        public void ElFlightDirectorNoEsElPilotoAutomatico()
        {
            var r = new PmdgSdkRaw { Family = PmdgFamily.B737, IndicationPowered = true, Fd1 = true, Fd2 = true };

            Assert.IsFalse(PmdgSdk.IsEngaged(r));
        }

        [TestMethod]
        public void EnLaAproximacionMandaAppYLoc()
        {
            var r = new PmdgSdkRaw { Family = PmdgFamily.B777, Ap1 = true, App = true, Loc = true };

            Assert.AreEqual("APP", PmdgSdk.NavMode(r));
            Assert.AreEqual("LOC", PmdgSdk.NavMode(new PmdgSdkRaw { Family = PmdgFamily.B777, Ap1 = true, Loc = true }));
            Assert.AreEqual("GS", PmdgSdk.VertMode(r), "sin otro modo vertical, APP engancha la senda");
        }

        [TestMethod]
        public void LosModosVerticalesSeDistinguen()
        {
            Assert.AreEqual("VNAV", PmdgSdk.VertMode(new PmdgSdkRaw { Vnav = true }));
            Assert.AreEqual("FLCH", PmdgSdk.VertMode(new PmdgSdkRaw { Flch = true }));
            Assert.AreEqual("VS",   PmdgSdk.VertMode(new PmdgSdkRaw { Vs = true }));
            Assert.AreEqual("ALT",  PmdgSdk.VertMode(new PmdgSdkRaw { AltHold = true }));
            Assert.AreEqual("HDG",  PmdgSdk.NavMode(new PmdgSdkRaw { HdgSel = true }));
        }

        [TestMethod]
        public void ElAutobrakeUsaLaTablaDelDocumento()
        {
            // Misma tabla que `0x2F80`: 0=RTO, 1=Off, 2..5 = brake1..max.
            string[] esperado = { "RTO", "OFF", "1", "2", "3", "MAX" };
            for (int i = 0; i < esperado.Length; i++)
                Assert.AreEqual(esperado[i], PmdgSdk.AutobrakeLabel(new PmdgSdkRaw { AutobrakeRaw = i }));

            Assert.AreEqual(AutobrakeSelector.NoData, PmdgSdk.AutobrakeLabel(new PmdgSdkRaw { AutobrakeRaw = PmdgSdk.NoData }));
        }

        [TestMethod]
        public void ElCartelSoloEstaEncendidoConOn()
        {
            // 0 OFF, 1 AUTO, 2 ON. `AUTO` lo decide el avión según la fase: no es «encendido».
            Assert.IsFalse(PmdgSdk.SeatbeltsOn(new PmdgSdkRaw { SeatBeltsRaw = 0 }));
            Assert.IsFalse(PmdgSdk.SeatbeltsOn(new PmdgSdkRaw { SeatBeltsRaw = 1 }));
            Assert.IsTrue(PmdgSdk.SeatbeltsOn(new PmdgSdkRaw { SeatBeltsRaw = 2 }));

            Assert.AreEqual("AUTO", PmdgSdk.SignLabel(1));
            Assert.AreEqual("ON", PmdgSdk.SignLabel(2));
            Assert.AreEqual("OFF", PmdgSdk.SignLabel(0));
        }

        [TestMethod]
        public void LaHuellaCambiaConCualquierModo()
        {
            // Sirve de prueba de vida en el 777, que no publica `MCP_indication_powered`.
            var quieto = new PmdgSdkRaw { Family = PmdgFamily.B777 };
            Assert.AreEqual(PmdgSdk.Fingerprint(quieto), PmdgSdk.Fingerprint(quieto));

            var conAp = new PmdgSdkRaw { Family = PmdgFamily.B777, Ap1 = true };
            Assert.AreNotEqual(PmdgSdk.Fingerprint(quieto), PmdgSdk.Fingerprint(conAp));

            var conBrak = new PmdgSdkRaw { Family = PmdgFamily.B777, AutobrakeRaw = 3 };
            Assert.AreNotEqual(PmdgSdk.Fingerprint(quieto), PmdgSdk.Fingerprint(conBrak));
        }

        [TestMethod]
        public void LaLineaDeDiagnosticoDiceLoQuePublicaElBloque()
        {
            var r = new PmdgSdkRaw
            {
                Family = PmdgFamily.B737, IndicationPowered = true, CmdB = true, Fd1 = true,
                Lnav = true, AltHold = true, Speed = true,
            };

            string line = PmdgSdk.Describe(r);

            StringAssert.Contains(line, "powered=1");
            StringAssert.Contains(line, "cmd=01");
            StringAssert.Contains(line, "fd=10");
            StringAssert.Contains(line, "-> LNAV+ALT");
        }
    }
}
