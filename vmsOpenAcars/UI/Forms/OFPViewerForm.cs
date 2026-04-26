using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using PdfiumViewer;
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

            _pdfViewer = new PdfViewer
            {
                Dock = DockStyle.Fill,
                ShowToolbar = true,
                Document = PdfDocument.Load(pdfPath)
            };

            Controls.Add(_pdfViewer);
            Controls.Add(toolbar);
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
