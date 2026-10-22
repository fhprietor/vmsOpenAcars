using Microsoft.VisualStudio.TestTools.UnitTesting;
using vmsOpenAcars.Helpers;

namespace vmsOpenAcars.Tests
{
    /// <summary>
    /// El piloto automático se decide con las **banderas** del panel, no con el rumbo.
    ///
    /// El error que corrige (v0.9.50 y anteriores): el respaldo para los addons de MSFS que no escriben
    /// `0x07BC` usaba **`0x07CC`**, que la tabla de FSUIPC7 documenta como **`AUTOPILOT HEADING LOCK
    /// DIR`** —*«Autopilot heading value, as degrees*65536/360»*—, y lo comparaba con `1`, `2` y `3`
    /// para deducir los modos. Un rumbo como 43 690 (240°) es distinto de cero, así que el AP salía
    /// **enganchado siempre**, y las comparaciones solo acertaban en rumbos de 0,005°, 0,011° y 0,016°,
    /// de ahí el `HDG ALT` fijo en todos los registros.
    /// </summary>
    [TestClass]
    public class AutopilotPanelTests
    {
        private static AutopilotFlags Solo() => new AutopilotFlags { Available = true };

        [TestMethod]
        public void ElMaestroBasta()
        {
            var f = Solo();
            f.Master = true;

            Assert.IsTrue(AutopilotPanel.IsEngaged(f));
        }

        [TestMethod]
        public void UnModoSinMaestroTambienCuenta()
        {
            // El caso que el respaldo quería cubrir: addons (PMDG, FBW) que no escriben 0x07BC pero sí
            // publican el modo. El NAV1 lock es el que delata el LNAV/NAV.
            var f = Solo();
            f.NavLock = true;

            Assert.IsTrue(AutopilotPanel.IsEngaged(f),
                "sin maestro, cualquier bandera de modo vale como AP enganchado");
            Assert.AreEqual("NAV", AutopilotPanel.NavMode(f));
        }

        [TestMethod]
        public void ElFlightDirectorNoEsElPilotoAutomatico()
        {
            // El FD va encendido en casi todo el vuelo, también en la final a mano: si contara, el AP
            // estaría «enganchado» desde el despegue hasta el aparcamiento.
            var f = Solo();
            f.FlightDirector = true;

            Assert.IsFalse(AutopilotPanel.IsEngaged(f));
        }

        [TestMethod]
        public void SinBanderasNoHayPilotoAutomatico()
        {
            Assert.IsFalse(AutopilotPanel.IsEngaged(Solo()));
        }

        [TestMethod]
        public void LosModosRealesDelVuelo()
        {
            var f = Solo();
            f.Master = true;
            f.NavLock = true;
            f.AltitudeLock = true;

            Assert.AreEqual("NAV", AutopilotPanel.NavMode(f));
            Assert.AreEqual("ALT", AutopilotPanel.VertMode(f));
        }

        [TestMethod]
        public void LaAproximacionGanaAlNav()
        {
            var f = Solo();
            f.Master = true;
            f.NavLock = true;
            f.ApproachHold = true;
            f.GlideslopeHold = true;

            Assert.AreEqual("ILS", AutopilotPanel.NavMode(f), "con glideslope es una aproximación de ILS");
            Assert.AreEqual("GS", AutopilotPanel.VertMode(f));
        }

        [TestMethod]
        public void AproximacionSinGlideslopeSeLlamaApp()
        {
            var f = Solo();
            f.Master = true;
            f.ApproachHold = true;

            Assert.AreEqual("APP", AutopilotPanel.NavMode(f));
            Assert.AreEqual("GS", AutopilotPanel.VertMode(f), "el modo vertical de una aproximación es GS");
        }

        [TestMethod]
        public void RumboYVelocidadTienenSuPropioModo()
        {
            var f = Solo();
            f.Master = true;
            f.HeadingLock = true;
            f.VerticalHold = true;

            Assert.AreEqual("HDG", AutopilotPanel.NavMode(f));
            Assert.AreEqual("VS", AutopilotPanel.VertMode(f));
        }

        [TestMethod]
        public void LaLineaDeDiagnosticoDiceTodoLoQuePublicaElAvion()
        {
            var f = Solo();
            f.Master = true;
            f.HeadingLock = true;
            f.AltitudeLock = true;
            f.FlightDirector = true;

            string line = AutopilotPanel.Describe(f);

            StringAssert.Contains(line, "master=1");
            StringAssert.Contains(line, "hdg=1");
            StringAssert.Contains(line, "alt=1");
            StringAssert.Contains(line, "fd=1");
            StringAssert.Contains(line, "-> HDG+ALT");
        }
    }
}
