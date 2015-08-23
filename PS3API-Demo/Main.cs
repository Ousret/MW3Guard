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

namespace PS3API_Demo
{
    public partial class Main : Form
    {
        private PS3API PS3 = new PS3API();
        private Guarder MW3_BOT;

        Thread oThread;
        Wait loading = new Wait();
        System.Windows.Forms.Timer MW3_GUARDER = new System.Windows.Forms.Timer();
        
        public Main()
        {
            InitializeComponent();
        }

        private void Main_Load(object sender, EventArgs e)
        {
            EnableOthersBox(false);
            //Initilize MW3Guard core
            MW3_BOT = new Guarder(PS3);

            MW3_GUARDER.Interval = 500; //Upd8 leaderboard every half sec.
            MW3_GUARDER.Tick += new EventHandler(guarder);
            if (PS3.GetCurrentAPI() == SelectAPI.TargetManager)
                PS3.PS3TMAPI_NET();
            
        }
        /*
        public string getClientPrimaryWeapon(int pID)
        {
            byte[] data = new byte[1];
            data = PS3.GetBytes((uint)(0x0110A4FF + (0x3980 * pID)), 1);
            switch (data[0])
            {
                case 0x68:
                    return "AC130 25MM";
                case 0x69:
                    return "AC130 40MM";
                case 0x6A:
                    return "AC130 105MM";
                default:
                    return "Inconnu";
            }
        }*/
        
        public void guarder(object sender, EventArgs e)
        {
            int i = 0, newRow = 0, searchRow = -1;
            //dataGridView1.Rows.Clear();

            for (i = 0; i < MW3_BOT.maxSlots; i++)
            {
                if ((MW3_BOT.c_board[i] != null) && !String.IsNullOrEmpty(MW3_BOT.c_board[i].client_name))
                {
                    /* If row already exist */

                    searchRow = GetClientROWIndex(i); 

                    if (searchRow != -1)
                    {
                        dataGridView1.Rows[searchRow].Cells[2].Value = MW3_BOT.c_board[i].score;
                        dataGridView1.Rows[searchRow].Cells[3].Value = MW3_BOT.c_board[i].kills;
                        dataGridView1.Rows[searchRow].Cells[4].Value = MW3_BOT.c_board[i].deaths;
                        dataGridView1.Rows[searchRow].Cells[5].Value = MW3_BOT.c_board[i].probaSuccess;
                        if (MW3_BOT.c_board[i].c_team == 1)
                        {
                            dataGridView1.Rows[searchRow].DefaultCellStyle.BackColor = Color.PowderBlue;
                        }
                        else
                        {
                            dataGridView1.Rows[searchRow].DefaultCellStyle.BackColor = Color.White;
                        }
                    }
                    else
                    {
                        newRow = dataGridView1.Rows.Add(i, MW3_BOT.c_board[i].client_name, MW3_BOT.c_board[i].score, MW3_BOT.c_board[i].kills, MW3_BOT.c_board[i].deaths, MW3_BOT.c_board[i].probaSuccess, false);
                        if (MW3_BOT.c_board[i].c_team == 1) dataGridView1.Rows[newRow].DefaultCellStyle.BackColor = Color.PowderBlue;
                    }


                }else
                {
                    searchRow = GetClientROWIndex(i);
                    if (searchRow != -1) dataGridView1.Rows.RemoveAt(searchRow);
                }

                searchRow = -1;
            }

            /* Update label for GameMode and Host and Map */
            label1.Text = "GameMode: "+MW3_BOT.current_gamemode + "-Host-" + MW3_BOT.current_host + " -Maps- " + MW3_BOT.current_maps;
            label2.Text = MW3_BOT.nbClient + "/"+MW3_BOT.current_maxplayer;
        }

        public int GetClientROWIndex(int pID)
        {
            foreach (DataGridViewRow row in dataGridView1.Rows)
            {
                if ((int)row.Cells[0].Value == pID) return row.Index;
            }

            return -1;
        }

        public void EnableOthersBox(bool active)
        {
            groupAttach.Enabled = active;
            groupMem.Enabled = active;
        }

        private void radioTM_CheckedChanged(object sender, EventArgs e)
        {
            EnableOthersBox(false);
            PS3.ChangeAPI(SelectAPI.TargetManager);
        }

        private void radioCC_CheckedChanged(object sender, EventArgs e)
        {
            EnableOthersBox(false);
            PS3.ChangeAPI(SelectAPI.ControlConsole);
        }

        private void btnConnect_Click(object sender, EventArgs e)
        {
            if (PS3.ConnectTarget())
            {
                EnableOthersBox(true);
                string Message = "You are now connected with this API : " + PS3.GetCurrentAPIName();
                MessageBox.Show(Message, "Connected!", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
            }
            else
            {
                EnableOthersBox(false);
                string Message = "Impossible to connect :/";
                MessageBox.Show(Message, "Error...", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnAttach_Click(object sender, EventArgs e)
        {
            if (PS3.AttachProcess())
            {
                /* Enable RemoteProcedureCalls */
                MW3_BOT.RPCEnable_124();
                /* Enable ForceHost (minpartyplayer = 1) */
                //MW3_BOT.ForceHost_124();
                MessageBox.Show("Current game is attached successfully.", "Success.", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
            }
            else
            {
                MessageBox.Show("No game process found!", "Error.", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnSetRand_Click(object sender, EventArgs e)
        {
            if (MW3_BOT.thread_stop)
            {
                MW3_BOT.thread_stop = false;

                //PS3.SetMemory((uint)0x65D14, new byte[] { 0x60, 0x00, 0x00, 0x00 });

                oThread = new Thread(new ThreadStart(MW3_BOT.GuardBot));
                oThread.Start();
                while (!oThread.IsAlive) ;
                MW3_GUARDER.Start();
                btnSetGuarder.Text = "Disable Guarder";
                PS3.CCAPI.Notify(CCAPI.NotifyIcon.TROPHY1, "Take over the cheaters");

            }
            else
            {
                MW3_BOT.thread_stop = true;
                MW3_GUARDER.Stop();
                loading.Show();
                
                while (oThread.IsAlive) Thread.Sleep(200) ;
                loading.Close();
                btnSetGuarder.Text = "Enable Guarder";
                PS3.CCAPI.Notify(CCAPI.NotifyIcon.TROPHY1, "Instance ending!");
            }
        }

        private void Main_FormClosing(object sender, FormClosingEventArgs e)
        {
            PS3.DisconnectTarget();
        }

        private void groupMem_Enter(object sender, EventArgs e)
        {
            
        }

        private void dataGridView1_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (dataGridView1.CurrentCell.RowIndex < 0) return;
            MW3_BOT.__voteKick = (int)dataGridView1.Rows[dataGridView1.CurrentCell.RowIndex].Cells[0].Value;
            MW3_BOT.__voteReason = comboReasons.SelectedIndex;
        }

        private void radioButton1_CheckedChanged(object sender, EventArgs e)
        {

        }
    }
}
