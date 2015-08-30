
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MW3Guard_PS3
{
    public partial class Params : Form
    {

        private GuardDB handle_db = new GuardDB();
        private bool first_run = true;

        public Params(bool first_run_arg)
        {
            InitializeComponent();
            handle_db.initialize();
            
            if (!first_run_arg) first_run = false;

            comboBox1.SelectedIndex = handle_db.getParamsInt("camp_rule_id");
            comboBox2.SelectedIndex = handle_db.getParamsInt("spawnkill_rule_id");

            checkBox5.Checked = handle_db.getParamsBool("sv_matchend");
            checkBox2.Checked = handle_db.getParamsBool("quakelike_announce");

            checkBox1.Checked = handle_db.getParamsBool("ratio_re_analysis");
            checkBox3.Checked = handle_db.getParamsBool("hide_firstrun");
            checkBox6.Checked = handle_db.getParamsBool("display_warnings");

        }

        private void initProgram()
        {
            bool disable_sv_matchend = checkBox5.Checked;
            bool enable_quakelike = checkBox2.Checked;
            int rule_protection_spawnkill = comboBox2.SelectedIndex;
            int camp_rule_id = comboBox1.SelectedIndex;
            bool enable_re_analysis = checkBox1.Checked;

            handle_db.setParamsInt("camp_rule_id", camp_rule_id);
            handle_db.setParamsInt("spawnkill_rule_id", rule_protection_spawnkill);

            handle_db.setParamsBool("sv_matchend", disable_sv_matchend);
            handle_db.setParamsBool("quakelike_announce", enable_quakelike);
            handle_db.setParamsBool("ratio_re_analysis", enable_re_analysis);

            handle_db.setParamsBool("hide_firstrun", checkBox3.Checked);
            handle_db.setParamsBool("display_warnings", checkBox6.Checked);
        }

        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {

        }

        private void Params_Load(object sender, EventArgs e)
        {

        }

        private void button1_Click(object sender, EventArgs e)
        {

            if (comboBox1.SelectedIndex == -1 || comboBox2.SelectedIndex == -1)
            {
                MessageBox.Show("You have to select at least option for 'camping rule' and 'spawnkill protection'", "MW3Guard", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            else
            {
                initProgram();

                if (!first_run)
                {
                    this.Close();
                }
                else
                {
                    this.Hide();
                    var form2 = new Main();
                    form2.Closed += (s, args) => this.Close();
                    form2.Show();
                }

            }

        }

        private void comboBox2_SelectedIndexChanged(object sender, EventArgs e)
        {

        }
    }
}
