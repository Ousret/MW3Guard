using System;
using System.Collections.Generic;
using System.IO;
using System.Security.AccessControl;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Security.Principal;
using System.Data.SQLite;

namespace MW3Guard_PS3
{
    class GuardDB
    {
        private const string _DB_STRING_CONNECTION = "Data Source=MW3Guard.db;Version=3;";

        /// <summary>
        /// Check if file and table exist
        /// </summary>
        public void initialize()
        {
            if (!File.Exists(@"MW3Guard.db"))
            {
                SQLiteConnection.CreateFile(@"MW3Guard.db");
                createTableParams();
                createTableKicks();
            }
        }

        /// <summary>
        /// Create table if not exist (Params)
        /// </summary>
        private void createTableParams()
        {

            string sqlStatement = "CREATE TABLE IF NOT EXISTS params (cname TEXT, state int)";
            SQLiteConnection sqliteCon = new SQLiteConnection(_DB_STRING_CONNECTION);
            sqliteCon.Open();
            using (SQLiteTransaction sqlTransaction = sqliteCon.BeginTransaction())
            {
                SQLiteCommand command = new SQLiteCommand(sqlStatement, sqliteCon);
                command.ExecuteNonQuery();
                sqlTransaction.Commit();
            }

        }

        /// <summary>
        /// Create table if not exist (Kicks list)
        /// </summary>
        private void createTableKicks()
        {
            // Performs an insert, change contents of sqlStatement to perform
            // update or delete.
            string sqlStatement = "CREATE TABLE IF NOT EXISTS kicks (kid INTEGER PRIMARY KEY, kdate TEXT, psnid TEXT, reason int, creason TEXT)";
            SQLiteConnection sqliteCon = new SQLiteConnection(_DB_STRING_CONNECTION);
            sqliteCon.Open();
            using (SQLiteTransaction sqlTransaction = sqliteCon.BeginTransaction())
            {
                SQLiteCommand command = new SQLiteCommand(sqlStatement, sqliteCon);
                command.ExecuteNonQuery();
                sqlTransaction.Commit();
            }
        }

        /// <summary>
        /// Retrieve param value, -1 if input not exist
        /// </summary>
        /// <param name="input">Key to retrieve</param>
        public int getParamsInt(string input)
        {
            int res = 0;
            // Open connection to database
            SQLiteConnection sqliteCon = new SQLiteConnection(_DB_STRING_CONNECTION);
            sqliteCon.Open();

            // Search the table for user Tommy
            string selectSQL = "SELECT state FROM params" +
                                      " WHERE cname = '" + input + "'";
            SQLiteCommand selectCommand = new SQLiteCommand(selectSQL
                                                               , sqliteCon);
            SQLiteDataReader dataReader = selectCommand.ExecuteReader();

            // Use a variable to store the result of the search
            bool paramExists = dataReader.Read();
            if (!paramExists) return -1;

            res = dataReader.GetInt32(dataReader.GetOrdinal("state"));
            dataReader.Close();
            sqliteCon.Close();

            return res;
        }

        /// <summary>
        /// Retrieve param value, false if input not exist
        /// </summary>
        /// <param name="input">Key to retrieve</param>
        public bool getParamsBool(string input)
        {
            int res = 0;
            // Open connection to database
            SQLiteConnection sqliteCon = new SQLiteConnection(_DB_STRING_CONNECTION);
            sqliteCon.Open();

            // Search the table for user Tommy
            string selectSQL = "SELECT state FROM params" +
                                      " WHERE cname = '" + input + "'";
            SQLiteCommand selectCommand = new SQLiteCommand(selectSQL
                                                               , sqliteCon);
            SQLiteDataReader dataReader = selectCommand.ExecuteReader();

            // Use a variable to store the result of the search
            bool paramExists = dataReader.Read();
            if (!paramExists) return false;

            res = dataReader.GetInt32(dataReader.GetOrdinal("state"));
            dataReader.Close();
            sqliteCon.Close();

            return res > 0 ? true : false;
        }

