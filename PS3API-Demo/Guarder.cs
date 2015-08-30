using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Diagnostics;
using PS3Lib;
using System.Media;
using System.Text.RegularExpressions;
using System.IO;

namespace MW3Guard_PS3
{
    class Guarder
    {
        private const string __version__ = "v1.4.1";

        private string _primary_name_ = "";

        private PS3API PS3_REMOTE;
        private RPC MW3_REMOTE;

        private bool enable_spawnkill_protection = false;
        //private bool enable_autobalance = false;
        private bool disable_sv_matchend = false;
        private bool enable_quakelike_announce = false;
        private bool enable_uav_redbox_analysis = false;
        private bool display_warnings = false;
        private bool camping_analysis = false;

        private int camping_rule_choise = -1, spawnkill_rule_choise = -1;

        private bool disable_sm_me = false;

        private const uint __PLAGE0__ = 0x00FCA3E8, __BLOCK0__ = 0x280;
        private const uint __PLAGE1__ = 0x0110A293, __BLOCK1__ = 0x3980;

        private GuardDB handle_db = new GuardDB();

        //private const uint __IPs__ = 0x01BBFE3C; //Useless!

        /* Def of a client slot data */
        public class client_data
        {

            public byte[] buffer0 = new byte[__BLOCK0__], buffer1 = new byte[__BLOCK1__];

            public string client_name = "";

            public int n_prestige = 0;
            public int n_level = 1;

            public int score = 0;
            public int kills = 0;
            public int deaths = 0;

            public int c_health = 0;

            public int c_primmary_ammo = 0;
            public int c_secondary_ammo = 0;

            public int c_team = 0;
            public int warn_nb = 0;

            public int report = 0;
            /* Coordonnées client et centre anti-campeur */
            public float xp, yp, zp, x0, y0, z0;
            public int nbsec_camp;
            public int nb_countcamp = 0;

            /* Mesure du barycentre ennemie */
            public float barycentre_x = 0, barycentre_y = 0, barycentre_z = 0;
            public double last_distance = 0;
            /* Stats pour rapprochement et eloignement successif (Ratio R/E) */
            public uint rapprochements = 0, eloignements = 0;
            public float probaSuccess;
            /* RatioRE nbTimes */
            public int nb_alert_ratio_re = 0;
            /* CustomKillstreak! (mod. QuakeLight) */
            public int lastKillStreak = 0, currentKillStreak = 0;

            /* Vote to kick (web to ps3) */
            public string client_ip = ""; //0x01BBFE3C
            public int nbVoteKick = 0;

            /* Test for Wallhack (only once to avoid useless charge) */
            public int wallhack_pr = -1;

            /* NbIter per client */
            public uint cl_inter = 0;

            /* Origin client, save x10 (for spawnkill protection) */
            public float[] s_originX = new float[_MAX_SAVE_ORIGIN], s_originY = new float[_MAX_SAVE_ORIGIN], s_originZ = new float[_MAX_SAVE_ORIGIN];
            public uint c_save = 0; //Max x100

            public bool spawnkill_analyse = false; //If client just died..
        }

        public volatile client_data[] c_board = new client_data[18];
        public volatile bool thread_stop = true;

        public volatile string current_host;
        public volatile string current_maps;
        public volatile string current_gamemode;
        public volatile string current_maxplayer = "18";

        public volatile int maxSlots = 0;

        public volatile int nbKicks = 0;

        public volatile int nbClient = 0;
        private bool _botEnable = false;

        public volatile int __voteKick = -1;
        public volatile int __voteReason = -1;

        public int _allow_nbcamp = 0;

        private float rayon = 750.0F;
        private float _spawnkill_dist = 1200.0F;

        private const uint _MAX_TEAM_DIFF = 2;
        private const uint _MAX_SAVE_ORIGIN = 100;

        private int[] team_score_bak = new int[2] { 0, 0 };

        private byte[] headers = new byte[0x100];

        public class Offsets {

            public const uint Wallhack = 0x00173B62;

            public class Block0
            {
                public const uint Model = 0;
                public const uint Health = 55;
            }
            public class Block1
            {
                public const uint Redbox = 0;
                public const uint OriginX = 9, OriginY = 13, OriginZ = 17;
                public const uint Tactical = 624;
                public const uint Vision = 868;
                public const uint PrimaryAmmo = 1048;
                public const uint ExplosiveBullet = 1248;
                public const uint Score = 13061, Kills = 13069, Deaths = 13065;
                public const uint Team = 13252;
                public const uint Name = 13313;
                public const uint UAV = 13480; //Doesnt work
                public const uint Level = 13352, Prestige = 13356;
                public const uint Clip = 13804;
            }
        }

        System.IO.StreamWriter _debug;
        Stopwatch _bench = new Stopwatch();

        /* Sound IO: CustomAnnounce */
        private SoundPlayer announceSound;

        public Guarder(PS3API INPUT)
        {
            PS3_REMOTE = INPUT;
            MW3_REMOTE = new RPC(PS3_REMOTE);
        }

        /// <summary>
        /// Enable RPC call through FPS func.
        /// </summary>
        public void RPCEnable_124()
        {
            MW3_REMOTE.Enable();
        }

        /// <summary>
        /// Change some dvar in order to force party hosting before ingame!
        /// </summary>
        public bool ForceHost_124()
        {
            /*
            party_minplayers 1 [OK]
            lobby_partySearchWaitTime 0
            party_gameStartTimerLength 1 [OK]
            party_pregameStartTimerLength 12
            party_vetoDelayTime 1
            party_maxTeamDiff 16 [OK]
            party_minLobbyTime 0 [OK]
            pt_backoutOnClientPresence 1
            partymigrate_timeout 1
            pt_pregameStartTimerLength 1
            pt_gameStartTimerLength 20 
            */
            if (MW3_REMOTE.cl_ingame()) return false;

            MW3_REMOTE.lockIntDvarToValue(0x8AEE34, 0x1); //party_minplayers
            MW3_REMOTE.lockIntDvarToValue(0x8AEDC0, 0x1); //pt_gameStartTimerLength
            MW3_REMOTE.lockIntDvarToValue(0x8AEDC8, 0x1); //pt_pregameStartTimerLength
            MW3_REMOTE.lockIntDvarToValue(0x8AEED8, 0x1); //partymigrate_timeout
            MW3_REMOTE.lockIntDvarToValue(0x8AEE88, 0xF); //party_maxTeamDiff

            PS3_REMOTE.Extension.WriteUInt32(0x428a40, 0x4081001c);
            PS3_REMOTE.Extension.WriteUInt32(0x428a44, 0x48000018);
            PS3_REMOTE.Extension.WriteUInt32(0x428a4c, 0x40810010);
            PS3_REMOTE.Extension.WriteUInt32(0x428a54, 0x40810008);
            PS3_REMOTE.Extension.WriteUInt32(0x428a58, 0x48000005);

            return true;
        }

        /// <summary>
        /// Check if PSN ID is valid according to current policy
        /// </summary>
        /// <param name="strToCheck">PSN ID </param>
        private bool isValidName(string strToCheck)
        {
            Regex rg;

            if (strToCheck.Substring(strToCheck.Length - 3, 3) == "(1)")
            {
                rg = new Regex(@"^[a-zA-Z0-9\-_()s,]{6,19}$");
            }
            else
            {
                rg = new Regex(@"^[a-zA-Z0-9\-_s,]{3,16}$");
            }
            
            return rg.IsMatch(strToCheck);
        }

        /// <summary>
        /// Save client pos. x,y,z in order to reuse them with spawnkill protection
        /// </summary>
        /// <param name="pID">Client ID (Server side)</param>
        private bool saveClientOrigin(int pID)
        {
            if (c_board[pID] == null || c_board[pID].c_save >= _MAX_SAVE_ORIGIN) return false;
            if (c_board[pID].xp == 0 || c_board[pID].yp == 0 || c_board[pID].zp == 0) return false;
            c_board[pID].s_originX[c_board[pID].c_save] = c_board[pID].xp;
            c_board[pID].s_originY[c_board[pID].c_save] = c_board[pID].yp;
            c_board[pID].s_originZ[c_board[pID].c_save] = c_board[pID].zp;
            c_board[pID].c_save++;
            return true;
        }

        /// <summary>
        /// Give distance between pID and his nearest ennemie
        /// </summary>
        /// <param name="pID">Client ID (Server side)</param>
        private double nearestEnnemie(int pID)
        {
            if (c_board[pID] == null) return -1;
            int i = 0;
            double minDist = 999999, tDist = 0;
            int opposite = oppositeTeam(pID);
            if (opposite == -1) return -1;

            for (i = 0; i < maxSlots; i++)
            {
                if (pID != i && c_board[i] != null && !String.IsNullOrEmpty(c_board[i].client_name) && c_board[i].c_team == opposite)
                {
                    tDist = distancePoints(c_board[i].xp, c_board[i].yp, c_board[i].zp, c_board[pID].xp, c_board[pID].yp, c_board[pID].zp);
                    if (tDist < minDist) minDist = tDist;
                }
            }

            return minDist;
        }

        /// <summary>
        /// Return opposite team of one client
        /// </summary>
        /// <param name="pID">Client ID (Server side)</param>
        private int oppositeTeam(int pID)
        {
            if (c_board[pID] == null) return -1;
            if (c_board[pID].c_team > 1) return -1;

            return (c_board[pID].c_team) == 0 ? 1 : 0; 
        }

        /// <summary>
        /// Test if client risk to be spawnkilled (test distance)
        /// </summary>
        /// <param name="pID">Client ID (Server side)</param>
        private bool clientRiskSpawnkill(int pID)
        {
            if (nearestEnnemie(pID) <= _spawnkill_dist) return true;
            return false;
        }

