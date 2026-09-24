using System;
using System.Threading.Tasks;

namespace vmsOpenAcars.Helpers
{
    /// <summary>
    /// Ejecuta trabajo en segundo plano que por diseño no se espera (*fire-and-forget*).
    ///
    /// Su razón de existir es la observación de excepciones. Una tarea que nadie espera y
    /// que falla deja la excepción sin observar: en .NET Framework 4.x eso termina, según
    /// la configuración, en un `TaskScheduler.UnobservedTaskException` silencioso o en un
    /// cierre del proceso — y ocurría en sitios como la descarga del METAR o del PDF del
    /// OFP, donde el usuario solo veía que "no pasó nada".
    ///
    /// No sustituye a `async`/`await`: úsalo solo cuando bloquear el flujo sea
    /// deliberadamente indeseable (arrancar una descarga desde el hilo de UI, o desde el
    /// hilo de telemetría a 20 Hz).
    /// </summary>
    internal static class FireAndForget
    {
        /// <summary>
        /// Arranca <paramref name="work"/> en el thread-pool sin esperarlo, registrando
        /// cualquier excepción con <paramref name="onError"/> en lugar de dejarla sin
        /// observar.
        /// </summary>
        internal static void Run(Func<Task> work, Action<Exception> onError = null,
                                 string operationName = null)
        {
            if (work == null) return;

            // Task.Run con Func<Task> desenvuelve la tarea interna, así que las excepciones
            // del trabajo asíncrono llegan a la continuación (no solo las del arranque).
            _ = Task.Run(work).ContinueWith(
                t =>
                {
                    // Se lee la excepción para marcarla como observada aunque no se reporte.
                    var ex = t.Exception?.GetBaseException();
                    if (onError != null) onError(ex);
                    else if (ex != null)
                        System.Diagnostics.Debug.WriteLine(
                            $"[FireAndForget] {operationName ?? "tarea"}: {ex.Message}");
                },
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously);
        }
    }
}
