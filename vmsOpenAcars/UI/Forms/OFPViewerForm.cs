using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using PdfiumViewer;
using vmsOpenAcars.Helpers;
using vmsOpenAcars.UI;

namespace vmsOpenAcars.UI.Forms
{
    public class OFPViewerForm : Form
    {
        // No es `readonly` a propósito (v0.9.63): si crear el control PdfViewer revienta porque la
        // nativa de PDF no carga, el campo se queda en null y la ventana sigue viva con el aviso.
        private PdfViewer _pdfViewer;
        private readonly string _tempFilePath;

        public OFPViewerForm(string pdfPath, string title)
        {
            _tempFilePath = pdfPath;

            Text = $"OFP — {title}";
            Size = new Size(920, 760);
            MinimumSize = new Size(640, 500);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Color.FromArgb(12, 14, 22);
            FormBorderStyle = FormBorderStyle.Sizable;
            Icon = System.Drawing.Icon.ExtractAssociatedIcon(
                System.Reflection.Assembly.GetExecutingAssembly().Location);

            var toolbar = new Panel
            {
                Dock = DockStyle.Top,
                Height = 30,
                BackColor = Color.FromArgb(15, 18, 28),
                Padding = new Padding(6, 0, 6, 0)
            };

            toolbar.Controls.Add(new Label
            {
                Text = $"📄  {title}",
                Font = new Font("Consolas", 9, FontStyle.Bold),
                ForeColor = Color.Gold,
                AutoSize = true,
                Location = new Point(8, 7),
                Margin = Padding.Empty
            });

            // El visor se construye DENTRO del try desde v0.9.63. La 0.9.61 solo protegía
            // `PdfDocument.Load`, y ahí no estaba el fallo: crear el control `PdfViewer` ya toca los
            // tipos de PdfiumViewer y dispara su constructor estático, que carga `pdfium.dll`. En un
            // Windows de 32 bits sin `x86\pdfium.dll` eso lanza `BadImageFormatException`
            // (0x8007000B, el mismo del reporte) desde el constructor de ESTE formulario, fuera del
            // try, y el cuadro genérico de .NET se comía el vuelo. Ahora la creación, el `Add` y la
            // carga van juntos: si algo falla, `_pdfViewer` queda en null, se pinta el aviso legible
            // y el resto del formulario —y el ACARS— sigue funcionando.
            var content = new Panel { Dock = DockStyle.Fill, BackColor = BackColor };
            try
            {
                _pdfViewer = new PdfViewer
                {
                    Dock = DockStyle.Fill,
                    ShowToolbar = true
                };
                content.Controls.Add(_pdfViewer);
                _pdfViewer.Document = PdfDocument.Load(pdfPath);
            }
            catch (Exception ex)
            {
                if (_pdfViewer != null)
                {
                    // El control pudo llegar a crearse y ser el `Document` lo que falló: se retira para
                    // que no tape el aviso y se libera. `Visible = false` a secas dejaría el control
                    // (y su nativa) vivo debajo del panel de error.
                    try
                    {
                        content.Controls.Remove(_pdfViewer);
                        _pdfViewer.Dispose();
                    }
                    catch { }
                    _pdfViewer = null;
                }
                content.Controls.Add(BuildFailurePanel(ex));
            }

            Controls.Add(content);
            Controls.Add(toolbar);
        }

        /// <summary>
        /// El aviso que sustituye al diálogo de .NET. La clave la decide `PdfNativeFailure` (helper puro
        /// con su test): la específica de «falta la librería de PDF» o la genérica de «no se pudo abrir».
        /// El detalle técnico va en gris para que el próximo reporte traiga el tipo de excepción y no
        /// solo una captura del diálogo del sistema.
        /// </summary>
        private Panel BuildFailurePanel(Exception ex)
        {
            var panel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = BackColor,
                Padding = new Padding(28, 24, 28, 24),
                AutoScroll = true
            };

            var message = new Label
            {
                Dock = DockStyle.Fill,
                AutoSize = false,
                ForeColor = Color.Gainsboro,
                Font = new Font("Segoe UI", 10f),
                Text = L._(PdfNativeFailure.MessageKeyFor(ex))
            };

            var detail = new Label
            {
                Dock = DockStyle.Bottom,
                AutoSize = false,
                Height = 60,
                ForeColor = Color.FromArgb(130, 136, 150),
                Font = new Font("Consolas", 8f),
                // Desde v0.9.63 el detalle lleva, además de la excepción, la bitness del proceso y la
                // ruta de la nativa que se esperaba con si existe: el próximo reporte ya no obliga a
                // adivinar por qué Windows de 32 bits no encuentra `x86\pdfium.dll`.
                Text = PdfNativeFailure.DescribeFailure(ex, AppDomain.CurrentDomain.BaseDirectory)
            };

            panel.Controls.Add(message);
            panel.Controls.Add(detail);
            return panel;
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            // `_pdfViewer` puede ser null: es el caso en que la nativa no cargó y la ventana se quedó
            // con el aviso. Sin esta guarda, cerrar la ventana daría NullReferenceException.
            try
            {
                if (_pdfViewer != null)
                {
                    _pdfViewer.Document?.Dispose();
                    _pdfViewer.Dispose();
                }
            }
            catch { }
            base.OnFormClosed(e);
            try { if (File.Exists(_tempFilePath)) File.Delete(_tempFilePath); }
            catch { }
        }
    }
}
