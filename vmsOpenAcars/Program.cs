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

            // Primer arranque de una instalación limpia (v1.0.0). Hasta v0.9.81 el piloto que bajaba
            // el cliente se quedaba sin `vmsOpenACars.exe.config` y tenía que descargar la plantilla
            // aparte; ahora la plantilla viaja en el zip y se copia aquí **solo si falta**. Va antes de
            // construir la ventana porque `ConfigurationManager` abre el fichero en el primer acceso,
            // no al arrancar: así no hay que reiniciar para que la configuración nueva se lea.
            // Nunca puede impedir que arranque el ACARS, ni siquiera si el disco está lleno.
            ConfigInstallOutcome configOutcome = ConfigInstallOutcome.AlreadyPresent;
            string configDetail = null;
            try
            {
                configOutcome = ConfigBootstrap.InstallIfMissing(
                    AppDomain.CurrentDomain.BaseDirectory, out configDetail);
            }
            catch { /* último recurso: el arranque manda */ }

            // Login del primer arranque (v1.0.0). Sin clave utilizable —ni en el almacén cifrado por
            // DPAPI ni en el `.config`— no hay a qué servidor hablarle: se piden usuario y contraseña
            // y la clave que devuelve phpVMS se guarda cifrada. La contraseña **no se guarda**.
            // Si el piloto cancela, no se arranca a medias: un ACARS sin credenciales no sirve para
            // nada y dejarlo abierto solo confunde.
            if (!AppConfig.IsConfigured)
            {
                using (var login = new LoginForm(AppConfig.VmsApiUrl))
                {
                    if (login.ShowDialog() != DialogResult.OK) return;
                }
            }

            _mainForm = new MainForm();
            _mainForm.ConfigBootstrapOutcome = configOutcome;
            _mainForm.ConfigBootstrapDetail  = configDetail;
            // Con la plantilla recién instalada se abren los Ajustes: es donde el piloto mete su URL y
            // su clave de phpVMS, que es justo lo que la plantilla no puede traer.
            _mainForm.OpenSettingsOnStart = configOutcome == ConfigInstallOutcome.Installed;
            Application.Run(_mainForm);
        }

        /// <summary>
        /// Excepción que nadie capturó en el hilo de la UI. Registra la excepción completa en el log,
        /// avisa al piloto en su idioma y **devuelve el control**: al salir de aquí `Application.Run`
        /// sigue, así que el vuelo en curso no se pierde.
        ///
        /// El texto depende del **módulo que de verdad falló**, no de una suposición (v0.9.64): la traza
        /// real de la 0.9.63 era `System.Data.SQLite.UnsafeNativeMethods.sqlite3_libversion()` desde el
        /// constructor estático de `GMapControl`, y el aviso decía pdfium porque el helper lo asumía.
        /// `NativeFailure` detecta `SQLite.Interop`, `pdfium` o «no identificado» en la traza, y el
        /// detalle técnico lleva la bitness y el estado de las cuatro nativas del paquete. Todo va en
        /// `try/catch` mudo porque este es el último recurso: si el aviso mismo falla, la aplicación se
        /// queda viva.
        /// </summary>
        private static void OnThreadException(object sender, ThreadExceptionEventArgs e)
        {
            try
            {
                if (_reporting) return;
                _reporting = true;

                Exception ex = e != null ? e.Exception : null;
                LogToAppLog(ex);

                string detail = NativeFailure.DescribeFailure(
                    ex, AppDomain.CurrentDomain.BaseDirectory);

                string key = NativeFailure.MessageKeyFor(ex, NativeFailure.GlobalGenericMessageKey);
                string message = L._(key);
                // Las claves de módulo no llevan hueco: el detalle se añade debajo para que nunca se
                // pierda. La genérica sí lo lleva (`{0}`) y se formatea como siempre.
                message = message.IndexOf("{0}", StringComparison.Ordinal) >= 0
                    ? L._(key, detail)
                    : message.TrimEnd() + "\n\n" + detail;

                MessageBox.Show(
                    message,
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