        /// <summary>
        /// Test if client is elligible for spawnkill protection.
        /// </summary>
        /// <param name="pID">Client ID (Server side)</param>
        private bool clientSpawnkillProtectionActive(int pID)
        {
            byte TacticalInsert = 0x4A;
            string c_map = ReturnInfos(6);
            if (c_map == "mp_seatown" || c_map == "mp_plaza2" || c_map == "mp_exchange" || c_map == "mp_bootleg" || c_map == "mp_alpha" || c_map == "mp_village" || c_map == "mp_bravo" || c_map == "mp_courtyard_ss" || c_map == "mp_aground_ss") TacticalInsert--;
            if (c_board[pID] == null || c_board[pID].buffer1[Offsets.Block1.Tactical] == TacticalInsert || c_board[pID].c_save == 0) return false;
            return true;
        }

        /* TESTS ONLY: Prevent mw3 remote ending "by host", we may went to disable sv_matchend ingame */
        /// <summary>
        /// Experimental: Disable sv_matchend whenever you wanted to.
        /// </summary>
        /// <param name="enable">If true, it'll disable sv_matchend, false will restore.</param>
        private void swap_sv_me_m(bool enable)
        {
            byte[] swap = new byte[52]
            {
                0x7C, 0x08, 0x03, 0xA6, 0xEB, 0x61, 0x00, 0x78, 0xEB, 0x81, 0x00, 0x80, 0xEB, 0xA1, 0x00, 
                0x88, 0xEB, 0xC1, 0x00, 0x90, 0xEB, 0xE1, 0x00, 0x98, 0x38, 0x21, 0x00, 0xA0, 0x4E, 0x80, 
                0x00, 0x20, 0x80, 0x63, 0x00, 0x00, 0x80, 0x84, 0x00, 0x00, 0x7C, 0x64, 0x18, 0x10, 0x7C, 
                0x63, 0x07, 0xB4, 0x4E, 0x80, 0x00, 0x20
            };

            byte[] origin = new byte[59]
            {
                0x7C,0x08,0x02,0xA6,0xF8,0x01, 0x00,0xB0, 0xFB, 0xE1, 0x00, 0x98, 0xFB, 0xC1, 0x00, 0x90,
                0xFB, 0xA1, 0x00, 0x88, 0xFB, 0x81,
                0x00, 0x80, 0xFB, 0x61, 0x00, 0x78, 0x4B, 0xEB, 0xCF, 0x05, 0x2C, 0x03, 0x00, 0x00, 0x41,
                0x82, 0x01, 0x0C, 0x4B, 0xEB, 0xCF, 0x65, 0x2C, 0x03, 0x00, 0x00, 0x41, 0x82, 0x01, 0x00,
                0x3C, 0x60, 0x01, 0x7C, 0x3B, 0xA0, 0x00
            };

            if (enable)
            {
                PS3_REMOTE.SetMemory(0x22f7a8 + 4, swap);
            }
            else
            {
                PS3_REMOTE.SetMemory(0x22f7a8 + 4, origin);
            }

            /*byte[] dmp = PS3_REMOTE.GetBytes(0x22f7a8, 1024); //sv_matchend
            _debug.WriteLine(BitConverter.ToString(dmp));

            //0x0019031C - CalculateRanks(void)
            dmp = PS3_REMOTE.GetBytes(0x19031c, 1024); //CalculateRanks(void)
            _debug.WriteLine(BitConverter.ToString(dmp));*/
        }
        /// <summary>
        /// Retrive how many client in specific team
        /// </summary>
        /// <param name="team">0; Team A -- 1; Team B</param>
        private int NbClientTeam(int team)
        {
            int i = 0, nbclientonteam = 0;
            if (team != 1 && team != 0) return 0;

            for (i = 0; i < maxSlots; i++)
            {
                if (c_board[i] != null && !String.IsNullOrEmpty(c_board[i].client_name) && c_board[i].c_team == team) nbclientonteam++;
            }

            return nbclientonteam;
        }

        /* Most secure origin, -1 if current is the actual best.. */
        /// <summary>
        /// Used for spawnkill protection, in order to determine most secure origin from saved one.
        /// </summary>
        /// <param name="pID">Client ID (Server side)</param>
        private int mostSecureOrigin(int pID)
        {
            if (c_board[pID] == null) return -1;
            int i = 0, j = 0, mostSec = -1;
            double minDist = nearestEnnemie(pID), tDist = 0;
            //if (minDist <= _spawnkill_dist) return -1;
            int tOpposite = oppositeTeam(pID);

            for (i = 0; i < c_board[pID].c_save; i++)
            {
                for (j = 0; j < maxSlots; j++)
                {
                    if (pID != j && c_board[j] != null && !String.IsNullOrEmpty(c_board[j].client_name) && c_board[j].c_team == tOpposite && GetClientHealth(j) > 0)
                    {
                        tDist = distancePoints(c_board[j].xp, c_board[j].yp, c_board[j].zp, c_board[pID].s_originX[i], c_board[pID].s_originY[i], c_board[pID].s_originZ[i]);
                        
                        if (tDist > minDist)
                        {
                            minDist = tDist;
                            mostSec = i;
                        }
                    }
                }
            }

            return mostSec;
        }
        /// <summary>
        /// Teleport client to one of his saved origin
        /// </summary>
        /// <param name="pID">Client ID (Server side)</param>
        /// <param name="saveLoc">Saved origin ID</param>
        private bool ClientTeleportSaveOrigin(int pID, int saveLoc)
        {
            if (c_board[pID] == null || saveLoc == -1 || saveLoc > _MAX_SAVE_ORIGIN-1) return false;
            PS3_REMOTE.Extension.WriteFloat((uint)(0x0110A29C + (0x3980 * pID)), c_board[pID].s_originX[saveLoc]);
            PS3_REMOTE.Extension.WriteFloat((uint)(0x0110A2A0 + (0x3980 * pID)), c_board[pID].s_originY[saveLoc]);
            PS3_REMOTE.Extension.WriteFloat((uint)(0x0110A2A4 + (0x3980 * pID)), c_board[pID].s_originZ[saveLoc]);

            if (display_warnings) MW3_REMOTE.iPrintln(pID, "^8Server: ^7Protection against ^3spawnkill enabled!");

            return true;
        }
        /// <summary>
        /// Set or reset client team appartenance. (Don't use that unless you know what you'r doing!)
        /// </summary>
        /// <param name="pID">Client ID (Server side)</param>
        /// <param name="n_team">New team (0; Team A, 1; Team B)</param>
        private bool setClientTeam(int pID, int n_team)
        {
            if (n_team != 0 && n_team != 1) return false;
            if (c_board[pID] == null) return false;
            byte conv_team = 0x0;
            int new_comrad = 0, nb_client_nteam = NbClientTeam(n_team), i = 0, j = 0;
            Random rnd = new Random();

            /* Don't move anyone if the target team if already full */
            if (nb_client_nteam >= (maxSlots / 2)) return false;

            if (n_team == 0)
            {
                conv_team = 0x1;
            }else if(n_team == 1)
            {
                conv_team = 0x2;
            }
            
            /* Teleport player to new comrad if there any client there */
            if (nb_client_nteam >= 1)
            {
                new_comrad = rnd.Next(1, nb_client_nteam+1);

                /* Find pID of the new comrad */
                for (i = 0; i < maxSlots; i++)
                {
                    if (c_board[i] != null && !String.IsNullOrEmpty(c_board[i].client_name) && c_board[i].c_team == n_team) j++;
                    if (j == new_comrad) break;
                }

                new_comrad = i; //Swap with pID of the new comrad

                /* Teleport to new comrad (rand) */
                PS3_REMOTE.Extension.WriteFloat((uint)(0x0110A29C + (0x3980 * pID)), c_board[new_comrad].xp + 0.0005F);
                PS3_REMOTE.Extension.WriteFloat((uint)(0x0110A2A0 + (0x3980 * pID)), c_board[new_comrad].yp - 0.0005F);
                PS3_REMOTE.Extension.WriteFloat((uint)(0x0110A2A4 + (0x3980 * pID)), c_board[new_comrad].zp);

            }

            /* Change team */
            PS3_REMOTE.SetMemory((uint)(0x0110d657 + (0x3980 * pID)), new byte[] { conv_team });
            c_board[pID].c_team = n_team;
            /* Warn client of being moved to opposite team */
            MW3_REMOTE.iPrintln(pID, "^3Auto-balancing: You have been ^5moved in the ^7opposite team!");

            return true;
        }
        /// <summary>
        /// Check if client is playing with splitscreen option
        /// </summary>
        /// <param name="pID">Client ID (Server side)</param>
        private bool isPlayingSplitScreen(int pID)
        {
            if (c_board[pID] == null) return false;
            int i = 0;
            string nclient = c_board[pID].client_name.Substring(c_board[pID].client_name.Length-3, 3);
            if (nclient == "(1)") return true;

            for (i = 0; i < maxSlots; i++)
            {
                if (pID != i && c_board[i] != null && !String.IsNullOrEmpty(c_board[i].client_name) && c_board[i].c_team == c_board[pID].c_team)
                {
                    if (c_board[pID].client_name == c_board[i].client_name.Substring(0, c_board[i].client_name.Length - 3)) return true;
                }
            }

            return false;
        }
        /// <summary>
        /// Rebalance teams if needed. (Do not use that unless you know what you'r doing..)
        /// </summary>
        private bool AutoBalancing()
        {
            int count_team0 = NbClientTeam(0), count_team1 = NbClientTeam(1);
            if (count_team0 == count_team1) return false;
            long difference =  Math.Abs(count_team0 - count_team1);
            int team_target = 0;
            int client_target = 0, i = 0, j = 0;
            Random rnd = new Random();

            if (difference >= _MAX_TEAM_DIFF)
            {
                PS3_REMOTE.CCAPI.Notify(CCAPI.NotifyIcon.CAUTION, "Auto-balancing running..");
                MW3_REMOTE.iPrintln(-1, "^3Auto-balancing: Due to ^5stupid(s) ^7ragequit.");

                /* Define what team we need to fill in */
                if (count_team0 > count_team1)
                {
                    team_target = 1;
                }
                else
                {
                    team_target = 0;
                }

                /* Move random client to opposite team */
                //Need to be completed..
                for (i = 0; i < difference; i++)
                {
                    if (team_target == 1)
                    {
                        client_target = rnd.Next(1, count_team0 + 1);

                        for (j = 0; j < maxSlots; j++)
                        {
                            if (c_board[i] != null && !String.IsNullOrEmpty(c_board[i].client_name) && c_board[i].c_team == 0) j++;
                            if (j == client_target) break;
                        }

                        client_target = j;

                        if (!isPlayingSplitScreen(client_target))
                        {
                            setClientTeam(client_target, team_target);
                        }

                    }
                    else
                    {
                        client_target = rnd.Next(1, count_team1 + 1);

                        for (j = 0; j < maxSlots; j++)
                        {
                            if (c_board[i] != null && !String.IsNullOrEmpty(c_board[i].client_name) && c_board[i].c_team == 1) j++;
                            if (j == client_target) break;
                        }

                        client_target = j;
                        if (!isPlayingSplitScreen(client_target))
                        {
                            setClientTeam(client_target, team_target);
                        }

                    }

                }

                return true;
            }

            return false;

        }
        /// <summary>
        /// Retrieve score of hole team
        /// </summary>
        /// <param name="team">Team ID</param>
        private int getTeamScore(int team)
        {
            int i = 0, score = 0;

            for (i = 0; i < maxSlots; i++)
            {
                if (c_board[i] != null && c_board[i].c_team == team) score += c_board[i].score;
            }

            return score+team_score_bak[team];
        }