        /// <summary>
        /// Set param value, create or upd8 if already exist.
        /// </summary>
        /// <param name="input">Key to set</param>
        /// <param name="state">Value to set</param>
        public void setParamsInt(string input, int state)
        {
            // Open connection to database
            SQLiteConnection sqliteCon = new SQLiteConnection(_DB_STRING_CONNECTION);
            sqliteCon.Open();

            // Search the table for user Tommy
            string selectSQL = "SELECT cname FROM params" +
                                      " WHERE cname = '"+input+"'";
            SQLiteCommand selectCommand = new SQLiteCommand(selectSQL
                                                               , sqliteCon);
            SQLiteDataReader dataReader = selectCommand.ExecuteReader();

            // Use a variable to store the result of the search
            bool paramExists = dataReader.Read();
            dataReader.Close();

            // If Tommy is in the table
            if (paramExists)
            {
                // Update his username
                using (SQLiteTransaction sqlTransaction = sqliteCon.BeginTransaction())
                {
                    // Update the expiry date of the application
                    string updateSQL = "UPDATE params SET state = "+state+"" +
                                               " WHERE cname = '"+input+"'";
                    SQLiteCommand updateCommand = new SQLiteCommand(updateSQL
                                                                        , sqliteCon);
                    updateCommand.ExecuteNonQuery();
                    sqlTransaction.Commit();
                }
            }
            else
            {
                // Insert Tommy as a new user
                using (SQLiteTransaction sqlTransaction = sqliteCon.BeginTransaction())
                {
                    string insertSQL = "INSERT INTO params(cname, state)" +
                                             " VALUES ('"+input+"', "+state+")";
                    SQLiteCommand insertCommand = new SQLiteCommand(insertSQL, sqliteCon);
                    insertCommand.ExecuteNonQuery();
                    sqlTransaction.Commit();
                }
            }

            sqliteCon.Close();
        }

        /// <summary>
        /// Set param value, create or upd8 if already exist.
        /// </summary>
        /// <param name="input">Key to set</param>
        /// <param name="state">Value to set</param>
        public void setParamsBool(string input, bool state)
        {
            int intstate = 0;
            
            if (state)
            {
                intstate = 1;
            }
            else
            {
                intstate = 0;
            }

            // Open connection to database
            SQLiteConnection sqliteCon = new SQLiteConnection(_DB_STRING_CONNECTION);
            sqliteCon.Open();

            // Search the table for user Tommy
            string selectSQL = "SELECT cname FROM params" +
                                      " WHERE cname = '" + input + "'";
            SQLiteCommand selectCommand = new SQLiteCommand(selectSQL
                                                               , sqliteCon);
            SQLiteDataReader dataReader = selectCommand.ExecuteReader();

            // Use a variable to store the result of the search
            bool paramExists = dataReader.Read();
            dataReader.Close();

            // If Tommy is in the table
            if (paramExists)
            {
                // Update his username
                using (SQLiteTransaction sqlTransaction = sqliteCon.BeginTransaction())
                {
                    // Update the expiry date of the application
                    string updateSQL = "UPDATE params SET state = " + intstate + "" +
                                               " WHERE cname = '" + input + "'";
                    SQLiteCommand updateCommand = new SQLiteCommand(updateSQL
                                                                        , sqliteCon);
                    updateCommand.ExecuteNonQuery();
                    sqlTransaction.Commit();
                }
            }
            else
            {
                // Insert Tommy as a new user
                using (SQLiteTransaction sqlTransaction = sqliteCon.BeginTransaction())
                {
                    string insertSQL = "INSERT INTO params(cname, state)" +
                                             " VALUES ('" + input + "', " + intstate + ")";
                    SQLiteCommand insertCommand = new SQLiteCommand(insertSQL, sqliteCon);
                    insertCommand.ExecuteNonQuery();
                    sqlTransaction.Commit();
                }
            }

            sqliteCon.Close();

        }

        /// <summary>
        /// Record new kick into database
        /// </summary>
        /// <param name="psnid">PlayStation ID</param>
        /// <param name="reason">Reason ID</param>
        /// <param name="sreason">Short description</param>
        public void setKickReason(string psnid, int reason, string sreason)
        {
            
            // Performs an insert, change contents of sqlStatement to perform
            // update or delete.
            string sqlStatement = "insert into kicks (kdate, psnid, reason, creason) values ('" + DateTime.Now.ToString("MM-dd-yyyy-h-mm") + "', '" + psnid + "', " + reason + ", '" + sreason + "')";
            SQLiteConnection sqliteCon = new SQLiteConnection(_DB_STRING_CONNECTION);
            sqliteCon.Open();
            using (SQLiteTransaction sqlTransaction = sqliteCon.BeginTransaction())
            {
                SQLiteCommand command = new SQLiteCommand(sqlStatement, sqliteCon);
                command.ExecuteNonQuery();
                sqlTransaction.Commit();
            }

        }

        /// <summary>
        /// Drop all data inside specific table
        /// </summary>
        /// <param name="target">Table to clean up</param>
        public void dropTable(string target)
        {
            // Performs an insert, change contents of sqlStatement to perform
            // update or delete.
            string sqlStatement = "DELETE FROM "+target;
            SQLiteConnection sqliteCon = new SQLiteConnection(_DB_STRING_CONNECTION);
            sqliteCon.Open();
            using (SQLiteTransaction sqlTransaction = sqliteCon.BeginTransaction())
            {
                SQLiteCommand command = new SQLiteCommand(sqlStatement, sqliteCon);
                command.ExecuteNonQuery();
                sqlTransaction.Commit();
            }
        }

    }
}
