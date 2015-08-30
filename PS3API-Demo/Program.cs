using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Data.SQLite;

namespace MW3Guard_PS3
{
    static class Program
    {
        static private GuardDB handle_db = new GuardDB();
        /// <summary>
        /// Point d'entrée principal de l'application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            handle_db.initialize();
            if (handle_db.getParamsBool("hide_firstrun"))
            {
                
                Application.Run(new Main());
            }
            else
            {
                
                Application.Run(new Params(true));
            }
            
        }
    }
}