        /// <summary>
        /// Should be started as a single thread, it's MW3Guard Core.
        /// </summary>
        public void GuardBot()
        {
            int i = 0, j = 0, nbClient_T = 0, deathstmp = 0, killstmp = 0;
            bool redboxEnabled = false;
            uint nbPoints = 0;
            double tmpDistance = 0;
            string client_name_t = "";

            _debug = new System.IO.StreamWriter("logs/MW3Guard-" + DateTime.Now.ToString("MM-dd-yyyy-h-mm") + ".log");
            _debug.WriteLine("["+ DateTime.Now.ToString("MM-dd-yyyy-h-mm") + "] MW3Guard; Thread started..");

            _primary_name_ = GetPrimaryName();

            handle_db.initialize();

            /* Read lastest params set by user */
            disable_sv_matchend = handle_db.getParamsBool("sv_matchend");
            enable_quakelike_announce = handle_db.getParamsBool("quakelike_announce");
            display_warnings = handle_db.getParamsBool("display_warnings");

            enable_uav_redbox_analysis = handle_db.getParamsBool("ratio_re_analysis");

            camping_rule_choise = handle_db.getParamsInt("camp_rule_id");
            spawnkill_rule_choise = handle_db.getParamsInt("spawnkill_rule_id");

            if (camping_rule_choise == -1 || camping_rule_choise == 0)
            {
                camping_analysis = false;
                _debug.WriteLine("[" + DateTime.Now.ToString("MM-dd-yyyy-h-mm") + "] Camping rule disabled..");
            }
            else if(camping_rule_choise == 1)
            {
                camping_analysis = true;
                rayon = 600.0F;
                _allow_nbcamp = 3;
                _debug.WriteLine("[" + DateTime.Now.ToString("MM-dd-yyyy-h-mm") + "] Camping rule enable, rule id = 1");
            }
            else if(camping_rule_choise == 2)
            {
                camping_analysis = true;
                rayon = 700.0F;
                _allow_nbcamp = 2;
                _debug.WriteLine("[" + DateTime.Now.ToString("MM-dd-yyyy-h-mm") + "] Camping rule enable, rule id = 2");
            }
            else if (camping_rule_choise == 3)
            {
                camping_analysis = true;
                rayon = 800.0F;
                _allow_nbcamp = 1;
                _debug.WriteLine("[" + DateTime.Now.ToString("MM-dd-yyyy-h-mm") + "] Camping rule enable, rule id = 3");
            }

            if (spawnkill_rule_choise == -1 || spawnkill_rule_choise == 0)
            {
                enable_spawnkill_protection = false;
                _debug.WriteLine("[" + DateTime.Now.ToString("MM-dd-yyyy-h-mm") + "] Spawnkill protection is disabled.");
            }
            else if(spawnkill_rule_choise == 1)
            {
                enable_spawnkill_protection = true;
                _spawnkill_dist = 700.0F;
                _debug.WriteLine("[" + DateTime.Now.ToString("MM-dd-yyyy-h-mm") + "] Spawnkill protection is enabled, rule id = 1");
            }
            else if(spawnkill_rule_choise == 2)
            {
                enable_spawnkill_protection = true;
                _spawnkill_dist = 850.0F;
                _debug.WriteLine("[" + DateTime.Now.ToString("MM-dd-yyyy-h-mm") + "] Spawnkill protection is enabled, rule id = 2");
            }
            else if(spawnkill_rule_choise == 3)
            {
                enable_spawnkill_protection = true;
                _spawnkill_dist = 1200.0F;
                _debug.WriteLine("[" + DateTime.Now.ToString("MM-dd-yyyy-h-mm") + "] Spawnkill protection is enabled, rule id = 3");
            }

            /* Corrupt sv_matchend: face the buffer overflow with manual corruption */
            if (disable_sv_matchend)
            {
                swap_sv_me_m(true);
                _debug.WriteLine("[" + DateTime.Now.ToString("MM-dd-yyyy-h-mm") + "] Shut sv_matchend during ingame session enabled..");
            }

            _debug.WriteLine("[" + DateTime.Now.ToString("MM-dd-yyyy-h-mm") + "] MW3Guard is linked to (" + _primary_name_ + ") account");

            while (!thread_stop)
            {
                _botEnable = setGuardState(); //Check if there is something to secure

                if (_botEnable)
                {
                    nbClient_T = 0;

                    /* Mettre à jour la liste des joueurs */
                    for (i = 0; i < maxSlots; i++)
                    {
                        if (c_board[i] == null) c_board[i] = new client_data();
                        /* Download buffer from PS3 Host */
                        c_board[i].buffer1 = PS3_REMOTE.GetBytes((uint)(__PLAGE1__ + (i * __BLOCK1__)), 0x3980);
                        client_name_t = GetClientName(i);

                        //Is there anybody there?
                        if (!String.IsNullOrEmpty(client_name_t))
                        {
                            //Test for valid name (PSN rules used + allow () for splitscreen client)
                            if (!isValidName(client_name_t))
                            {
                                MW3_REMOTE.SV_KickClient(i, "was kicked for ^5illegal ^2name (Reason 12)");
                                PS3_REMOTE.CCAPI.Notify(CCAPI.NotifyIcon.CAUTION, client_name_t + ": kick for illegal name");
                                handle_db.setKickReason(client_name_t, 12, "illegal name");
                                _debug.WriteLine("[" + DateTime.Now.ToString("MM-dd-yyyy-h-mm") + "] Server auto-kick for "+ client_name_t+"; Illegal name.");
                                nbKicks++;
                                goto st_kicked;
                            }

                            //Check if client try to change their name in-game
                            if (!String.IsNullOrEmpty(c_board[i].client_name))
                            {
                                if (c_board[i].client_name != client_name_t)
                                {
                                    MW3_REMOTE.SV_KickClient(i, "was ^5kicked for ^2cheating (Reason 11)");
                                    PS3_REMOTE.CCAPI.Notify(CCAPI.NotifyIcon.CAUTION, client_name_t+": kick for name change");
                                    handle_db.setKickReason(client_name_t, 11, "name change");
                                    _debug.WriteLine("[" + DateTime.Now.ToString("MM-dd-yyyy-h-mm") + "] Server auto-kick for " + client_name_t + "; Name change");
                                    nbKicks++;
                                    goto st_kicked;
                                }
                            }
                            else
                            {
                                _debug.WriteLine("[" + DateTime.Now.ToString("MM-dd-yyyy-h-mm") + "] " + client_name_t + " is now connected..!");
                            }

                            c_board[i].client_name = client_name_t;

                            /* Upd1 buffer0 */
                            c_board[i].buffer0 = PS3_REMOTE.GetBytes((uint)(__PLAGE0__ + (i * __BLOCK0__)), 72);

                            /* Upd8 leaderboard things */
                            c_board[i].score = GetClientScore(i);
                            
                            killstmp = GetClientKills(i);
                            deathstmp = GetClientDeaths(i);

                            //Upd8 origin x,y,z
                            c_board[i].xp = getClientCoordinateX(i);
                            c_board[i].yp = getClientCoordinateY(i);
                            c_board[i].zp = getClientCoordinateZ(i);

                            // Guess killstreak
                            if (deathstmp == c_board[i].deaths)
                            {
                                c_board[i].currentKillStreak += (killstmp - c_board[i].kills);
                            }
                            else
                            {
                                c_board[i].currentKillStreak = 0;
                                c_board[i].spawnkill_analyse = true;
                            }

                            c_board[i].kills = killstmp;
                            c_board[i].deaths = deathstmp;

                            c_board[i].c_team = GetClientTeam(i);
                            c_board[i].n_level = getClientLevel(i);
                            c_board[i].n_prestige = getClientPrestige(i);
                            
                            //Check if client have redbox (cannot detect client local mod.) 
                            redboxEnabled = clientHaveRedBox(i);
                            
                            /* QuakeLike announcer (with iPrintLnBold + PlaySound) --> multi-kill, monster kill, god like, etc.. . */
                            if (enable_quakelike_announce) localAnnounce(i);

                            /* Just died.. Check if client could generate spawnkill or disturb other client with bad spawn */
                            if (enable_spawnkill_protection && GetClientHealth(i) > 0 && c_board[i].spawnkill_analyse)
                            {
                                if (clientSpawnkillProtectionActive(i) && clientRiskSpawnkill(i))
                                {
                                    ClientTeleportSaveOrigin(i, mostSecureOrigin(i)); //Teleport only if we found better origin point.
                                    _debug.WriteLine("[" + DateTime.Now.ToString("MM-dd-yyyy-h-mm") + "] Spawnkill protection had moved "+c_board[i].client_name+"..");
                                }
                                
                                c_board[i].spawnkill_analyse = false;
                            }

                            //Check if client is not playing..! (avoid ps3 freeze when calling server RPC outside game session)
                            if (!isGameFinished() && !isPlayerFreezed(i))
                            {
                                /* Camping detection begin here */
                                
                                // Don't apply with killstreak bonus (osprey, ac-130, predator, etc.. .)
                                if (!redboxEnabled && isClientProtectedArea(c_board[i].x0, c_board[i].y0, c_board[i].z0, c_board[i].xp, c_board[i].yp, c_board[i].zp))
                                {
                                    if (camping_analysis)
                                    {
                                        c_board[i].nbsec_camp++;

                                        if (c_board[i].nbsec_camp >= 14 && c_board[i].nbsec_camp < 18)
                                        {
                                            if (display_warnings) setClientAlertCamp(i, "en", 1);
                                        }
                                        else if (c_board[i].nbsec_camp >= 18 && c_board[i].nbsec_camp < 21)
                                        {
                                            if (display_warnings) setClientAlertCamp(i, "fr", 1);
                                        }
                                        else if (c_board[i].nbsec_camp >= 21)
                                        {
                                            // Nb of times caught to camp..
                                            c_board[i].nb_countcamp++;
                                            // If it's enough..
                                            if (c_board[i].nb_countcamp > _allow_nbcamp)
                                            {
                                                MW3_REMOTE.SV_KickClient(i, "has been ^4kicked for ^7camping too long");
                                                handle_db.setKickReason(client_name_t, 14, "camping");
                                                _debug.WriteLine("[" + DateTime.Now.ToString("MM-dd-yyyy-h-mm") + "] " + c_board[i].client_name + " has been auto-kicked for camping..");
                                                nbKicks++;
                                                goto st_kicked;
                                            }

                                            c_board[i].nbsec_camp = 0;
                                        }
                                    }
                                    
                                }
                                else
                                {
                                    // Draw new sphere from this very origin for new center.
                                    c_board[i].x0 = c_board[i].xp;
                                    c_board[i].y0 = c_board[i].yp;
                                    c_board[i].z0 = c_board[i].zp;
                                    c_board[i].nbsec_camp = 0;

                                    /* Save loc */
                                    saveClientOrigin(i);
                                    
                                }
                                
                                /* multi-language warning (EN; FR; ES; GE) */
                                if (c_board[i].warn_nb == 0 && c_board[i].kills >= 1)
                                {
                                    if (display_warnings) warnClient(i, "en");
                                    c_board[i].warn_nb++;
                                }
                                else if (c_board[i].warn_nb == 1 && c_board[i].kills >= 1)
                                {
                                    if (display_warnings) warnClient(i, "fr");
                                    c_board[i].warn_nb++;
                                }
                                else if (c_board[i].warn_nb == 2 && c_board[i].kills >= 1)
                                {
                                    if (display_warnings) warnClient(i, "es");
                                    c_board[i].warn_nb++;
                                }
                                else if (c_board[i].warn_nb == 3 && c_board[i].kills >= 1)
                                {
                                    if (display_warnings) warnClient(i, "ge");
                                    c_board[i].warn_nb++;
                                }
                                else if (c_board[i].warn_nb == 4 && c_board[i].kills >= 3)
                                {
                                    if (display_warnings) setClientAlertCamp(i, "en");
                                    c_board[i].warn_nb++;
                                }
                                else if (c_board[i].warn_nb == 5 && c_board[i].kills >= 3)
                                {
                                    if (display_warnings) setClientAlertCamp(i, "fr");
                                    c_board[i].warn_nb++;
                                }
                                else if (c_board[i].warn_nb == 6 && c_board[i].kills >= 3)
                                {
                                    if (display_warnings) setClientAlertCamp(i, "es");
                                    c_board[i].warn_nb++;
                                }
                                else if (c_board[i].warn_nb == 7 && c_board[i].kills >= 3)
                                {
                                    if (display_warnings) setClientAlertCamp(i, "ge");
                                    c_board[i].warn_nb++;
                                }
                                
                                /* Obvious cases of cheating */
                                if (clienthavegodmodeclass(i))
                                {
                                    MW3_REMOTE.SV_KickClient(i, "has been ^1kicked ^0for ^2cheating. ^7(Reason 1)");
                                    SetHostWarning(c_board[i].client_name + ": kick for god mode class");
                                    handle_db.setKickReason(client_name_t, 1, "God class");
                                    _debug.WriteLine("[" + DateTime.Now.ToString("MM-dd-yyyy-h-mm") + "] " + c_board[i].client_name + " has been auto-kicked for GodClass..");
                                    nbKicks++;
                                }
                                else if (haveClientUFOMode(i))
                                {
                                    MW3_REMOTE.SV_KickClient(i, "has been ^1kicked ^0for ^2cheating. ^7(Reason 2)");
                                    SetHostWarning(c_board[i].client_name + ": kick for ufomode");
                                    handle_db.setKickReason(client_name_t, 2, "Illegal clip");
                                    _debug.WriteLine("[" + DateTime.Now.ToString("MM-dd-yyyy-h-mm") + "] " + c_board[i].client_name + " has been auto-kicked for illegal clip");
                                    nbKicks++;
                                }
                                else if (haveClientUnlimitedAmmo(i))
                                {
                                    MW3_REMOTE.SV_KickClient(i, "has been ^1kicked ^0for ^2cheating. ^7(Reason 3)");
                                    SetHostWarning(c_board[i].client_name + ": kick for unlimited ammo ("+ getClientPrimaryAmmoAmount(i) +")");
                                    handle_db.setKickReason(client_name_t, 3, "Unlimited ammo");
                                    _debug.WriteLine("[" + DateTime.Now.ToString("MM-dd-yyyy-h-mm") + "] " + c_board[i].client_name + " has been auto-kicked for unlimited ammo");
                                    nbKicks++;
                                }
                                else if (haveClientWallhack(i))
                                {
                                    MW3_REMOTE.SV_KickClient(i, "has been ^1kicked ^0for ^2cheating. ^7(Reason 4)");
                                    SetHostWarning(c_board[i].client_name + ": kick for wallhack");
                                    handle_db.setKickReason(client_name_t, 4, "Wallhack");
                                    _debug.WriteLine("[" + DateTime.Now.ToString("MM-dd-yyyy-h-mm") + "] " + c_board[i].client_name + " has been auto-kicked for wallhack");
                                    nbKicks++;
                                }
                                else if (clientHaveNightVision(i))
                                {
                                    MW3_REMOTE.SV_KickClient(i, "has been ^1kicked ^0for ^2cheating. ^7(Reason 5)");
                                    SetHostWarning(c_board[i].client_name + ": kick for vision hack");
                                    handle_db.setKickReason(client_name_t, 5, "Illegal vision");
                                    _debug.WriteLine("[" + DateTime.Now.ToString("MM-dd-yyyy-h-mm") + "] " + c_board[i].client_name + " has been auto-kicked for vision hack");
                                    nbKicks++;
                                }
                                else if ((c_board[i].c_team == 2 || c_board[i].c_team == 3) && c_board[i].kills >= 1)
                                {
                                    MW3_REMOTE.SV_KickClient(i, "has been ^1kicked ^0for ^2cheating. ^7(Reason 6)");
                                    SetHostWarning(c_board[i].client_name + ": kick for team hack");
                                    handle_db.setKickReason(client_name_t, 6, "Team hack");
                                    _debug.WriteLine("[" + DateTime.Now.ToString("MM-dd-yyyy-h-mm") + "] " + c_board[i].client_name + " has been auto-kicked for team hack");
                                    nbKicks++;
                                }
                                else if (c_board[i].kills < 3 && redboxEnabled)
                                {
                                    //False positive occurs when someone truly have redbox enabled.. (need to investigate..)
                                    //MW3_REMOTE.SV_KickClient(i, "has been ^1kicked ^0for ^2cheating. ^7(Reason 7)");
                                    SetHostWarning(c_board[i].client_name + ": possible illegal mod.");
                                    _debug.WriteLine("[" + DateTime.Now.ToString("MM-dd-yyyy-h-mm") + "] " + c_board[i].client_name + " is suspected of illegal mod. (No-kick)");
                                    nbKicks++;
                                }
                                else if (haveClientExplosivBullets(i))
                                {
                                    MW3_REMOTE.SV_KickClient(i, "has been ^1kicked ^0for ^2cheating. ^7(Reason 8)");
                                    SetHostWarning(c_board[i].client_name + ": kick for explosiv bullets");
                                    handle_db.setKickReason(client_name_t, 8, "Bullet mod.");
                                    _debug.WriteLine("[" + DateTime.Now.ToString("MM-dd-yyyy-h-mm") + "] " + c_board[i].client_name + " has been kicked for having bullet illegal mod.");
                                    nbKicks++;
                                }
                                else if (GetClientInvisibleStatus(i) && c_board[i].kills >= 1)
                                {
                                    MW3_REMOTE.SV_KickClient(i, "has been ^1kicked ^0for ^2cheating. ^7(Reason 9)");
                                    SetHostWarning(c_board[i].client_name + ": kick for invisible class");
                                    handle_db.setKickReason(client_name_t, 9, "Invisible");
                                    _debug.WriteLine("[" + DateTime.Now.ToString("MM-dd-yyyy-h-mm") + "] " + c_board[i].client_name + " has been kicked for having bullet being invisible");
                                    nbKicks++;
                                }
                                else if(enable_uav_redbox_analysis && c_board[i].probaSuccess > 2.1F && c_board[i].kills > 10)
                                {
                                    c_board[i].nb_alert_ratio_re++;

                                    if (c_board[i].nb_alert_ratio_re == 1)
                                    {
                                        SetHostWarning(c_board[i].client_name + ": may have illegal UAV or RedBox");
                                        _debug.WriteLine("[" + DateTime.Now.ToString("MM-dd-yyyy-h-mm") + "] " + c_board[i].client_name + " may have illegal UAV or RedBox (No-kick)");
                                    }
                                    else if (c_board[i].nb_alert_ratio_re == 15)
                                    {
                                        SetHostWarning(c_board[i].client_name + ": probable illegal UAV or RedBox");
                                        _debug.WriteLine("[" + DateTime.Now.ToString("MM-dd-yyyy-h-mm") + "] " + c_board[i].client_name + " have probable illegal UAV or RedBox (No-kick)");
                                    }else if(c_board[i].nb_alert_ratio_re == 25)
                                    {
                                        SetHostWarning(c_board[i].client_name + ": high chance of illegal UAV or RedBox");
                                        _debug.WriteLine("[" + DateTime.Now.ToString("MM-dd-yyyy-h-mm") + "] " + c_board[i].client_name + " should have illegal UAV or RedBox (No-kick)");
                                    }
                                    
                                    
                                }
                                else if (__voteKick == i)
                                {
                                    
                                    switch (__voteReason)
                                    {
                                        case 0:
                                            MW3_REMOTE.SV_KickClient(i, "has been ^1kicked ^0for ^2violent ^1language. ^7(Reason 10)");
                                            break;
                                        case 1:
                                            MW3_REMOTE.SV_KickClient(i, "has been ^1kicked ^0for ^2cheating. ^7(Reason 10)");
                                            break;
                                        case 2:
                                            MW3_REMOTE.SV_KickClient(i, "has been ^1kicked ^0for ^2playing ^8against his ^1comrades. ^7(Reason 10)");
                                            break;
                                        default:
                                            MW3_REMOTE.SV_KickClient(i, "has been ^1kicked ^0for ^2cheating. ^7(Reason 10)");
                                            break;
                                    }

                                    SetHostWarning(c_board[i].client_name + ": kick by vote");
                                    handle_db.setKickReason(client_name_t, 10, "By admin");
                                    _debug.WriteLine("[" + DateTime.Now.ToString("MM-dd-yyyy-h-mm") + "] " + c_board[i].client_name + " has been kicked by admin");
                                    nbKicks++;
                                    __voteKick = -1;
                                    __voteReason = -1;
                                    goto st_kicked;
                                }

                                /* Calc Ratio get close / away from opposite team barrycenter */
                                if (enable_uav_redbox_analysis)
                                {
                                    for (j = 0; j < maxSlots; j++)
                                    {
                                        if (c_board[j] != null && !String.IsNullOrEmpty(c_board[j].client_name) && c_board[j].c_team != c_board[i].c_team && c_board[j].c_team != 2)
                                        {
                                            c_board[i].barycentre_x += c_board[j].xp;
                                            c_board[i].barycentre_y += c_board[j].yp;
                                            c_board[i].barycentre_z += c_board[j].zp;
                                            nbPoints++;
                                        }
                                    }

                                    if (nbPoints > 0)
                                    {
                                        c_board[i].barycentre_x /= (float)nbPoints;
                                        c_board[i].barycentre_y /= (float)nbPoints;
                                        c_board[i].barycentre_z /= (float)nbPoints;

                                        tmpDistance = distancePoints(c_board[i].barycentre_x, c_board[i].barycentre_y, c_board[i].barycentre_z, c_board[i].xp, c_board[i].yp, c_board[i].zp);
                                        if (tmpDistance < c_board[i].last_distance)
                                        {
                                            c_board[i].rapprochements++;
                                        }
                                        else
                                        {
                                            c_board[i].eloignements++;
                                        }

                                        c_board[i].last_distance = tmpDistance;
                                        nbPoints = 0;

                                        if (c_board[i].eloignements > 0 && c_board[i].rapprochements > 0)
                                        {
                                            c_board[i].probaSuccess = ((float)c_board[i].rapprochements / (float)c_board[i].eloignements);
                                        }
                                        else if (c_board[i].eloignements == 0 && c_board[i].rapprochements > 0)
                                        {
                                            c_board[i].probaSuccess = -1; //Weird case, could not be possible unless UAV is constantly on. (In theory..)
                                        }
                                        else
                                        {
                                            c_board[i].probaSuccess = 0;
                                        }
                                    }
                                    
                                }
                                
                            }

                            nbClient_T++;
                            c_board[i].cl_inter++;

                        }
                        else
                        {

                            if (!String.IsNullOrEmpty(c_board[i].client_name))
                            {
                                _debug.WriteLine("[" + DateTime.Now.ToString("MM-dd-yyyy-h-mm") + "] " + c_board[i].client_name + " disconnected..");
                                if (c_board[i].c_team > -1 && c_board[i].c_team < 2) team_score_bak[c_board[i].c_team] += c_board[i].score;
                            }

                            c_board[i].score = 0;
                            c_board[i].kills = 0;
                            c_board[i].deaths = 0;
                            c_board[i].c_health = 0;
                            c_board[i].c_primmary_ammo = 0;
                            c_board[i].c_team = 0;
                            c_board[i].client_name = String.Empty;
                            c_board[i].cl_inter = 0;
                            c_board[i].n_level = 1;
                            c_board[i].n_prestige = 0;
                            c_board[i].nb_alert_ratio_re = 0;

                            c_board[i].spawnkill_analyse = false;
                            c_board[i].c_save = 0;

                            if (__voteKick == i)
                            {
                                __voteKick = -1;
                                __voteReason = -1;
                                PS3_REMOTE.CCAPI.Notify(CCAPI.NotifyIcon.CAUTION, "Client "+i+": Already gone");
                            }
                        }
                        st_kicked:
                        redboxEnabled = false;

                    }

                    //We may went to disable patch on sv_matchend on team abord.
                    if (!disable_sm_me && disable_sv_matchend && ((NbClientTeam(0) == 0 || NbClientTeam(1) == 0) || (nbClient == 1 && nbClient_T == 1)))
                    {
                        disable_sm_me = true;
                        swap_sv_me_m(false);
                        _debug.WriteLine("[" + DateTime.Now.ToString("MM-dd-yyyy-h-mm") + "] Re-enable sv_matchend because game could end.. (Lack of client)");
                    }
                    else if(disable_sm_me && nbClient > 2 && nbClient_T > 2)
                    {
                        swap_sv_me_m(true);
                        disable_sm_me = false;
                        _debug.WriteLine("[" + DateTime.Now.ToString("MM-dd-yyyy-h-mm") + "] sv_matchend disabled.. (Again)");
                    }

                    /* Upd8 actual client counter */
                    nbClient = nbClient_T;

                    /* Auto-balancing in case of major ragequit.. */
                    //AutoBalancing(); Does not work properly.. Working in a fix.

                    if (isGameFinished() && nbClient > 0)
                    {
                        if (disable_sv_matchend) swap_sv_me_m(false); //If the session is about to end, we need to re-enable sv_matchend to avoid ps3 goes into infinite loop.
                        PS3_REMOTE.CCAPI.Notify(CCAPI.NotifyIcon.INFO, "MW3Guard is going to sleep.");
                        _debug.WriteLine("[" + DateTime.Now.ToString("MM-dd-yyyy-h-mm") + "] MW3Guard is about to sleep.. (session ending)");
                        while (MW3_REMOTE.cl_ingame() && !thread_stop) Thread.Sleep(200);
                    }

                }
                else
                {
                    PS3_REMOTE.CCAPI.Notify(CCAPI.NotifyIcon.CAUTION, "MW3Guard is sleeping..");
                    _debug.WriteLine("[" + DateTime.Now.ToString("MM-dd-yyyy-h-mm") + "] MW3Guard is waiting for another session..");
                    Array.Clear(c_board, 0, maxSlots);
                    MW3_REMOTE.lastsoundreq = "";
                    MW3_REMOTE.lastreq = -1;
                    nbKicks = 0;
                    current_gamemode = "";
                    current_maps = "";
                    nbClient = 0;
                    Array.Clear(team_score_bak, 0, 2);
                    /* We shall wait until next session to be started */
                    while (!MW3_REMOTE.cl_ingame() && !thread_stop) Thread.Sleep(500);
                    if (disable_sv_matchend) swap_sv_me_m(true); //Disable sv_matchend (by corrupting it..)
                }

                Thread.Sleep(50);
            }

            _debug.Close();
        }

