using System;
using System.Threading;
using System.Windows.Forms;
using vmsOpenAcars.Helpers;
using vmsOpenAcars.UI.Forms;

namespace vmsOpenAcars
{
    static class Program
    {
        // La instancia viva del formulario principal: los manejadores de abajo la usan para volcar la
        // excepción al log del ACARS, que es el único log que la aplicación ya escribe.
        private static MainForm _mainForm;

        // Evita que el manejador recurse: mostrar el propio aviso puede disparar otra excepción, y
        // `MessageBox.Show` abre un bucle de mensajes en el que `Application.ThreadException` puede
        // volver a entrar.
        private static bool _reporting;

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // El cuadro «Excepción no controlada en la aplicación» nunca más (v0.9.63). El piloto que
            // abría el OFP se lo encontró y con él se perdió el vuelo: crear el control `PdfViewer`
            // dispara el constructor estático de PdfiumViewer, que carga `pdfium.dll`; en un Windows de
            // 32 bits sin `x86\pdfium.dll` lanzaba BadImageFormatException (0x8007000B) desde el
            // constructor del formulario y `Main` no tenía a quién contárselo. `CatchException` manda
            // lo que escape del hilo de la UI a `Application.ThreadException` en vez de al diálogo del
            // framework; el otro manejador cubre los hilos que no son de la UI.
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += OnThreadException;
            AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;

            _mainForm = new MainForm();
            Application.Run(_mainForm);
        }

        /// <summary>
        /// Excepción que nadie capturó en el hilo de la UI. Registra la excepción completa en el log,
        /// avisa al piloto en su idioma con el detalle técnico (bitness del proceso, ruta de la nativa
        /// de PDF que se esperaba y si existe) y **devuelve el control**: al salir de aquí
        /// `Application.Run` sigue, así que el vuelo en curso no se pierde. Todo va en `try/catch` mudo
        /// porque este es el último recurso: si el aviso mismo falla, la aplicación se queda viva.
        /// </summary>
        private static void OnThreadException(object sender, ThreadExceptionEventArgs e)
        {
            try
            {
                if (_reporting) return;
                _reporting = true;

                Exception ex = e != null ? e.Exception : null;
                LogToAppLog(ex);

                string detail = PdfNativeFailure.DescribeFailure(
                    ex, AppDomain.CurrentDomain.BaseDirectory);

                MessageBox.Show(
                    L._("Err_UnhandledMsg", detail),
                    L._("Err_UnhandledTitle"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
            catch
            {
                // Mudo a propósito: el manejador no puede recursar ni tumbar la aplicación.
            }
            finally
            {
                _reporting = false;
            }
        }

        /// <summary>
        /// Excepción en un hilo que no es el de la UI. Aquí el CLR ya no permite continuar, así que lo
        /// único que se hace es **registrar la excepción completa** en el log (sin bloquear: el método
        /// del formulario usa `BeginInvoke`) y callarse, sin aviso modal, que ya no serviría de nada.
        /// </summary>
        private static void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            try
            {
                LogToAppLog(e != null ? e.ExceptionObject as Exception : null);
            }
            catch
            {
                // Última red: si el log falla mientras el proceso muere, no hay nada que hacer.
            }
        }

        private static void LogToAppLog(Exception ex)
        {
            try
            {
                _mainForm?.LogUnhandledException(ex);
            }
            catch
            {
                // Sin formulario (fallo durante su construcción) no hay log donde escribir.
            }
        }
    }
}
