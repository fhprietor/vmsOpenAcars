using System;
using System.Drawing;
using vmsOpenAcars.Core.Flight;
using vmsOpenAcars.Models;
using vmsOpenAcars.Services;
using vmsOpenAcars.UI.Forms;

namespace vmsOpenAcars.ViewModels
{
    internal sealed class TelemetryCallbacks
    {
        public Action<string, Color>          Log;

        /// <summary>
        /// **Línea de depuración marcada explícitamente** (v0.9.78), sin que el texto tenga que
        /// estar en la lista cerrada. Es el camino del aviso del enlace con el simulador, que está
        /// traducido y no comparte prefijo entre idiomas. El filtro y el espejo al PIREP son los
        /// mismos que los de `Log`: la única diferencia es cómo se clasifica la línea.
        /// </summary>
        public Action<string, Color>          LogDebug;
        public Action<string, OsdSeverity>    OsdMessage;
        public Action                         FlightInfoChanged;
        public Action<double, double, double> MapPositionUpdate;
        public Action<string>                 PositionUpdate;
        public Action<FlightPhase>            PhaseChanged;
        public Action<FlightPhase>            AirStatusChanged;
        public Action<ValidationStatus>       ValidationStatusChanged;
        public Action<bool>                   AcarsStatusChanged;
        public Action<string>                 SimulatorNameChanged;
    }
}