        /// <summary>
        /// Get client level 1-80
        /// </summary>
        /// <param name="pID">Client ID (Server side)</param>
        private int getClientLevel(int pID)
        {
            if (c_board[pID] == null) return 1;
            return (c_board[pID].buffer1[Offsets.Block1.Level]);
        }
        /// <summary>
        /// Get client prestige 0-20
        /// </summary>
        /// <param name="pID">Client ID (Server side)</param>
        private int getClientPrestige(int pID)
        {
            if (c_board[pID] == null) return 0;
            return (c_board[pID].buffer1[Offsets.Block1.Prestige]);
        }

        /* LightQuake Announce for killstreak */
        /// <summary>
        /// QuakeLike announcer, multi-kill, monster kill, eagle eye, etc.. .
        /// </summary>
        /// <param name="pID">Client ID (Server side)</param>
        private void localAnnounce(int pID)
        {
            if (c_board[pID].currentKillStreak == c_board[pID].lastKillStreak) return;

            switch (c_board[pID].currentKillStreak)
            {
                case 0: //Looking on the last killStreak!

                    if (c_board[pID].lastKillStreak > 20)
                    {
                        MW3_REMOTE.iPrintlnBold(-1, c_board[pID].client_name + " ^8died and ^4gained favour from the ^5gods.");
                        MW3_REMOTE.PlaySound(-1, "mp_bonus_start");
                        announceSound = new SoundPlayer(@"QuakeSounds\flawless.wav");
                        announceSound.Play();
                    }
                    else if (c_board[pID].lastKillStreak > 15)
                    {
                        MW3_REMOTE.iPrintlnBold(-1, c_board[pID].client_name + " was too much ^3bloodthirsty..");
                        MW3_REMOTE.PlaySound(-1, "mp_bonus_start");
                        announceSound = new SoundPlayer(@"QuakeSounds\flawless.wav");
                        announceSound.Play();
                    }
                    else if(c_board[pID].lastKillStreak > 10)
                    {
                        MW3_REMOTE.iPrintlnBold(-1, c_board[pID].client_name + " had ^8forgot that he was ^6mortal after all..");
                        MW3_REMOTE.PlaySound(-1, "mp_bonus_start");
                        announceSound = new SoundPlayer(@"QuakeSounds\hattrick.wav");
                        announceSound.Play();
                    }
                    else if (c_board[pID].lastKillStreak > 8)
                    {
                        MW3_REMOTE.iPrintlnBold(-1, c_board[pID].client_name + " claim the ^6throne too ^8early, it seem.");
                        MW3_REMOTE.PlaySound(-1, "mp_bonus_start");
                        announceSound = new SoundPlayer(@"QuakeSounds\hattrick.wav");
                        announceSound.Play();
                    }
                    else if (c_board[pID].lastKillStreak > 5)
                    {
                        MW3_REMOTE.iPrintlnBold(-1, c_board[pID].client_name + " forgot that ^5gluttony is a ^4mortal sin!");
                        MW3_REMOTE.PlaySound(-1, "mp_bonus_start");
                    }

                    break;
                case 3: //Triple kill
                    MW3_REMOTE.iPrintlnBold(-1, c_board[pID].client_name+" starting with ^3triple-kill!");
                    MW3_REMOTE.PlaySound(-1, "mp_bonus_start");
                    announceSound = new SoundPlayer(@"QuakeSounds\triplekill.wav");
                    announceSound.Play();
                    break;
                case 5:
                    MW3_REMOTE.iPrintlnBold(-1, c_board[pID].client_name +" aspire to be an ^4Olympian! (Multi-kill)");
                    MW3_REMOTE.PlaySound(-1, "mp_bonus_start");
                    announceSound = new SoundPlayer(@"QuakeSounds\multikill.wav");
                    announceSound.Play();
                    break;
                case 6:
                    MW3_REMOTE.iPrintlnBold(-1, c_board[pID].client_name + " is in a ^5rampage!");
                    MW3_REMOTE.PlaySound(-1, "mp_bonus_start");
                    announceSound = new SoundPlayer(@"QuakeSounds\rampage.wav");
                    announceSound.Play();
                    break;
                case 7:
                    MW3_REMOTE.iPrintlnBold(-1, c_board[pID].client_name + " is in ^6killing ^7spree!");
                    MW3_REMOTE.PlaySound(-1, "mp_bonus_start");
                    announceSound = new SoundPlayer(@"QuakeSounds\killingspree.wav");
                    announceSound.Play();
                    break;
                case 9:
                    MW3_REMOTE.iPrintlnBold(-1, c_board[pID].client_name + " is ^6dominating!");
                    MW3_REMOTE.PlaySound(-1, "mp_bonus_start");
                    announceSound = new SoundPlayer(@"QuakeSounds\dominating.wav");
                    announceSound.Play();
                    break;
                case 11:
                    MW3_REMOTE.iPrintlnBold(-1, c_board[pID].client_name + " is ^8un^7stoppable!");
                    MW3_REMOTE.PlaySound(-1, "mp_bonus_start");
                    announceSound = new SoundPlayer(@"QuakeSounds\unstoppable.wav");
                    announceSound.Play();
                    break;
                case 13:
                    MW3_REMOTE.iPrintlnBold(-1, c_board[pID].client_name + " comes to ^9mega-kill!");
                    MW3_REMOTE.PlaySound(-1, "mp_bonus_start");
                    announceSound = new SoundPlayer(@"QuakeSounds\megakill.wav");
                    announceSound.Play();
                    break;
                case 15:
                    MW3_REMOTE.iPrintlnBold(-1, c_board[pID].client_name + " comes to ^3ultra-^7kill!");
                    MW3_REMOTE.PlaySound(-1, "mp_bonus_start");
                    announceSound = new SoundPlayer(@"QuakeSounds\ultrakill.wav");
                    announceSound.Play();
                    break;
                case 16:
                    MW3_REMOTE.iPrintlnBold(-1, c_board[pID].client_name + " have ^4eagle ^7eye!");
                    MW3_REMOTE.PlaySound(-1, "mp_bonus_start");
                    announceSound = new SoundPlayer(@"QuakeSounds\eagleeye.wav");
                    announceSound.Play();
                    break;
                case 17:
                    MW3_REMOTE.iPrintlnBold(-1, c_board[pID].client_name + " ^5own ^7your souls!");
                    MW3_REMOTE.PlaySound(-1, "mp_bonus_start");
                    announceSound = new SoundPlayer(@"QuakeSounds\ownage.wav");
                    announceSound.Play();
                    break;
                case 18:
                    MW3_REMOTE.iPrintlnBold(-1, c_board[pID].client_name + " have made a ^4pact with ^5Lucifer!");
                    MW3_REMOTE.PlaySound(-1, "mp_bonus_start");
                    announceSound = new SoundPlayer(@"QuakeSounds\ludicrouskill.wav");
                    announceSound.Play();
                    break;
                case 19:
                    MW3_REMOTE.iPrintlnBold(-1, c_board[pID].client_name + " is a ^4head ^7hunter!");
                    MW3_REMOTE.PlaySound(-1, "mp_bonus_start");
                    announceSound = new SoundPlayer(@"QuakeSounds\headhunter.wav");
                    announceSound.Play();
                    break;
                case 20:
                    MW3_REMOTE.iPrintlnBold(-1, c_board[pID].client_name + " is a ^8wicked ^9sick!");
                    MW3_REMOTE.PlaySound(-1, "mp_bonus_start");
                    announceSound = new SoundPlayer(@"QuakeSounds\whickedsick.wav");
                    announceSound.Play();
                    break;
                case 21:
                    MW3_REMOTE.iPrintlnBold(-1, c_board[pID].client_name + " is a ^7mons^5ter!");
                    MW3_REMOTE.PlaySound(-1, "mp_bonus_start");
                    announceSound = new SoundPlayer(@"QuakeSounds\monsterkill.wav");
                    announceSound.Play();
                    break;
                case 23:
                    MW3_REMOTE.iPrintlnBold(-1, c_board[pID].client_name + " being a ^3half-^8god! All Hail The King !");
                    MW3_REMOTE.PlaySound(-1, "mp_bonus_start");
                    announceSound = new SoundPlayer(@"QuakeSounds\holyshit.wav");
                    announceSound.Play();
                    break;
                case 24:
                    MW3_REMOTE.iPrintlnBold(-1, c_board[pID].client_name + " playing ^7god ^4like! ^3Kneel before ^7him!");
                    MW3_REMOTE.PlaySound(-1, "mp_bonus_start");
                    announceSound = new SoundPlayer(@"QuakeSounds\godlike.wav");
                    announceSound.Play();
                    break;
            }
            c_board[pID].lastKillStreak = c_board[pID].currentKillStreak;
        }
        /// <summary>
        /// Check if gaming session is about to end or not.
        /// </summary>
        private bool isGameFinished()
        {
            if (getTeamScore(0) == 0 && getTeamScore(1) == 0) return false;
            int i = 0, j = 0, nbMatch = 0, nbTests = 0;
            
            if (getCurrentGameMode() == "Team Deathmatch")
            {
                if (getTeamScore(0) >= 7000 || getTeamScore(1) >= 7000) return true;
            }

            for (i = 0; i < maxSlots; i++)
            {
                if (c_board[i] != null && !string.IsNullOrEmpty(c_board[i].client_name) && c_board[i].xp != 0 && c_board[i].yp != 0 && c_board[i].zp != 0)
                {
                    for (j = 0; j < maxSlots; j++)
                    {
                        if (c_board[j] != null && !string.IsNullOrEmpty(c_board[j].client_name) && i != j)
                        {
                            if ((c_board[i].xp == c_board[j].xp && c_board[i].yp == c_board[j].yp && c_board[i].zp == c_board[j].zp) || (isPlayerFreezed(i) == isPlayerFreezed(j) && isPlayerFreezed(i)))
                            {
                                nbMatch++;
                            }
                            nbTests++;
                        }
                    }
                    if (nbMatch > ((nbTests / 2))) return true;
                    nbMatch = 0;
                }
            }
            
            return false;
        }
        /// <summary>
        /// Check if client is currently freezed or not
        /// </summary>
        /// <param name="pID">Client ID (Server side)</param>
        private bool isPlayerFreezed(int pID)
        {
            if (c_board[pID] == null) return false;
            if (c_board[pID].buffer1[Offsets.Block1.Clip] == 0x07) return true;
            return false;
        }
        /// <summary>
        /// Get current map name
        /// </summary>
        private string getMapName()
        {
            //a == "mp_seatown" | a == "mp_plaza2" | a == "mp_exchange" | a == "mp_bootleg" | a == "mp_alpha" | a == "mp_village" | a == "mp_bravo" | a == "mp_courtyard_ss" | a == "mp_aground_ss"
            switch (ReturnInfos(6))
            {
                case "mp_alpha":
                    return "Lockdown";

                case "mp_bootleg":
                    return "Bootleg";

                case "mp_bravo":
                    return "Mission";

                case "mp_carbon":
                    return "Carbon";

                case "mp_dome":
                    return "Dome";

                case "mp_exchange":
                    return "Downturn";

                case "mp_hardhat":
                    return "Hardhat";

                case "mp_interchange":
                    return "Interchange";

                case "mp_lambeth":
                    return "Fallen";

                case "mp_mogadishu":
                    return "Bakaara";

                case "mp_paris":
                    return "Resistance";

                case "mp_plaza2":
                    return "Arkaden";

                case "mp_radar":
                    return "Outpost";

                case "mp_seatown":
                    return "Seatown";

                case "mp_underground":
                    return "Underground";

                case "mp_village":
                    return "Village";

                case "mp_aground_ss":
                    return "Aground";

                case "mp_aqueduct_ss":
                    return "Aqueduct";

                case "mp_cement":
                    return "Foundation";

                case "mp_hillside_ss":
                    return "Getaway";

                case "mp_italy":
                    return "Piazza";

                case "mp_meteora":
                    return "Sanctuary";

                case "mp_morningwood":
                    return "Black Box";

                case "mp_overwatch":
                    return "Overwatch";

                case "mp_park":
                    return "Liberation";

                case "mp_qadeem":
                    return "Oasis";

                case "mp_restrepo_ss":
                    return "Lookout";

                case "mp_terminal_cls":
                    return "Terminal";
            }
            return "Unknown Map";
        }
        /// <summary>
        /// Get current hostname
        /// </summary>
        private string getHostName()
        {
            string str = this.ReturnInfos(0x10);

            switch (str)
            {
                case "Modern Warfare 3":
                    return "NoHost"; //Outgame

                case "":
                    return "OutGame"; //Outgame
            }

            return str; //InGame
        }
        /// <summary>
        /// Get distance between two origin in the 3D world.
        /// </summary>
        /// <param name="x0">Origin A; X</param>
        /// <param name="y0">Origin A; Y</param>
        /// <param name="z0">Origin A; Z</param>
        /// <param name="xp">Origin B; X</param>
        /// <param name="yp">Origin B; Y</param>
        /// <param name="zp">Origin B; Z</param>
        /// <returns>Distance between two origin</returns>
        private double distancePoints(float x0, float y0, float z0, float xp, float yp, float zp)
        {
            double res = Math.Pow((x0 - xp), 2) + Math.Pow((y0 - yp), 2) + Math.Pow((z0 - zp), 2);
            double distance = Math.Sqrt(res);
            return distance;
        }
        /// <summary>
        /// Test if client is in protected area
        /// </summary>
        private bool isClientProtectedArea(float x0, float y0, float z0, float xp, float yp, float zp)
        {
            if (x0 == 0 && y0 == 0 && z0 == 0) return false;
            double res = Math.Pow((x0 - xp), 2) + Math.Pow((y0 - yp), 2) + Math.Pow((z0 - zp), 2);
            double distance = Math.Sqrt(res);
            
            if (distance <= rayon) return true;
            return false;
        }

