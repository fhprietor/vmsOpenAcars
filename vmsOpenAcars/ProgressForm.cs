using System.Drawing;
using System.Windows.Forms;

namespace vmsOpenAcars
{
    public class ProgressForm : Form
    {
        private Label labelStatus;
        private ProgressBar progressBar1;

        public ProgressForm()
        {
            // Form
            Text = "Actualizando vmsOpenAcars";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            ControlBox = false;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            Size = new Size(420, 100);

            // Label
            labelStatus = new Label
            {
                Text = "Preparando descarga...",
                AutoSize = false,
                Width = 400,
                Height = 20,
                Location = new Point(10, 15),
                Font = new Font("Segoe UI", 9f)
            };

            // ProgressBar
            progressBar1 = new ProgressBar
            {
                Minimum = 0,
                Maximum = 100,
                Value = 0,
                Width = 390,
                Height = 23,
                Location = new Point(10, 42)
            };

            Controls.Add(labelStatus);
            Controls.Add(progressBar1);
        }

        public void SetProgress(int percent)
        {
            progressBar1.Value = percent;
            labelStatus.Text = $"Descargando actualización... {percent}%";
            Application.DoEvents();
        }
    }
}