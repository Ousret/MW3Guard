namespace PS3API_Demo
{
    partial class Main
    {
        /// <summary>
        /// Variable nécessaire au concepteur.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Nettoyage des ressources utilisées.
        /// </summary>
        /// <param name="disposing">true si les ressources managées doivent être supprimées ; sinon, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Code généré par le Concepteur Windows Form

        /// <summary>
        /// Méthode requise pour la prise en charge du concepteur - ne modifiez pas
        /// le contenu de cette méthode avec l'éditeur de code.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Main));
            this.groupConnect = new System.Windows.Forms.GroupBox();
            this.btnConnect = new System.Windows.Forms.Button();
            this.radioCC = new System.Windows.Forms.RadioButton();
            this.radioTM = new System.Windows.Forms.RadioButton();
            this.groupAttach = new System.Windows.Forms.GroupBox();
            this.btnAttach = new System.Windows.Forms.Button();
            this.groupMem = new System.Windows.Forms.GroupBox();
            this.btnSetGuarder = new System.Windows.Forms.Button();
            this.dataGridView1 = new System.Windows.Forms.DataGridView();
            this.button1 = new System.Windows.Forms.Button();
            this.label1 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.label3 = new System.Windows.Forms.Label();
            this.comboReasons = new System.Windows.Forms.ComboBox();
            this.ID = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.Player = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.Score = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.Kill = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.Death = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.CampN = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.UAV = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.groupConnect.SuspendLayout();
            this.groupAttach.SuspendLayout();
            this.groupMem.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView1)).BeginInit();
            this.groupBox1.SuspendLayout();
            this.SuspendLayout();
            // 
            // groupConnect
            // 
            this.groupConnect.Controls.Add(this.btnConnect);
            this.groupConnect.Controls.Add(this.radioCC);
            this.groupConnect.Controls.Add(this.radioTM);
            this.groupConnect.Location = new System.Drawing.Point(15, 12);
            this.groupConnect.Name = "groupConnect";
            this.groupConnect.Size = new System.Drawing.Size(200, 100);
            this.groupConnect.TabIndex = 0;
            this.groupConnect.TabStop = false;
            this.groupConnect.Text = "1. Connection Panel ";
            // 
            // btnConnect
            // 
            this.btnConnect.Location = new System.Drawing.Point(32, 27);
            this.btnConnect.Name = "btnConnect";
            this.btnConnect.Size = new System.Drawing.Size(137, 28);
            this.btnConnect.TabIndex = 2;
            this.btnConnect.Text = "Connect";
            this.btnConnect.UseVisualStyleBackColor = true;
            this.btnConnect.Click += new System.EventHandler(this.btnConnect_Click);
            // 
            // radioCC
            // 
            this.radioCC.AutoSize = true;
            this.radioCC.Location = new System.Drawing.Point(113, 64);
            this.radioCC.Name = "radioCC";
            this.radioCC.Size = new System.Drawing.Size(56, 17);
            this.radioCC.TabIndex = 1;
            this.radioCC.Text = "CCAPI\r\n";
            this.radioCC.UseVisualStyleBackColor = true;
            this.radioCC.CheckedChanged += new System.EventHandler(this.radioCC_CheckedChanged);
            // 
            // radioTM
            // 
            this.radioTM.AutoSize = true;
            this.radioTM.Checked = true;
            this.radioTM.Location = new System.Drawing.Point(32, 64);
            this.radioTM.Name = "radioTM";
            this.radioTM.Size = new System.Drawing.Size(58, 17);
            this.radioTM.TabIndex = 0;
            this.radioTM.TabStop = true;
            this.radioTM.Text = "TMAPI\r\n";
            this.radioTM.UseVisualStyleBackColor = true;
            this.radioTM.CheckedChanged += new System.EventHandler(this.radioTM_CheckedChanged);
            // 
            // groupAttach
            // 
            this.groupAttach.Controls.Add(this.btnAttach);
            this.groupAttach.Location = new System.Drawing.Point(221, 12);
            this.groupAttach.Name = "groupAttach";
            this.groupAttach.Size = new System.Drawing.Size(189, 100);
            this.groupAttach.TabIndex = 1;
            this.groupAttach.TabStop = false;
            this.groupAttach.Text = "2. Attach Process";
            // 
            // btnAttach
            // 
            this.btnAttach.Location = new System.Drawing.Point(25, 27);
            this.btnAttach.Name = "btnAttach";
            this.btnAttach.Size = new System.Drawing.Size(137, 54);
            this.btnAttach.TabIndex = 3;
            this.btnAttach.Text = "Attach Game Process";
            this.btnAttach.UseVisualStyleBackColor = true;
            this.btnAttach.Click += new System.EventHandler(this.btnAttach_Click);
            // 
            // groupMem
            // 
            this.groupMem.Controls.Add(this.btnSetGuarder);
            this.groupMem.Location = new System.Drawing.Point(416, 12);
            this.groupMem.Name = "groupMem";
            this.groupMem.Size = new System.Drawing.Size(167, 100);
            this.groupMem.TabIndex = 2;
            this.groupMem.TabStop = false;
            this.groupMem.Text = "3. Process Guarder";
            this.groupMem.Enter += new System.EventHandler(this.groupMem_Enter);
            // 
            // btnSetGuarder
            // 
            this.btnSetGuarder.Location = new System.Drawing.Point(19, 27);
            this.btnSetGuarder.Name = "btnSetGuarder";
            this.btnSetGuarder.Size = new System.Drawing.Size(137, 54);
            this.btnSetGuarder.TabIndex = 0;
            this.btnSetGuarder.Text = "Initialize Guarder";
            this.btnSetGuarder.UseVisualStyleBackColor = true;
            this.btnSetGuarder.Click += new System.EventHandler(this.btnSetRand_Click);
            // 
            // dataGridView1
            // 
            this.dataGridView1.AllowUserToAddRows = false;
            this.dataGridView1.AllowUserToDeleteRows = false;
            this.dataGridView1.CellBorderStyle = System.Windows.Forms.DataGridViewCellBorderStyle.Raised;
            this.dataGridView1.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dataGridView1.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.ID,
            this.Player,
            this.Score,
            this.Kill,
            this.Death,
            this.CampN,
            this.UAV});
            this.dataGridView1.Location = new System.Drawing.Point(8, 137);
            this.dataGridView1.Name = "dataGridView1";
            this.dataGridView1.ReadOnly = true;
            this.dataGridView1.Size = new System.Drawing.Size(568, 346);
            this.dataGridView1.TabIndex = 3;
            this.dataGridView1.CellContentClick += new System.Windows.Forms.DataGridViewCellEventHandler(this.dataGridView1_CellContentClick);
            // 
            // button1
            // 
            this.button1.Location = new System.Drawing.Point(347, 19);
            this.button1.Name = "button1";
            this.button1.Size = new System.Drawing.Size(96, 21);
            this.button1.TabIndex = 4;
            this.button1.Text = "Manual Kick";
            this.button1.UseVisualStyleBackColor = true;
            this.button1.Click += new System.EventHandler(this.button1_Click);
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(5, 121);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(113, 13);
            this.label1.TabIndex = 6;
            this.label1.Text = "Server instance: None";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Font = new System.Drawing.Font("Franklin Gothic Medium", 21.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label2.ForeColor = System.Drawing.SystemColors.ActiveCaption;
            this.label2.Location = new System.Drawing.Point(492, 508);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(66, 37);
            this.label2.TabIndex = 7;
            this.label2.Text = "OFF";
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.label3);
            this.groupBox1.Controls.Add(this.comboReasons);
            this.groupBox1.Controls.Add(this.button1);
            this.groupBox1.Location = new System.Drawing.Point(8, 489);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(449, 60);
            this.groupBox1.TabIndex = 9;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "Manual mod.";
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Font = new System.Drawing.Font("Calibri", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label3.ForeColor = System.Drawing.Color.DarkRed;
            this.label3.Location = new System.Drawing.Point(6, 42);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(256, 14);
            this.label3.TabIndex = 7;
            this.label3.Text = "With great power comes great responsibility! ";
            // 
            // comboReasons
            // 
            this.comboReasons.FormattingEnabled = true;
            this.comboReasons.Items.AddRange(new object[] {
            "Violent language",
            "Using unauthorized mod.",
            "Play against his own team"});
            this.comboReasons.Location = new System.Drawing.Point(6, 19);
            this.comboReasons.Name = "comboReasons";
            this.comboReasons.Size = new System.Drawing.Size(335, 21);
            this.comboReasons.TabIndex = 6;
            this.comboReasons.Text = "Kick reason";
            // 
            // ID
            // 
            this.ID.HeaderText = "pID";
            this.ID.Name = "ID";
            this.ID.ReadOnly = true;
            this.ID.Resizable = System.Windows.Forms.DataGridViewTriState.True;
            this.ID.Width = 40;
            // 
            // Player
            // 
            this.Player.HeaderText = "Player";
            this.Player.Name = "Player";
            this.Player.ReadOnly = true;
            this.Player.Width = 200;
            // 
            // Score
            // 
            this.Score.HeaderText = "Score";
            this.Score.Name = "Score";
            this.Score.ReadOnly = true;
            this.Score.Width = 60;
            // 
            // Kill
            // 
            this.Kill.HeaderText = "Kill";
            this.Kill.Name = "Kill";
            this.Kill.ReadOnly = true;
            this.Kill.Width = 60;
            // 
            // Death
            // 
            this.Death.HeaderText = "Death";
            this.Death.Name = "Death";
            this.Death.ReadOnly = true;
            this.Death.Width = 60;
            // 
            // CampN
            // 
            this.CampN.HeaderText = "Proba";
            this.CampN.Name = "CampN";
            this.CampN.ReadOnly = true;
            this.CampN.Width = 40;
            // 
            // UAV
            // 
            this.UAV.HeaderText = "UAV";
            this.UAV.Name = "UAV";
            this.UAV.ReadOnly = true;
            this.UAV.Width = 40;
            // 
            // Main
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(584, 561);
            this.Controls.Add(this.groupBox1);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.dataGridView1);
            this.Controls.Add(this.groupMem);
            this.Controls.Add(this.groupAttach);
            this.Controls.Add(this.groupConnect);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MaximumSize = new System.Drawing.Size(600, 600);
            this.MinimumSize = new System.Drawing.Size(600, 600);
            this.Name = "Main";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Modern Warfare 3 Guarder";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.Main_FormClosing);
            this.Load += new System.EventHandler(this.Main_Load);
            this.groupConnect.ResumeLayout(false);
            this.groupConnect.PerformLayout();
            this.groupAttach.ResumeLayout(false);
            this.groupMem.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView1)).EndInit();
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.GroupBox groupConnect;
        private System.Windows.Forms.Button btnConnect;
        private System.Windows.Forms.RadioButton radioCC;
        private System.Windows.Forms.RadioButton radioTM;
        private System.Windows.Forms.GroupBox groupAttach;
        private System.Windows.Forms.Button btnAttach;
        private System.Windows.Forms.GroupBox groupMem;
        private System.Windows.Forms.Button btnSetGuarder;
        private System.Windows.Forms.DataGridView dataGridView1;
        private System.Windows.Forms.Button button1;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.ComboBox comboReasons;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.DataGridViewTextBoxColumn ID;
        private System.Windows.Forms.DataGridViewTextBoxColumn Player;
        private System.Windows.Forms.DataGridViewTextBoxColumn Score;
        private System.Windows.Forms.DataGridViewTextBoxColumn Kill;
        private System.Windows.Forms.DataGridViewTextBoxColumn Death;
        private System.Windows.Forms.DataGridViewTextBoxColumn CampN;
        private System.Windows.Forms.DataGridViewTextBoxColumn UAV;
    }
}