        /// <summary>
        /// Display warning about camping
        /// </summary>
        /// <param name="pID">Client(Server side)</param>
        /// <param name="local">Language</param>
        /// <param name="level">0; Simple warning -- 1; Last warning</param>
        private void setClientAlertCamp(int pID, string local, int level = 0)
        {
            switch (local)
            {
                case "en":
                    if (level == 0)
                    {
                        MW3_REMOTE.iPrintln(pID, "^1Warning: ^2You ^7can't stay on the same ^5spot ^7too ^7long!");
                    }else if(level == 1)
                    {
                        MW3_REMOTE.iPrintln(pID, "^1Server: ^2Last ^7warning, ^5change your position ^7now!");
                    }
                    break;
                case "fr":
                    if (level == 0)
                    {
                        MW3_REMOTE.iPrintln(pID, "^1Attention: ^3Interdiction formelle de ^7'camper' !");
                    }else if (level == 1)
                    {
                        MW3_REMOTE.iPrintln(pID, "^1Serveur: ^3Dernier avertissement, ^7deplacez-vous!");
                    }
                    break;
                case "es":
                    if (level == 0)
                    {
                        MW3_REMOTE.iPrintln(pID, "^1Advertencia! ^7Debe ^4desplazarse !");
                    }else if(level == 1)
                    {
                        MW3_REMOTE.iPrintln(pID, "^1Advertencia! ^7Debe ^4desplazarse !");
                    }
                    break;
                case "ge":
                    if (level == 0)
                    {
                        MW3_REMOTE.iPrintln(pID, "^3Sie ^3konnen nicht an der gleichen ^4Stelle zu ^7lange ^7bleiben!");
                    }else if(level == 1)
                    {
                        MW3_REMOTE.iPrintln(pID, "^3Sie ^3konnen nicht an der gleichen ^4Stelle zu ^7lange ^7bleiben!");
                    }
                    break;
            }
        }

