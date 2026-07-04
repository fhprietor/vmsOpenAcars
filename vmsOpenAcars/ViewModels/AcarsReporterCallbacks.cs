using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using vmsOpenAcars.Core.Flight;
using vmsOpenAcars.Models;
using vmsOpenAcars.Services;
using vmsOpenAcars.UI.Forms;

namespace vmsOpenAcars.ViewModels
{
    internal sealed class AcarsReporterCallbacks
    {
        public Action<string, Color>                                        Log;
        public Action<string, OsdSeverity>                                  OsdMessage;
        public Action<string, Color, bool>                                  ButtonStateChanged;
        public Action                                                       FlightEnded;
        public Action<string>                                               AirportChanged;
        public Action<ValidationStatus>                                     ValidationStatusChanged;
        public Action                                                       ResetTelemetry;
        public Func<string, string, EcamDialogButtons, Task<DialogResult>>  ShowConfirmation;
        public Action<SimbriefPlan>                                         SetActivePlan;
        public Action                                                       UpdateFlightInfo;
    }
}
