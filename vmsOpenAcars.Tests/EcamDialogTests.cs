using System.Drawing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using vmsOpenAcars.UI.Forms;

namespace vmsOpenAcars.Tests
{
    /// <summary>
    /// Reparto de espacio del `EcamDialog`, que es el popup de confirmación de toda la aplicación.
    ///
    /// Regresión reportada por el mantenedor: el aviso de discrepancia de aeronave son ~10 líneas
    /// dentro de una ventana de alto fijo, y el mensaje —en posición absoluta con `AutoSize`—
    /// crecía hacia abajo y se pintaba **encima** de los botones, dejando el NO inalcanzable.
    /// Ahora el mensaje vive en un panel con desplazamiento (`Dock.Fill`) y los botones en otro
    /// (`Dock.Bottom`), de modo que no pueden solaparse por mucho que crezca el texto.
    /// </summary>
    [TestClass]
    public class EcamDialogTests
    {
        private const string MismatchMessage =
            "⚠️ AIRCRAFT MISMATCH\n\nSimulator: B777\nOFP (SimBrief): B77L\n\n" +
            "The OFP was generated for a different aircraft type. Fuel and performance data may " +
            "not be accurate.\n\nDo you want to start the flight anyway?";

        [TestMethod]
        public void LongMessage_LeavesTheButtonsVisible()
        {
            using (var dlg = new EcamDialog(MismatchMessage, "AIRCRAFT MISMATCH", EcamDialogButtons.YesNo))
            {
                dlg.PerformLayout();     // la ventana no se enseña: hay que pedir el layout a mano

                Rectangle message = dlg.MessageHostBounds;
                Rectangle buttons = dlg.ButtonsBounds;

                Assert.IsTrue(message.Bottom <= buttons.Top,
                    $"el mensaje no puede invadir la fila de botones (mensaje {message}, botones {buttons})");
                Assert.IsTrue(buttons.Bottom <= dlg.ClientSize.Height,
                    $"los botones tienen que caber en la ventana ({buttons} en {dlg.ClientSize})");
                Assert.IsTrue(message.Height >= dlg.MessagePreferredHeight,
                    $"el mensaje ({dlg.MessagePreferredHeight} px) tiene que caber sin desplazarse " +
                    $"en los {message.Height} px de su panel");
            }
        }

        [TestMethod]
        public void ShortMessage_KeepsTheDialogSmall()
        {
            using (var dlg = new EcamDialog("OK", "TITLE", EcamDialogButtons.OK))
            {
                dlg.PerformLayout();

                Assert.IsTrue(dlg.ClientSize.Height <= 250,
                    $"un aviso de una línea no debe crecer la ventana (dio {dlg.ClientSize.Height})");
                Assert.IsTrue(dlg.MessageHostBounds.Bottom <= dlg.ButtonsBounds.Top);
            }
        }
    }
}