        /// <summary>
        /// Detect if bullet are modded for specific client.
        /// </summary>
        /// <param name="pID">Client(Server side)</param>
        private bool haveClientExplosivBullets(int pID)
        {
            if (c_board[pID] == null) return false;
            if ((c_board[pID].buffer1[Offsets.Block1.ExplosiveBullet] == 0xC5) && (c_board[pID].buffer1[Offsets.Block1.ExplosiveBullet+1] == 0xFF)) return true;
            return false;
        }

        /// <summary>
        /// Get current max client supported by server (Not by bandwith!)
        /// </summary>
        private string getMaxPlayers()
        {
            return ReturnInfos(18);
        }

        /// <summary>
        /// Get current game mode
        /// </summary>
        private string getCurrentGameMode()
        {
            switch (this.ReturnInfos(2))
            {
                case "war":
                    return "Team Deathmatch";

                case "dm":
                    return "Free for All";

                case "sd":
                    return "Search and Destroy";

                case "dom":
                    return "Domination";

                case "conf":
                    return "Kill Confirmed";

                case "sab":
                    return "Sabotage";

                case "koth":
                    return "Head Quartes";

                case "ctf":
                    return "Capture The Flag";

                case "infect":
                    return "Infected";

                case "sotf":
                    return "Hunted";

                case "dd":
                    return "Demolition";

                case "grnd":
                    return "Drop Zone";

                case "tdef":
                    return "Team Defender";

                case "tjugg":
                    return "Team Juggernaut";

                case "jugg":
                    return "Juggernaut";

                case "gun":
                    return "Gun Game";

                case "oic":
                    return "One In The Chamber";
            }
            return "Unknown Gametype";
        }

