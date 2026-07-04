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
