using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Windows.Forms;
using PS3Lib;
using System.IO;

namespace MW3Guard_PS3
{
    public partial class Main : Form
    {
        private PS3API PS3 = new PS3API();
        private Guarder MW3_BOT;

        protected Thread oThread;
        private Wait loading = new Wait();
        private System.Windows.Forms.Timer MW3_GUARDER = new System.Windows.Forms.Timer();
        private GuardDB handle_db = new GuardDB();

        private bool PS3_CCAPI_CONNECTED = false;
        
        public Main()
        {
            InitializeComponent();
        }

        private void Main_Load(object sender, EventArgs e)
        {
            //Initilize MW3Guard core
            MW3_BOT = new Guarder(PS3);

            MW3_GUARDER.Interval = 500; //Upd8 leaderboard every half sec.
            MW3_GUARDER.Tick += new EventHandler(guarder);

            PS3.ChangeAPI(SelectAPI.ControlConsole); //Sorry dude, this will only work with CCAPI.
            //You could try to convert it to TMAPI if you wanted to.
            
        }

        /// <summary>
        /// Refresh the visual part of the program, use with timer!
        /// </summary>
        public void guarder(object sender, EventArgs e)
        {
            int i = 0, newRow = 0, searchRow = -1;
            Image level;
            //dataGridView1.Rows.Clear();

            for (i = 0; i < MW3_BOT.maxSlots; i++)
            {
                if ((MW3_BOT.c_board[i] != null) && !String.IsNullOrEmpty(MW3_BOT.c_board[i].client_name))
                {
                    /* If row already exist */
                    searchRow = GetClientROWIndex(i); 

                    if (searchRow != -1)
                    {
                        dataGridView1.Rows[searchRow].Cells[3].Value = MW3_BOT.c_board[i].score;
                        dataGridView1.Rows[searchRow].Cells[4].Value = MW3_BOT.c_board[i].kills;
                        dataGridView1.Rows[searchRow].Cells[5].Value = MW3_BOT.c_board[i].deaths;

                        if (MW3_BOT.c_board[i].c_team == 1)
                        {
                            dataGridView1.Rows[searchRow].DefaultCellStyle.BackColor = Color.PowderBlue;
                        }
                        else if (MW3_BOT.c_board[i].c_team == 0)
                        {
                            dataGridView1.Rows[searchRow].DefaultCellStyle.BackColor = Color.White;
                        }else
                        {
                            dataGridView1.Rows[searchRow].DefaultCellStyle.BackColor = Color.DarkOrange;
                        }

                    }
                    else
                    {
                        if (MW3_BOT.c_board[i].n_prestige > 10)
                        {
                            level = Image.FromFile("levels\\p11+.png");
                        }
                        else  if (MW3_BOT.c_board[i].n_prestige > 0)
                        {
                            level = Image.FromFile("levels\\p" + MW3_BOT.c_board[i].n_prestige + ".png");
                        }
                        else
                        {
                            level = Image.FromFile("levels\\80.png");
                        }

                        level = level.GetThumbnailImage(20, 20, null, IntPtr.Zero);

                        newRow = dataGridView1.Rows.Add(i, level, MW3_BOT.c_board[i].client_name, MW3_BOT.c_board[i].score, MW3_BOT.c_board[i].kills, MW3_BOT.c_board[i].deaths);
                        //if (MW3_BOT.c_board[i].c_team == 1) dataGridView1.Rows[newRow].DefaultCellStyle.BackColor = Color.PowderBlue;

                        if (MW3_BOT.c_board[i].c_team == 1)
                        {
                            dataGridView1.Rows[newRow].DefaultCellStyle.BackColor = Color.PowderBlue;
                        }
                        else if (MW3_BOT.c_board[i].c_team == 0)
                        {
                            dataGridView1.Rows[newRow].DefaultCellStyle.BackColor = Color.White;
                        }
                        else
                        {
                            dataGridView1.Rows[newRow].DefaultCellStyle.BackColor = Color.DarkOrange;
                        }

                    }
                }
                else
                {
                    searchRow = GetClientROWIndex(i);
                    if (searchRow != -1) dataGridView1.Rows.RemoveAt(searchRow);
                }

                searchRow = -1;
            }

            /* Update label for GameMode and Host and Map */
            if (MW3_BOT.nbClient == 0)
            {
                label1.Text = "No current running session";
                label2.Text = "OFF";

                comboReasons.Enabled = false;
                button1.Enabled = false;
            }
            else
            {
                label1.Text = "Currently working on " + MW3_BOT.current_maps + ".. .";
                label2.Text = MW3_BOT.nbClient + "/" + MW3_BOT.current_maxplayer;
                label4.Text = MW3_BOT.nbKicks.ToString();

                comboReasons.Enabled = true;
                button1.Enabled = true;
            }
            
        }
        /// <summary>
        /// Check if client have already been added to datagridview
        /// </summary>
        /// <param name="pID">Client ID (Server side)</param>
        public int GetClientROWIndex(int pID)
        {
            foreach (DataGridViewRow row in dataGridView1.Rows)
            {
                if ((int)row.Cells[0].Value == pID) return row.Index;
            }

            return -1;
        }

        private void Main_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (oThread != null && oThread.IsAlive)
            {
                MW3_BOT.thread_stop = true;
                MW3_GUARDER.Stop();

                while (oThread.IsAlive) Thread.Sleep(200);
            }

            PS3.DisconnectTarget();
        }