        private bool haveTeamMark(string input)
        {
            if (input[0] != '[') return false;
            return true;
        }

        private string extractNameWithoutTeamMark(string input)
        {
            int i = 0, len = input.Length, maxInd = 0;
            if (!haveTeamMark(input)) return input;
            for (i = 0; i < 6; i++)
            {
                if (input[i] == ']') maxInd = i;
            }

            return input.Substring(maxInd+1, len - (maxInd+1));
        }

        /// <summary>
        /// Check if we are in game and hosting the game
        /// </summary>
        private bool setGuardState()
        {

            if (!MW3_REMOTE.cl_ingame())
            {
                return false;
            }else if(_botEnable && nbClient > 0)
            {
                return true;
            }

            /* Upd8 headers */
            _debug.WriteLine("[" + DateTime.Now.ToString("MM-dd-yyyy-h-mm") + "] Downloading session header infos..");
            getHeaders();

            string c_host = getHostName();
            current_host = c_host;

            if (extractNameWithoutTeamMark(current_host) != _primary_name_)
            {
                //ForceHosting(); DONT DO THAT! Create new instance instead!
                _debug.WriteLine("[" + DateTime.Now.ToString("MM-dd-yyyy-h-mm") + "] MW3Guard cannot work if you'r not the host..!");
                
                return false;
            }

            string c_gamemode = getCurrentGameMode();
            current_gamemode = c_gamemode;
            current_maxplayer = getMaxPlayers();
            maxSlots = int.Parse(current_maxplayer);
            if (maxSlots < 0) maxSlots = 0;
            string c_maps = getMapName();

            if (c_maps != current_maps)
            {
                _debug.WriteLine("[" + DateTime.Now.ToString("MM-dd-yyyy-h-mm") + "] MW3Guard is waking up.. ("+current_gamemode+"; "+ c_maps + "; "+current_host+")");
                PS3_REMOTE.CCAPI.Notify(CCAPI.NotifyIcon.INFO, "New instance");
                Array.Clear(c_board, 0, 18);
                current_maps = c_maps;
                return true;
            }

            if (c_maps != "Unknown Map") return true;
            
            current_maps = "";
            _debug.WriteLine("[" + DateTime.Now.ToString("MM-dd-yyyy-h-mm") + "] Weird situation.. Error! (SetGuardState)");
            return false;
        }

        /// <summary>
        /// Send notif throuth CCAPI
        /// </summary>
        private void SetHostWarning(string text)
        {
            PS3_REMOTE.CCAPI.Notify(CCAPI.NotifyIcon.CAUTION, text);
        }

        /// <summary>
        /// Check if client is watching killcam, just about simple test on health.
        /// </summary>
        /// <param name="pID">Client ID (Server side)</param>
        private bool GetKillCamStatus(int pID)
        {
            if (c_board[pID] == null) return false;
            if (c_board[pID].buffer0[Offsets.Block0.Health] == 0x0) return true; //c_board[pID].buffer0[Offsets.Block0.Model + 5] == 0x0B && 
            return false;
        }

        /// <summary>
        /// Get current client name
        /// </summary>
        /// <param name="pID">Client ID (Server side)</param>
        private string GetClientName(int pID)
        {
            if (c_board[pID] == null) return String.Empty;
            byte[] data = new byte[40];
            string str = "";

            data = bytecpy(ref c_board[pID].buffer1, Offsets.Block1.Name, Offsets.Block1.Name + 39);
            str += Encoding.UTF8.GetString(data);
            int index = str.IndexOf('\0');
            string final = str.Substring(0, index);

            if (final.Length < 2) return string.Empty;

            return final;
        }

