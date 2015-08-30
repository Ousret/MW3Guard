using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.SQLite;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MW3Guard_PS3
{
    public partial class Console : Form
    {
        private const string _DB_STRING_CONNECTION = "Data Source=MW3Guard.db;Version=3;";

        public Console()
        {
            InitializeComponent();
        }

        private void Console_Load(object sender, EventArgs e)
        {
            comboBox1.SelectedIndex = 2;
            Console_Fill(100);
        }

        private void Console_Fill(int nbFetch)
        {
            //Clean datagridview
            dataGridView1.Rows.Clear();
            // Open connection to database
            SQLiteConnection sqliteCon = new SQLiteConnection(_DB_STRING_CONNECTION);
            sqliteCon.Open();

            // Search the table for user Tommy
            string selectSQL = "SELECT kid, kdate, psnid, creason FROM kicks" +
                                      " ORDER BY kid DESC LIMIT "+nbFetch;
            SQLiteCommand selectCommand = new SQLiteCommand(selectSQL
                                                               , sqliteCon);
            SQLiteDataReader dataReader = selectCommand.ExecuteReader();

            // Use a variable to store the result of the search
            while (dataReader.Read())
            {
                dataGridView1.Rows.Add(dataReader.GetInt32(dataReader.GetOrdinal("kid")), dataReader.GetString(dataReader.GetOrdinal("kdate")), dataReader.GetString(dataReader.GetOrdinal("psnid")), dataReader.GetString(dataReader.GetOrdinal("creason")));
            }

            dataReader.Close();
            sqliteCon.Close();

        }

        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            switch (comboBox1.SelectedIndex)
            {
                case 0:
                    Console_Fill(10);
                    break;
                case 1:
                    Console_Fill(50);
                    break;
                case 2:
                    Console_Fill(100);
                    break;
                case 3:
                    Console_Fill(200);
                    break;
                case 4:
                    Console_Fill(500);
                    break;
                case 5:
                    Console_Fill(1000);
                    break;
                default:
                    Console_Fill(100);
                    break;
            }
        }
    }
}