        private void groupMem_Enter(object sender, EventArgs e)
        {
            
        }

        private void dataGridView1_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {

        }

        /// <summary>
        /// Manual kick from user interface. Do not use unless you are 100% sure!
        /// </summary>
        private void button1_Click(object sender, EventArgs e)
        {
            if (dataGridView1.CurrentCell.RowIndex < 0) return;
            MW3_BOT.__voteKick = (int)dataGridView1.Rows[dataGridView1.CurrentCell.RowIndex].Cells[0].Value;
            MW3_BOT.__voteReason = comboReasons.SelectedIndex;
        }

        private void radioButton1_CheckedChanged(object sender, EventArgs e)
        {

        }

        private void optionsToolStripMenuItem1_Click(object sender, EventArgs e)
        {

        }
        /// <summary>
        /// Handle PS3 RTE connection
        /// </summary>
        private void pS3CCAPIToolStripMenuItem_Click(object sender, EventArgs e)
        {

            if (PS3_CCAPI_CONNECTED)
            {
                PS3.DisconnectTarget();
                connectionToolStripMenuItem.Text = "Connection";
                PS3_CCAPI_CONNECTED = false;
                attachProcessToolStripMenuItem.Enabled = false;
                initializeGuarderToolStripMenuItem.Enabled = false;
                enableForceHostToolStripMenuItem.Enabled = false;
                MessageBox.Show("You are now disconnected from PS3 CCAPI", "MW3Guard", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
            }
            else if (PS3.ConnectTarget())
            {
                
                string Message = "You are now connected with this API : " + PS3.GetCurrentAPIName();
                MessageBox.Show(Message, "MW3Guard", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);

                connectionToolStripMenuItem.Text = "Disconnect";
                PS3_CCAPI_CONNECTED = true;
                attachProcessToolStripMenuItem.Enabled = true;
                initializeGuarderToolStripMenuItem.Enabled = false;

            }
            else
            {
                
                string Message = "Impossible to connect :/";
                MessageBox.Show(Message, "MW3Guard", MessageBoxButtons.OK, MessageBoxIcon.Error);
                PS3_CCAPI_CONNECTED = false;
                connectionToolStripMenuItem.Text = "Connection";
                attachProcessToolStripMenuItem.Enabled = false;
                initializeGuarderToolStripMenuItem.Enabled = false;

            }
        }

        private void configToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (oThread != null && oThread.IsAlive)
            {
                MessageBox.Show("You need to disable MW3Guard before! 'Process->Disable guarder'", "MW3Guard", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            else
            {
                
                Params form2 = new Params(false);
                form2.Show();

            }
        }

        private void attachProcessToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (PS3.AttachProcess())
            {
                /* Enable RemoteProcedureCalls */
                MW3_BOT.RPCEnable_124();
                /* Enable ForceHost (minpartyplayer = 1) */
                //MW3_BOT.ForceHost_124();
                MessageBox.Show("Current game is attached successfully.", "MW3Guard", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);

                attachProcessToolStripMenuItem.Enabled = false;
                initializeGuarderToolStripMenuItem.Enabled = true;
                enableForceHostToolStripMenuItem.Enabled = true;

            }
            else
            {
                MessageBox.Show("No game process found!", "MW3Guard", MessageBoxButtons.OK, MessageBoxIcon.Error);
                initializeGuarderToolStripMenuItem.Enabled = false;
                enableForceHostToolStripMenuItem.Enabled = false;
            }
        }

        private void initializeGuarderToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (MW3_BOT.thread_stop)
            {
                MW3_BOT.thread_stop = false;

                oThread = new Thread(new ThreadStart(MW3_BOT.GuardBot));
                oThread.Start();
                while (!oThread.IsAlive) ;
                MW3_GUARDER.Start();
                initializeGuarderToolStripMenuItem.Text = "Disable guarder";
                PS3.CCAPI.Notify(CCAPI.NotifyIcon.TROPHY1, "MW3Guard enabled!");

                enableForceHostToolStripMenuItem.Enabled = false;

            }
            else
            {
                MW3_BOT.thread_stop = true;
                MW3_GUARDER.Stop();
                loading.Show();

                while (oThread.IsAlive) Thread.Sleep(200);
                loading.Close();
                initializeGuarderToolStripMenuItem.Text = "Enable guarder";
                PS3.CCAPI.Notify(CCAPI.NotifyIcon.TROPHY1, "MW3Guard disabled!");

                enableForceHostToolStripMenuItem.Enabled = true;

            }
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        private void aboutToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            var form2 = new About();
            form2.Show();
        }

        private void resetDatabaseToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (oThread != null && oThread.IsAlive)
            {
                MessageBox.Show("Database locked, please stop Guard instance before!", "MW3Guard", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            else
            {
                handle_db.initialize();
                handle_db.dropTable("params");
                handle_db.dropTable("kicks");
                MessageBox.Show("Database now clean, please restart MW3Guard.", "MW3Guard", MessageBoxButtons.OK, MessageBoxIcon.Information);
                Application.Exit();
            }
        }

        private void enableForceHostToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (MW3_BOT.ForceHost_124())
            {
                MessageBox.Show("You should be able to host party if your firewall allow it", "MW3Guard", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show("Cannot do this ingame!", "MW3Guard", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void statsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var form2 = new Console();
            form2.Show();
        }

        private void menuStrip1_ItemClicked(object sender, ToolStripItemClickedEventArgs e)
        {

        }
    }
}
