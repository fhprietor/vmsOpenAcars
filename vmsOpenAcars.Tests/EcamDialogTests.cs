using System.Drawing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using vmsOpenAcars.Services;
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

        /// <summary>
        /// Los rótulos de los botones salen del fichero de idioma.
        ///
        /// Regresión reportada por el mantenedor: el modal de salir de la aplicación se leía
        /// «¿Salir de la aplicación?» con **YES** y **NO** debajo. El mensaje sí estaba traducido
        /// (`ConfirmExit`), pero los rótulos estaban escritos a mano en inglés dentro de
        /// `EcamDialog`, que es el diálogo de confirmación de **toda** la aplicación: el fallo no
        /// era de ese modal, era de todos los que usan este popup.
        /// </summary>
        [TestMethod]
        public void ButtonLabels_ComeFromTheLanguageFile()
        {
            string original = LocalizationService.Instance.CurrentLanguage;
            try
            {
                LocalizationService.Instance.LoadLanguage("es");
                using (var dlg = new EcamDialog("mensaje", "TITULO", EcamDialogButtons.YesNoCancel))
                {
                    // El contrato: el rótulo es el que da el idioma cargado.
                    Assert.AreEqual(LocalizationService.Instance.GetString("BtnYes"), dlg.Button1Text);
                    Assert.AreEqual(LocalizationService.Instance.GetString("BtnNo"), dlg.Button2Text);
                    Assert.AreEqual(LocalizationService.Instance.GetString("BtnCancel"), dlg.Button3Text);

                    // Y la regresión concreta: con el idioma en español no puede salir el inglés
                    // escrito a mano. Con «NO» no se comprueba porque coincide en los dos idiomas.
                    Assert.AreNotEqual("YES", dlg.Button1Text, "el botón de afirmar sigue en inglés");
                    Assert.AreNotEqual("CANCEL", dlg.Button3Text, "el botón de cancelar sigue en inglés");
                }

                // El OK del aviso de una línea, que era el otro rótulo escrito a mano.
                using (var dlg = new EcamDialog("mensaje", "TITULO", EcamDialogButtons.OK))
                    Assert.AreEqual(LocalizationService.Instance.GetString("BtnOk"), dlg.Button1Text);

                // Y que el idioma manda de verdad: en inglés vuelve a ser YES.
                LocalizationService.Instance.LoadLanguage("en");
                using (var dlg = new EcamDialog("message", "TITLE", EcamDialogButtons.YesNo))
                {
                    Assert.AreEqual("YES", dlg.Button1Text);
                    Assert.AreEqual("NO", dlg.Button2Text);
                }
            }
            finally
            {
                // El servicio es un singleton y lo comparten otros tests: se deja como estaba.
                LocalizationService.Instance.LoadLanguage(original);
            }
        }
    }
}