        /// <summary>
        /// Extract part of byte array to another, similar to memcpy in c/c++
        /// </summary>
        /// <param name="origin">Array to read</param>
        /// <param name="start">Start index</param>
        /// <param name="end">End index</param>
        private byte[] bytecpy(ref byte[] origin, uint start, uint end)
        {
            if (end < start) return null;

            uint i = 0, j = 0;
            byte[] res = new byte[(end-start) + 1];
            for (i = start; i < (end+1); i++)
            {
                res[j] = origin[i];
                j++;
            }
            return res;
        }

        /// <summary>
        /// Get client health 0-100
        /// </summary>
        /// <param name="pID">Client ID (Server side)</param>
        private int GetClientHealth(int pID)
        {
            if (c_board[pID] == null) return 0;
            return c_board[pID].buffer0[Offsets.Block0.Health];
        }

        /// <summary>
        /// Get client current score
        /// </summary>
        /// <param name="pID">Client ID (Server side)</param>
        private int GetClientScore(int pID)
        {
            if (c_board[pID] == null) return 0;
            byte[] data = new byte[4];
            data = bytecpy(ref c_board[pID].buffer1, Offsets.Block1.Score, Offsets.Block1.Score + 3);
            Array.Reverse(data, 0, 4);

            return BitConverter.ToInt32(data, 0);
        }

        /// <summary>
        /// Get client current kills
        /// </summary>
        /// <param name="pID">Client ID (Server side)</param>
        private int GetClientKills(int pID)
        {
            if (c_board[pID] == null) return 0;
            byte[] data = new byte[4];
            data = bytecpy(ref c_board[pID].buffer1, Offsets.Block1.Kills, Offsets.Block1.Kills + 3);
            Array.Reverse(data, 0, 4);
            return BitConverter.ToInt32(data, 0);
        }

        /// <summary>
        /// Get client current deaths
        /// </summary>
        /// <param name="pID">Client ID (Server side)</param>
        private int GetClientDeaths(int pID)
        {
            if (c_board[pID] == null) return 0;
            byte[] data = new byte[4];
            data = bytecpy(ref c_board[pID].buffer1, Offsets.Block1.Deaths, Offsets.Block1.Deaths + 3);
            Array.Reverse(data, 0, 4);
            return BitConverter.ToInt32(data, 0);
        }

        /// <summary>
        /// Get client primary ammo amount (0-150)
        /// </summary>
        /// <param name="pID">Client ID (Server side)</param>
        public int getClientPrimaryAmmoAmount(int pID)
        {
            if (c_board[pID] == null) return 0;
            return c_board[pID].buffer1[Offsets.Block1.PrimaryAmmo];
        }


        /* DO NOT WORK, BAD OFFSET! */
        /// <summary>
        /// Check if UAV is enabled for specific client (Does not work yet)
        /// </summary>
        /// <param name="pID">Client ID (Server side)</param>
        public bool getClientUAVStatus(int pID)
        {
            if (c_board[pID] == null) return false;
            if (c_board[pID].buffer1[Offsets.Block1.UAV] == 0x01) return true;
            return false;
        }

        /// <summary>
        /// Check if client have more than 150 bullets per mag for primary weapon
        /// </summary>
        /// <param name="pID">Client ID (Server side)</param>
        private bool haveClientUnlimitedAmmo(int pID)
        {
            if (c_board[pID] == null) return false;
            if (getClientPrimaryAmmoAmount(pID) > 150) return true;
            return false;
        }

        /// <summary>
        /// Check if client have legal clip type.
        /// </summary>
        /// <param name="pID">Client ID (Server side)</param>
        private bool haveClientUFOMode(int pID)
        {
            if (c_board[pID] == null) return false;
            if (c_board[pID].buffer1[Offsets.Block1.Clip] == 0x02) return true;
            return false;
        }

        /// <summary>
        /// Check if client intend to use wallhack mod.
        /// </summary>
        /// <param name="pID">Client ID (Server side)</param>
        private bool haveClientWallhack(int pID)
        {
            if (c_board[pID].wallhack_pr == 1) return false;
            byte[] data = new byte[2] { 0x02, 0x81 };

            if (!GetClientInvisibleStatus(pID))
                c_board[pID].wallhack_pr = 1;

            data = PS3_REMOTE.GetBytes((uint)(Offsets.Wallhack + (0x3980 * pID)), 2);
            if (data[0] == 0x01 && data[1] == 0x2c) return true;
            return false;
        }

        //Night vision + shoot hand grenade detection
        /// <summary>
        /// Check if client is using legal vision
        /// </summary>
        /// <param name="pID">Client ID (Server side)</param>
        private bool clientHaveNightVision(int pID)
        {
            if (c_board[pID] == null) return false;
            if (c_board[pID].buffer1[Offsets.Block1.Vision] == 0xFF || c_board[pID].buffer1[Offsets.Block1.Vision] == 0x40 || c_board[pID].buffer1[Offsets.Block1.Vision] == 0x0A) return true;
            return false;
        }

        /// <summary>
        /// Get client redbox status. (Legit one..) Does not detect client local mod.
        /// </summary>
        /// <param name="pID">Client ID (Server side)</param>
        private bool clientHaveRedBox(int pID)
        {
            if (c_board[pID] == null) return false;
            if (c_board[pID].buffer1[Offsets.Block1.Redbox] == 0x10) return true;
            return false;
        }

        /*private void setClientRedBox(int pID)
        {
            PS3_REMOTE.SetMemory((uint)(0x0110a293 + (0x3980 * pID)), new byte[] { 0x10 });
        }*/

        private float getClientCoordinateX(int pID)
        {
            if (c_board[pID] == null) return 0;
            byte[] data = new byte[4];
            data = bytecpy(ref c_board[pID].buffer1, Offsets.Block1.OriginX, Offsets.Block1.OriginX + 3);
            Array.Reverse(data, 0, 4);
            
            return BitConverter.ToSingle(data, 0);
        }

        private float getClientCoordinateY(int pID)
        {
            if (c_board[pID] == null) return 0;
            byte[] data = new byte[4];
            data = bytecpy(ref c_board[pID].buffer1, Offsets.Block1.OriginY, Offsets.Block1.OriginY + 3);
            Array.Reverse(data, 0, 4);

            return BitConverter.ToSingle(data, 0);
        }

        private float getClientCoordinateZ(int pID)
        {
            if (c_board[pID] == null) return 0;
            byte[] data = new byte[4];
            data = bytecpy(ref c_board[pID].buffer1, Offsets.Block1.OriginZ, Offsets.Block1.OriginZ + 3);
            Array.Reverse(data, 0, 4);

            return BitConverter.ToSingle(data, 0);
        }

        private string GetPrimaryName()
        {
            return PS3_REMOTE.Extension.ReadString(0x1bbbc2c);
        }
        /// <summary>
        /// Verify if client is running legal model id.
        /// </summary>
        /// <param name="pID">Client ID (Server side)</param>
        private bool GetClientInvisibleStatus(int pID)
        {
            if (c_board[pID] == null) return false;
            if ((c_board[pID].buffer0[Offsets.Block0.Model] == 0x00) && (c_board[pID].buffer0[Offsets.Block0.Model+1] == 0x00)) return true;
            return false;
        }

        /// <summary>
        /// Check if client is running with GodMode class
        /// </summary>
        /// <param name="pID">Client ID (Server side)</param>
        private bool clienthavegodmodeclass(int pID)
        {
            //0x110a4fb --> 616
            //0x110a503 --> 624
            //0x110a4ff --> 620

            if (c_board[pID] == null) return false;
            if (c_board[pID].buffer1[616] != 0x00 && c_board[pID].buffer1[624] == 0x0 && c_board[pID].buffer1[620] == 0x0) return true;
            return false;
        }

        /// <summary>
        /// Get client team (0; Team A -- 1; Team B)
        /// </summary>
        /// <param name="pID">Client ID (Server side)</param>
        private int GetClientTeam(int pID)
        {
            if (c_board[pID] == null) return -1;
            switch (c_board[pID].buffer1[Offsets.Block1.Team])
            {
                case 0x01:
                    return 0;
                case 0x02:
                    return 1;
                case 0x07: //Spectator
                    return 2; 
                case 0x05: //Godmode trick
                    return 3;
                default:
                    return -1;
            }

        }

        /// <summary>
        /// DIsplay warning on client screen about this protection.
        /// </summary>
        /// <param name="pID">Client ID (Server side)</param>
        public void warnClient(int pID, string lang)
        {
            switch (lang)
            {
                case "en":
                    MW3_REMOTE.iPrintln(pID, "^1Warning: ^2Server ^7cheat-protected! ^8"+ __version__);
                    break;
                case "fr":
                    MW3_REMOTE.iPrintln(pID, "^1Attention: ^2Serveur ^8avec ^7protection contre la ^2triche! ^8" + __version__);
                    break;
                case "es":
                    MW3_REMOTE.iPrintln(pID, "^7Queda prohibido de ^2utilizar ^7'mods'^0, y/o ^7'hacks' ! ^8" + __version__);
                    break;
                case "ge":
                    MW3_REMOTE.iPrintln(pID, "^1Aktiver Schutz gegen ^5betrugen, ^7Vorsicht! ^8" + __version__);
                    break;
            }

        }

        /// <summary>
        /// Get server's header from PS3 RAM.
        /// </summary>
        /// <param name="pID">Client ID (Server side)</param>
        private void getHeaders()
        {
            headers = PS3_REMOTE.GetBytes(0x8360d5, 0x100);
        }

        /// <summary>
        /// Get infos from server header
        /// </summary>
        /// <param name="pID">Client ID (Server side)</param>
        private string ReturnInfos(int Index)
        {
            string data = Encoding.ASCII.GetString(headers);

            data = data.Replace(@"\", "|");
            int k = data.Split(new char[] { '|' }).Length;
            if (k >= Index) return data.Split(new char[] { '|' })[Index];
            
            return "nullObject";
        }
    }
}
