using Microsoft.VisualStudio.TestTools.UnitTesting;
using vmsOpenAcars.Models;

namespace vmsOpenAcars.Tests
{
    /// <summary>
    /// Clasificación del estado de un PIREP (columna `state` de phpVMS).
    ///
    /// Esta clasificación decide el fallback de <c>FilePirep()</c>: cuando phpVMS archiva
    /// el PIREP pero devuelve un código HTTP no-2xx, el vuelo solo se da por enviado si el
    /// PIREP ya NO está activo. El bug original comparaba <c>status</c> (un código de fase
    /// ACARS, no numérico) contra "1"/"6", de modo que cualquier respuesta no nula se
    /// interpretaba como archivada y el piloto veía "PIREP FILED" sin que el vuelo
    /// estuviera registrado.
    ///
    /// El caso crítico es el último: ante un estado ilegible, la respuesta correcta es
    /// NO afirmar que se archivó. Preferimos que el piloto vea "no se pudo enviar" y
    /// pueda reintentar antes que perder el vuelo en silencio.
    /// </summary>
    [TestClass]
    public class PirepStateTests
    {
        // ── Estados que mantienen el PIREP en el ciclo activo del piloto ─────────

        [TestMethod]
        public void InProgress_IsActive()
        {
            Assert.IsTrue(Pirep.IsActiveState((int)PirepState.InProgress));
        }

        [TestMethod]
        public void Paused_IsActive()
        {
            Assert.IsTrue(Pirep.IsActiveState((int)PirepState.Paused));
        }

        // ── Estados que significan que el PIREP fue archivado ────────────────────

        [TestMethod]
        public void Pending_IsNotActive()
        {
            Assert.IsFalse(Pirep.IsActiveState((int)PirepState.Pending),
                "un PIREP pendiente de aprobación ya salió del ciclo activo");
        }

        [TestMethod]
        public void Accepted_IsNotActive()
        {
            Assert.IsFalse(Pirep.IsActiveState((int)PirepState.Accepted));
        }

        [TestMethod]
        public void Rejected_IsNotActive()
        {
            Assert.IsFalse(Pirep.IsActiveState((int)PirepState.Rejected));
        }

        [TestMethod]
        public void Cancelled_IsNotActive()
        {
            Assert.IsFalse(Pirep.IsActiveState((int)PirepState.Cancelled));
        }

        // ── El caso que motivó el fix ────────────────────────────────────────────

        [TestMethod]
        public void UnknownState_IsTreatedAsActive_SoTheFileIsNotClaimedAsSent()
        {
            Assert.IsTrue(Pirep.IsActiveState(null),
                "sin poder leer el estado no se puede afirmar que el PIREP se archivó");

            Assert.IsTrue(Pirep.IsActiveState(Pirep.UnknownState),
                "-1 es el centinela de 'state ausente' que usa ApiService.GetPirepDetail");
        }

        // ── Coherencia con la propiedad de instancia ─────────────────────────────

        [DataTestMethod]
        [DataRow((int)PirepState.InProgress, true)]
        [DataRow((int)PirepState.Paused,     true)]
        [DataRow((int)PirepState.Pending,    false)]
        [DataRow((int)PirepState.Accepted,   false)]
        public void InstanceIsActive_MatchesStaticClassification(int state, bool expected)
        {
            var pirep = new Pirep { State = state };
            Assert.AreEqual(expected, pirep.IsActive);
            Assert.AreEqual(expected, Pirep.IsActiveState(state));
        }

        [TestMethod]
        public void DefaultPirep_IsActive_BecauseZeroIsInProgress()
        {
            // Un Pirep recién construido (State sin asignar) es 0 = InProgress. Importa
            // porque un DTO a medio poblar no debe leerse como "archivado".
            Assert.IsTrue(new Pirep().IsActive);
        }
    }
}
