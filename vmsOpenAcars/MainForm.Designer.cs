namespace vmsOpenAcars
{
    partial class MainForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.lblLat = new System.Windows.Forms.Label();
            this.lblAlt = new System.Windows.Forms.Label();
            this.lblServerStatus = new System.Windows.Forms.Label();
            this.telemetryTimer = new System.Windows.Forms.Timer(this.components);
            this.lblLon = new System.Windows.Forms.Label();
            this.btnStartFlight = new System.Windows.Forms.Button();
            this.lblStatus = new System.Windows.Forms.Label();
            this.lblNetworkStatus = new System.Windows.Forms.Label();
            this.txtSimbriefId = new System.Windows.Forms.TextBox();
            this.lblSimbriefId = new System.Windows.Forms.Label();
            this.grpFlightData = new System.Windows.Forms.GroupBox();
            this.lblLastUpdate = new System.Windows.Forms.Label();
            this.lblCurrentPhase = new System.Windows.Forms.Label();
            this.lblFlightNumber = new System.Windows.Forms.Label();
            this.btnFetchSimbrief = new System.Windows.Forms.Button();
            this.chkMockMode = new System.Windows.Forms.CheckBox();
            this.lstLogs = new System.Windows.Forms.ListBox();
            this.btnFinishFlight = new System.Windows.Forms.Button();
            this.lblPilotInfo = new System.Windows.Forms.Label();
            this.lblRank = new System.Windows.Forms.Label();
            this.btnLogin = new System.Windows.Forms.Button();
            this.grpFlightData.SuspendLayout();
            this.SuspendLayout();
            // 
            // lblLat
            // 
            this.lblLat.AutoSize = true;
            this.lblLat.Location = new System.Drawing.Point(104, 242);
            this.lblLat.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.lblLat.Name = "lblLat";
            this.lblLat.Size = new System.Drawing.Size(47, 20);
            this.lblLat.TabIndex = 0;
            this.lblLat.Text = "lblLat";
            // 
            // lblAlt
            // 
            this.lblAlt.AutoSize = true;
            this.lblAlt.Location = new System.Drawing.Point(108, 315);
            this.lblAlt.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.lblAlt.Name = "lblAlt";
            this.lblAlt.Size = new System.Drawing.Size(43, 20);
            this.lblAlt.TabIndex = 1;
            this.lblAlt.Text = "lblAlt";
            // 
            // lblServerStatus
            // 
            this.lblServerStatus.AutoSize = true;
            this.lblServerStatus.Location = new System.Drawing.Point(108, 546);
            this.lblServerStatus.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.lblServerStatus.Name = "lblServerStatus";
            this.lblServerStatus.Size = new System.Drawing.Size(117, 20);
            this.lblServerStatus.TabIndex = 2;
            this.lblServerStatus.Text = "lblServerStatus";
            // 
            // telemetryTimer
            // 
            this.telemetryTimer.Tick += new System.EventHandler(this.TelemetryTimer_Tick);
            // 
            // lblLon
            // 
            this.lblLon.AutoSize = true;
            this.lblLon.Location = new System.Drawing.Point(104, 280);
            this.lblLon.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.lblLon.Name = "lblLon";
            this.lblLon.Size = new System.Drawing.Size(51, 20);
            this.lblLon.TabIndex = 3;
            this.lblLon.Text = "lblLon";
            // 
            // btnStartFlight
            // 
            this.btnStartFlight.Location = new System.Drawing.Point(108, 414);
            this.btnStartFlight.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.btnStartFlight.Name = "btnStartFlight";
            this.btnStartFlight.Size = new System.Drawing.Size(112, 35);
            this.btnStartFlight.TabIndex = 4;
            this.btnStartFlight.Text = "Start Flight";
            this.btnStartFlight.UseVisualStyleBackColor = true;
            this.btnStartFlight.Click += new System.EventHandler(this.btnStartFlight_Click);
            // 
            // lblStatus
            // 
            this.lblStatus.AutoSize = true;
            this.lblStatus.Location = new System.Drawing.Point(108, 509);
            this.lblStatus.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.lblStatus.Name = "lblStatus";
            this.lblStatus.Size = new System.Drawing.Size(71, 20);
            this.lblStatus.TabIndex = 5;
            this.lblStatus.Text = "lblStatus";
            // 
            // lblNetworkStatus
            // 
            this.lblNetworkStatus.AutoSize = true;
            this.lblNetworkStatus.Location = new System.Drawing.Point(108, 582);
            this.lblNetworkStatus.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.lblNetworkStatus.Name = "lblNetworkStatus";
            this.lblNetworkStatus.Size = new System.Drawing.Size(129, 20);
            this.lblNetworkStatus.TabIndex = 6;
            this.lblNetworkStatus.Text = "lblNetworkStatus";
            // 
            // txtSimbriefId
            // 
            this.txtSimbriefId.Location = new System.Drawing.Point(105, 20);
            this.txtSimbriefId.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.txtSimbriefId.Name = "txtSimbriefId";
            this.txtSimbriefId.Size = new System.Drawing.Size(148, 26);
            this.txtSimbriefId.TabIndex = 7;
            this.txtSimbriefId.Text = "1199171";
            // 
            // lblSimbriefId
            // 
            this.lblSimbriefId.AutoSize = true;
            this.lblSimbriefId.Location = new System.Drawing.Point(9, 25);
            this.lblSimbriefId.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.lblSimbriefId.Name = "lblSimbriefId";
            this.lblSimbriefId.Size = new System.Drawing.Size(88, 20);
            this.lblSimbriefId.TabIndex = 8;
            this.lblSimbriefId.Text = "Simbrief ID";
            // 
            // grpFlightData
            // 
            this.grpFlightData.Controls.Add(this.lblLastUpdate);
            this.grpFlightData.Controls.Add(this.lblCurrentPhase);
            this.grpFlightData.Controls.Add(this.lblFlightNumber);
            this.grpFlightData.Controls.Add(this.btnFetchSimbrief);
            this.grpFlightData.Controls.Add(this.txtSimbriefId);
            this.grpFlightData.Controls.Add(this.lblSimbriefId);
            this.grpFlightData.Location = new System.Drawing.Point(436, 331);
            this.grpFlightData.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.grpFlightData.Name = "grpFlightData";
            this.grpFlightData.Padding = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.grpFlightData.Size = new System.Drawing.Size(322, 235);
            this.grpFlightData.TabIndex = 9;
            this.grpFlightData.TabStop = false;
            this.grpFlightData.Text = "Flight Data";
            // 
            // lblLastUpdate
            // 
            this.lblLastUpdate.AutoSize = true;
            this.lblLastUpdate.Location = new System.Drawing.Point(122, 158);
            this.lblLastUpdate.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.lblLastUpdate.Name = "lblLastUpdate";
            this.lblLastUpdate.Size = new System.Drawing.Size(108, 20);
            this.lblLastUpdate.TabIndex = 13;
            this.lblLastUpdate.Text = "lblLastUpdate";
            // 
            // lblCurrentPhase
            // 
            this.lblCurrentPhase.AutoSize = true;
            this.lblCurrentPhase.Location = new System.Drawing.Point(112, 120);
            this.lblCurrentPhase.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.lblCurrentPhase.Name = "lblCurrentPhase";
            this.lblCurrentPhase.Size = new System.Drawing.Size(122, 20);
            this.lblCurrentPhase.TabIndex = 12;
            this.lblCurrentPhase.Text = "lblCurrentPhase";
            // 
            // lblFlightNumber
            // 
            this.lblFlightNumber.AutoSize = true;
            this.lblFlightNumber.Location = new System.Drawing.Point(100, 55);
            this.lblFlightNumber.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.lblFlightNumber.Name = "lblFlightNumber";
            this.lblFlightNumber.Size = new System.Drawing.Size(119, 20);
            this.lblFlightNumber.TabIndex = 10;
            this.lblFlightNumber.Text = "lblFlightNumber";
            // 
            // btnFetchSimbrief
            // 
            this.btnFetchSimbrief.Location = new System.Drawing.Point(54, 177);
            this.btnFetchSimbrief.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.btnFetchSimbrief.Name = "btnFetchSimbrief";
            this.btnFetchSimbrief.Size = new System.Drawing.Size(249, 35);
            this.btnFetchSimbrief.TabIndex = 9;
            this.btnFetchSimbrief.Text = "Fetch Simbrief Plan";
            this.btnFetchSimbrief.UseVisualStyleBackColor = true;
            this.btnFetchSimbrief.Click += new System.EventHandler(this.btnFetchSimbrief_Click);
            // 
            // chkMockMode
            // 
            this.chkMockMode.AutoSize = true;
            this.chkMockMode.Location = new System.Drawing.Point(108, 631);
            this.chkMockMode.Name = "chkMockMode";
            this.chkMockMode.Size = new System.Drawing.Size(148, 24);
            this.chkMockMode.TabIndex = 10;
            this.chkMockMode.Text = "Simulador Mock";
            this.chkMockMode.UseVisualStyleBackColor = true;
            // 
            // lstLogs
            // 
            this.lstLogs.FormattingEnabled = true;
            this.lstLogs.ItemHeight = 20;
            this.lstLogs.Location = new System.Drawing.Point(777, 334);
            this.lstLogs.Name = "lstLogs";
            this.lstLogs.Size = new System.Drawing.Size(1127, 324);
            this.lstLogs.TabIndex = 11;
            // 
            // btnFinishFlight
            // 
            this.btnFinishFlight.Location = new System.Drawing.Point(238, 414);
            this.btnFinishFlight.Name = "btnFinishFlight";
            this.btnFinishFlight.Size = new System.Drawing.Size(160, 35);
            this.btnFinishFlight.TabIndex = 12;
            this.btnFinishFlight.Text = "Enviar PIREP";
            this.btnFinishFlight.UseVisualStyleBackColor = true;
            this.btnFinishFlight.Click += new System.EventHandler(this.btnFinishFlight_Click);
            // 
            // lblPilotInfo
            // 
            this.lblPilotInfo.AutoSize = true;
            this.lblPilotInfo.Location = new System.Drawing.Point(287, 242);
            this.lblPilotInfo.Name = "lblPilotInfo";
            this.lblPilotInfo.Size = new System.Drawing.Size(82, 20);
            this.lblPilotInfo.TabIndex = 13;
            this.lblPilotInfo.Text = "lblPilotInfo";
            // 
            // lblRank
            // 
            this.lblRank.AutoSize = true;
            this.lblRank.Location = new System.Drawing.Point(291, 285);
            this.lblRank.Name = "lblRank";
            this.lblRank.Size = new System.Drawing.Size(62, 20);
            this.lblRank.TabIndex = 14;
            this.lblRank.Text = "lblRank";
            // 
            // btnLogin
            // 
            this.btnLogin.Location = new System.Drawing.Point(112, 365);
            this.btnLogin.Name = "btnLogin";
            this.btnLogin.Size = new System.Drawing.Size(99, 41);
            this.btnLogin.TabIndex = 15;
            this.btnLogin.Text = "Login";
            this.btnLogin.UseVisualStyleBackColor = true;
            this.btnLogin.Click += new System.EventHandler(this.btnLogin_Click);
            // 
            // MainForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(9F, 20F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1916, 692);
            this.Controls.Add(this.btnLogin);
            this.Controls.Add(this.lblRank);
            this.Controls.Add(this.lblPilotInfo);
            this.Controls.Add(this.btnFinishFlight);
            this.Controls.Add(this.lstLogs);
            this.Controls.Add(this.chkMockMode);
            this.Controls.Add(this.grpFlightData);
            this.Controls.Add(this.lblNetworkStatus);
            this.Controls.Add(this.lblStatus);
            this.Controls.Add(this.btnStartFlight);
            this.Controls.Add(this.lblLon);
            this.Controls.Add(this.lblServerStatus);
            this.Controls.Add(this.lblAlt);
            this.Controls.Add(this.lblLat);
            this.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.Name = "MainForm";
            this.Text = "Form1";
            this.Load += new System.EventHandler(this.Form1_Load);
            this.grpFlightData.ResumeLayout(false);
            this.grpFlightData.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label lblLat;
        private System.Windows.Forms.Label lblAlt;
        private System.Windows.Forms.Label lblServerStatus;
        private System.Windows.Forms.Timer telemetryTimer;
        private System.Windows.Forms.Label lblLon;
        private System.Windows.Forms.Button btnStartFlight;
        private System.Windows.Forms.Label lblStatus;
        private System.Windows.Forms.Label lblNetworkStatus;
        private System.Windows.Forms.TextBox txtSimbriefId;
        private System.Windows.Forms.Label lblSimbriefId;
        private System.Windows.Forms.GroupBox grpFlightData;
        private System.Windows.Forms.Label lblFlightNumber;
        private System.Windows.Forms.Button btnFetchSimbrief;
        private System.Windows.Forms.Label lblCurrentPhase;
        private System.Windows.Forms.Label lblLastUpdate;
        private System.Windows.Forms.CheckBox chkMockMode;
        private System.Windows.Forms.ListBox lstLogs;
        private System.Windows.Forms.Button btnFinishFlight;
        private System.Windows.Forms.Label lblPilotInfo;
        private System.Windows.Forms.Label lblRank;
        private System.Windows.Forms.Button btnLogin;
    }
}

