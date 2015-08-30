using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MW3Guard_PS3
{
    public partial class About : Form
    {
        public About()
        {
            InitializeComponent();
        }

        private void label1_Click(object sender, EventArgs e)
        {
            MessageBox.Show("Under MIT Licence", "MW3Guard", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void label2_Click(object sender, EventArgs e)
        {
            MessageBox.Show("Did I forget to mention that they take us for idiots?", "MW3Guard", MessageBoxButtons.OK, MessageBoxIcon.Information);
            MessageBox.Show("They doesn't want to update their game in order to force us to buy their newer ones..", "MW3Guard", MessageBoxButtons.OK, MessageBoxIcon.Information);
            MessageBox.Show("We ain't fooled twice..", "MW3Guard", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}
