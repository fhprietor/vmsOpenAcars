namespace vmsOpenAcars
{
    partial class Form1
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
            this.lblRoute = new System.Windows.Forms.Label();
            this.lblFlightNumber = new System.Windows.Forms.Label();
            this.btnFetchSimbrief = new System.Windows.Forms.Button();
            this.lblCurrentPhase = new System.Windows.Forms.Label();
            this.lblLastUpdate = new System.Windows.Forms.Label();
            this.grpFlightData.SuspendLayout();
            this.SuspendLayout();
            // 
            // lblLat
            // 
            this.lblLat.AutoSize = true;
            this.lblLat.Location = new System.Drawing.Point(69, 157);
            this.lblLat.Name = "lblLat";
            this.lblLat.Size = new System.Drawing.Size(32, 13);
            this.lblLat.TabIndex = 0;
            this.lblLat.Text = "lblLat";
            // 
            // lblAlt
            // 
            this.lblAlt.AutoSize = true;
            this.lblAlt.Location = new System.Drawing.Point(72, 205);
            this.lblAlt.Name = "lblAlt";
            this.lblAlt.Size = new System.Drawing.Size(29, 13);
            this.lblAlt.TabIndex = 1;
            this.lblAlt.Text = "lblAlt";
            // 
            // lblServerStatus
            // 
            this.lblServerStatus.AutoSize = true;
            this.lblServerStatus.Location = new System.Drawing.Point(72, 355);
            this.lblServerStatus.Name = "lblServerStatus";
            this.lblServerStatus.Size = new System.Drawing.Size(78, 13);
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
            this.lblLon.Location = new System.Drawing.Point(69, 182);
            this.lblLon.Name = "lblLon";
            this.lblLon.Size = new System.Drawing.Size(35, 13);
            this.lblLon.TabIndex = 3;
            this.lblLon.Text = "lblLon";
            // 
            // btnStartFlight
            // 
            this.btnStartFlight.Location = new System.Drawing.Point(75, 238);
            this.btnStartFlight.Name = "btnStartFlight";
            this.btnStartFlight.Size = new System.Drawing.Size(75, 23);
            this.btnStartFlight.TabIndex = 4;
            this.btnStartFlight.Text = "Start Flight";
            this.btnStartFlight.UseVisualStyleBackColor = true;
            this.btnStartFlight.Click += new System.EventHandler(this.btnStartFlight_Click);
            // 
            // lblStatus
            // 
            this.lblStatus.AutoSize = true;
            this.lblStatus.Location = new System.Drawing.Point(72, 331);
            this.lblStatus.Name = "lblStatus";
            this.lblStatus.Size = new System.Drawing.Size(47, 13);
            this.lblStatus.TabIndex = 5;
            this.lblStatus.Text = "lblStatus";
            // 
            // lblNetworkStatus
            // 
            this.lblNetworkStatus.AutoSize = true;
            this.lblNetworkStatus.Location = new System.Drawing.Point(72, 378);
            this.lblNetworkStatus.Name = "lblNetworkStatus";
            this.lblNetworkStatus.Size = new System.Drawing.Size(87, 13);
            this.lblNetworkStatus.TabIndex = 6;
            this.lblNetworkStatus.Text = "lblNetworkStatus";
            // 
            // txtSimbriefId
            // 
            this.txtSimbriefId.Location = new System.Drawing.Point(70, 13);
            this.txtSimbriefId.Name = "txtSimbriefId";
            this.txtSimbriefId.Size = new System.Drawing.Size(100, 20);
            this.txtSimbriefId.TabIndex = 7;
            this.txtSimbriefId.Text = "1199171";
            // 
            // lblSimbriefId
            // 
            this.lblSimbriefId.AutoSize = true;
            this.lblSimbriefId.Location = new System.Drawing.Point(6, 16);
            this.lblSimbriefId.Name = "lblSimbriefId";
            this.lblSimbriefId.Size = new System.Drawing.Size(58, 13);
            this.lblSimbriefId.TabIndex = 8;
            this.lblSimbriefId.Text = "Simbrief ID";
            // 
            // grpFlightData
            // 
            this.grpFlightData.Controls.Add(this.lblLastUpdate);
            this.grpFlightData.Controls.Add(this.lblCurrentPhase);
            this.grpFlightData.Controls.Add(this.lblRoute);
            this.grpFlightData.Controls.Add(this.lblFlightNumber);
            this.grpFlightData.Controls.Add(this.btnFetchSimbrief);
            this.grpFlightData.Controls.Add(this.txtSimbriefId);
            this.grpFlightData.Controls.Add(this.lblSimbriefId);
            this.grpFlightData.Location = new System.Drawing.Point(298, 17);
            this.grpFlightData.Name = "grpFlightData";
            this.grpFlightData.Size = new System.Drawing.Size(310, 153);
            this.grpFlightData.TabIndex = 9;
            this.grpFlightData.TabStop = false;
            this.grpFlightData.Text = "Flight Data";
            // 
            // lblRoute
            // 
            this.lblRoute.AutoSize = true;
            this.lblRoute.Location = new System.Drawing.Point(70, 53);
            this.lblRoute.Name = "lblRoute";
            this.lblRoute.Size = new System.Drawing.Size(46, 13);
            this.lblRoute.TabIndex = 11;
            this.lblRoute.Text = "lblRoute";
            // 
            // lblFlightNumber
            // 
            this.lblFlightNumber.AutoSize = true;
            this.lblFlightNumber.Location = new System.Drawing.Point(67, 36);
            this.lblFlightNumber.Name = "lblFlightNumber";
            this.lblFlightNumber.Size = new System.Drawing.Size(79, 13);
            this.lblFlightNumber.TabIndex = 10;
            this.lblFlightNumber.Text = "lblFlightNumber";
            // 
            // btnFetchSimbrief
            // 
            this.btnFetchSimbrief.Location = new System.Drawing.Point(36, 115);
            this.btnFetchSimbrief.Name = "btnFetchSimbrief";
            this.btnFetchSimbrief.Size = new System.Drawing.Size(166, 23);
            this.btnFetchSimbrief.TabIndex = 9;
            this.btnFetchSimbrief.Text = "Fetch Simbrief Plan";
            this.btnFetchSimbrief.UseVisualStyleBackColor = true;
            this.btnFetchSimbrief.Click += new System.EventHandler(this.btnFetchSimbrief_Click);
            // 
            // lblCurrentPhase
            // 
            this.lblCurrentPhase.AutoSize = true;
            this.lblCurrentPhase.Location = new System.Drawing.Point(75, 78);
            this.lblCurrentPhase.Name = "lblCurrentPhase";
            this.lblCurrentPhase.Size = new System.Drawing.Size(81, 13);
            this.lblCurrentPhase.TabIndex = 12;
            this.lblCurrentPhase.Text = "lblCurrentPhase";
            // 
            // lblLastUpdate
            // 
            this.lblLastUpdate.AutoSize = true;
            this.lblLastUpdate.Location = new System.Drawing.Point(81, 103);
            this.lblLastUpdate.Name = "lblLastUpdate";
            this.lblLastUpdate.Size = new System.Drawing.Size(72, 13);
            this.lblLastUpdate.TabIndex = 13;
            this.lblLastUpdate.Text = "lblLastUpdate";
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(800, 450);
            this.Controls.Add(this.grpFlightData);
            this.Controls.Add(this.lblNetworkStatus);
            this.Controls.Add(this.lblStatus);
            this.Controls.Add(this.btnStartFlight);
            this.Controls.Add(this.lblLon);
            this.Controls.Add(this.lblServerStatus);
            this.Controls.Add(this.lblAlt);
            this.Controls.Add(this.lblLat);
            this.Name = "Form1";
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
        private System.Windows.Forms.Label lblRoute;
        private System.Windows.Forms.Label lblFlightNumber;
        private System.Windows.Forms.Button btnFetchSimbrief;
        private System.Windows.Forms.Label lblCurrentPhase;
        private System.Windows.Forms.Label lblLastUpdate;
    }
}

