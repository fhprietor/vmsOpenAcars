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
        private readonly PdfViewer _pdfViewer;
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

            // El PDF se carga AQUÍ, en el constructor, y hasta v0.9.60 cualquier fallo de la nativa
            // subía sin control hasta `BtnOfp_Click` (un `async void`) y acababa en el diálogo genérico
            // de .NET: «Se ha intentado cargar un programa con un formato incorrecto (0x8007000B)».
            // Es lo que le pasó al piloto de la 0.9.60 en un Windows de 32 bits: el paquete solo traía
            // `pdfium.dll` x64, así que el proceso de 32 bits no tenía nativa que cargar. La ventana se
            // abre igual, con el texto en su idioma y el ACARS intacto; el vuelo no depende de esto.
            var content = new Panel { Dock = DockStyle.Fill, BackColor = BackColor };
            _pdfViewer = new PdfViewer
            {
                Dock = DockStyle.Fill,
                ShowToolbar = true
            };
            content.Controls.Add(_pdfViewer);

            try
            {
                _pdfViewer.Document = PdfDocument.Load(pdfPath);
            }
            catch (Exception ex)
            {
                _pdfViewer.Visible = false;
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
                Height = 44,
                ForeColor = Color.FromArgb(130, 136, 150),
                Font = new Font("Consolas", 8f),
                Text = $"{ex.GetType().Name}: {ex.Message}"
            };

            panel.Controls.Add(message);
            panel.Controls.Add(detail);
            return panel;
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _pdfViewer.Document?.Dispose();
            base.OnFormClosed(e);
            try { if (File.Exists(_tempFilePath)) File.Delete(_tempFilePath); }
            catch { }
        }
    }
}
