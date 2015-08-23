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

/*
    Nouvelles idées:

    * Réequilibrage des équipes en cas de ragequit massif. (60%)
        -> Si aucun spectateurs (En train de joindre..)
        -> Si difference equipe sup. à 3..
        -> ReSpawn dans l'équipe adverse sur coordonnée joueur opposé
        -> Ne pas changer d'équipe au écran scindé pour rester cohérent..
        -> Soucis de réarangement des couleurs..
    * Anti-kill spawn
        -> Prendre échantillon à n moments t (5 echantillons max)
        -> Si ennemis dans périmetre de sécurité (Changement parmis les 5 échantillons)
        -> Uniquement si ne possede pas de réinsertion tactique 0x4A = Tactical Insertion
    * Détection de comportement suspect (radar hack, redbox..)
        -> Ratio R/E élevé
        -> Vise 3, 4 sec avant.
        -> Nb eliminations
        -> Etat du radar (Inactif)

*/

namespace PS3API_Demo
{
    class Guarder
    {
        private const string __version__ = "v1.1.0";

        private PS3API PS3_REMOTE;
        private RPC MW3_REMOTE;

        private const uint __PLAGE0__ = 0x00FCA3E8, __BLOCK0__ = 0x280;
        private const uint __PLAGE1__ = 0x0110A293, __BLOCK1__ = 0x3980;
        //0x01BBFE3C
        public class client_data
        {

            public byte[] buffer0 = new byte[__BLOCK0__], buffer1 = new byte[__BLOCK1__];

            public string client_name = "";

            public int n_prestige = 0;
            public int n_level = 0;

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
            public uint c_save = 0; //Max x10
        }

        public volatile client_data[] c_board = new client_data[18];
        public volatile bool thread_stop = true;

        public volatile string current_host;
        public volatile string current_maps;
        public volatile string current_gamemode;
        public volatile string current_maxplayer = "18";

        public volatile int maxSlots = 0;

        public volatile int nbClient = 0;
        private bool _botEnable = false;

        public volatile int __voteKick = -1;
        public volatile int __voteReason = -1;

        public const int _allow_nbcamp = 0;
        private const float rayon = 750.0F;

        private const uint _MAX_TEAM_DIFF = 2;
        private const uint _MAX_SAVE_ORIGIN = 10;

        private byte[] headers = new byte[0x100];

        public class Offsets {

            public const uint Wallhack = 0x00173B62;

            public class Block0
            {
                public const uint Model = 0;
                public const uint Health = 54;
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
                public const uint UAV = 13480; //Fake..
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

        public void RPCEnable_124()
        {
            MW3_REMOTE.Enable();
        }

        public void ForceHost_124()
        {

            //MW3_REMOTE.setIntDvar((uint)0x18CF42C, 1);

            /*
            party_minplayers 1 [OK]
            lobby_partySearchWaitTime 0
            party_gameStartTimerLength 1 [OK]
            party_pregameStartTimerLength 12
            party_vetoDelayTime 1
            party_maxTeamDiff 1000 [OK]
            party_minLobbyTime 0 [OK]
            pt_backoutOnClientPresence 1
            partymigrate_timeout 1
            pt_pregameStartTimerLength 1
            pt_gameStartTimerLength 20 
            */

            MW3_REMOTE.lockIntDvarToValue(0x8AEE34, 0x1); //party_minplayers
            MW3_REMOTE.lockIntDvarToValue(0x8AEDC0, 0x1); //pt_gameStartTimerLength
            MW3_REMOTE.lockIntDvarToValue(0x8AEDC8, 0x1); //pt_pregameStartTimerLength
            MW3_REMOTE.lockIntDvarToValue(0x8AEED8, 0x1); //partymigrate_timeout
            MW3_REMOTE.lockIntDvarToValue(0x8AEE88, 0xA2); //party_maxTeamDiff

            PS3_REMOTE.Extension.WriteUInt32(0x428a40, 0x4081001c);
            PS3_REMOTE.Extension.WriteUInt32(0x428a44, 0x48000018);
            PS3_REMOTE.Extension.WriteUInt32(0x428a4c, 0x40810010);
            PS3_REMOTE.Extension.WriteUInt32(0x428a54, 0x40810008);
            PS3_REMOTE.Extension.WriteUInt32(0x428a58, 0x48000005);

            /*uint addrstart = 0x8aec34, addrptr = 0;
            uint i = 0, crow = 0;
            //MW3_REMOTE.lockIntDvarToValue(0x8AEE34, 0x1); //+27 PreLobby Timer (Old: 18CF42C)

            for (i = 0; i < 1000; i+=4)
            {
                crow = i / 4;
                addrptr = i + addrstart;
                
                _debug.WriteLine("Row "+ String.Format("{0:X}", addrptr) + " :" + BitConverter.ToString(PS3_REMOTE.GetBytes(addrptr, 4)));
            }
           
            _debug.Close();*/
            //maxPlayer = 0x8aee40
            //timerLobby = 0x8aee64
            //MW3_REMOTE.lockIntDvarToValue(0x8aee64, 0x1);

            //MW3_REMOTE.lockIntDvarToValue(0x8376C018, 0x3);
            //MW3_REMOTE.lockIntDvarToValue(0x8376C018, 0x3);

            //MW3_REMOTE.lockIntDvarToValue((0x8AEE34)+27, 0xF);

            /*byte[] sv_dump = PS3_REMOTE.GetBytes(0x8AEE34, 128);
            _debug.WriteLineAsync("---Dump SV_Memory---");

            _debug.WriteLineAsync(BitConverter.ToString(sv_dump));
            _debug.WriteLineAsync(Encoding.UTF8.GetString(sv_dump));

            for (int i = 0; i < 96; i++)
            {
                _debug.WriteLineAsync("Byte " + i + ": "+sv_dump[i]);
            }*/
            /**/
            //MW3_REMOTE.CBuf_AddText
        }

        public bool isValidName(string strToCheck)
        {
            Regex rg = new Regex(@"^[a-zA-Z0-9\-_()s,]*$");
            return rg.IsMatch(strToCheck);
        }

        /*private void ForceHosting()
        {
            PS3_REMOTE.Extension.WriteUInt32(0x71d0d0, 1);
            byte[] buffer = new byte[] {
                    60, 0x60, 0, 0x72, 0x80, 0x63, 0xd0, 0xd0, 0x2c, 3, 0, 0, 0x41, 130, 0, 0xb8,
                    0x2c, 3, 0, 1, 0x41, 130, 0, 4, 60, 0x60, 1, 0x8d, 0x38, 0x80, 0, 1,
                    0x90, 0x83, 250, 80, 60, 0x60, 1, 0x8d, 0x38, 0x80, 0, 5, 0x90, 0x83, 250, 0x94,
                    60, 0x60, 1, 0x8d, 0x38, 0x80, 0, 1, 0x90, 0x83, 0x47, 20, 60, 0x60, 1, 0x8d,
                    0x38, 0x80, 0, 0, 0x90, 0x83, 0x41, 0xc4, 60, 0x60, 1, 0x8d, 0x38, 0x80, 0, 0,
                    0x90, 0x83, 0x42, 8, 60, 0x60, 1, 0x8d, 0x38, 0x80, 0, 10, 0x90, 0x83, 0xee, 160,
                    60, 0x60, 1, 0x8d, 0x38, 0x80, 0, 10, 0x90, 0x83, 0xee, 0xe4, 60, 0x60, 1, 0x8d,
                    0x38, 0x80, 0, 1, 0x90, 0x83, 0xef, 40, 60, 0x60, 1, 0x8d, 0x38, 0x80, 0, 0,
                    0x90, 0x83, 0xef, 0x6c, 60, 0x60, 1, 0x8d, 0x38, 0x80, 0, 0, 0x90, 0x83, 240, 0x38,
                    60, 0x60, 1, 0x8d, 0x38, 0x80, 0, 0, 0x90, 0x83, 240, 0x38, 60, 0x60, 1, 0x8d,
                    0x38, 0x80, 0, 0, 0x90, 0x83, 240, 0xc0, 60, 0x60, 1, 0x8d, 0x38, 0x80, 0, 1,
                    0x90, 0x83, 0xf4, 120, 60, 0x60, 1, 0x8d, 0x38, 0x80, 0, 12, 0x90, 0x83, 0xf4, 0xbc,
                    0x48, 0, 0, 0xac, 60, 0x60, 1, 0x8d, 0x38, 0x80, 0, 1, 0x90, 0x83, 250, 80,
                    60, 0x60, 1, 0x8d, 0x38, 0x80, 0, 60, 0x90, 0x83, 250, 0x94, 60, 0x60, 1, 0x8d,
                    60, 0x80, 0x42, 0x20, 0x90, 0x83, 0x47, 20, 60, 0x60, 1, 0x8d, 60, 0x80, 0x42, 0x20,
                    0x90, 0x83, 0x41, 0xc4, 60, 0x60, 1, 0x8d, 0x38, 0x80, 0, 10, 0x90, 0x83, 0x42, 8,
                    60, 0x60, 1, 0x8d, 0x38, 0x80, 0, 8, 0x90, 0x83, 0xee, 160, 60, 0x60, 1, 0x8d,
                    0x38, 0x80, 0, 5, 0x90, 0x83, 0xee, 0xe4, 60, 0x60, 1, 0x8d, 0x38, 0x80, 0xaf, 200,
                    0x90, 0x83, 0xef, 40, 60, 0x60, 1, 0x8d, 0x38, 0x80, 11, 0xb8, 0x90, 0x83, 0xef, 0x6c,
                    60, 0x60, 1, 0x8d, 0x38, 0x80, 0, 5, 0x90, 0x83, 240, 0x38, 60, 0x60, 1, 0x8d,
                    0x38, 0x80, 0, 0x16, 0x90, 0x83, 240, 0x38, 60, 0x60, 1, 0x8d, 0x38, 0x80, 3, 0xe8,
                    0x90, 0x83, 240, 0xc0, 60, 0x60, 1, 0x8d, 0x38, 0x80, 0, 8, 0x90, 0x83, 0xf4, 120,
                    60, 0x60, 1, 0x8d, 0x38, 0x80, 0, 0x12, 0x90, 0x83, 0xf4, 0xbc, 0x4e, 0x80, 0, 0x20
                 };
            byte[] buffer2 = new byte[] { 0x48, 0x42, 0xec, 0x54 };
            PS3_REMOTE.SetMemory(0x2ee48c, buffer2);
            PS3_REMOTE.SetMemory(0x71d0e0, buffer);
        }*/

        private bool saveClientOrigin(int pID)
        {
            if (c_board[pID] == null || c_board[pID].c_save >= _MAX_SAVE_ORIGIN) return false;
            c_board[pID].s_originX[c_board[pID].c_save] = c_board[pID].xp;
            c_board[pID].s_originY[c_board[pID].c_save] = c_board[pID].yp;
            c_board[pID].s_originZ[c_board[pID].c_save] = c_board[pID].zp;
            c_board[pID].c_save++;
            return true;
        }

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

        private int oppositeTeam(int pID)
        {
            if (c_board[pID] == null) return -1;
            if (c_board[pID].c_team > 1) return -1;

            return (c_board[pID].c_team) == 0 ? 1 : 0; 
        }

        private bool clientRiskSpawnkill(int pID)
        {
            if (nearestEnnemie(pID) <= rayon) return true;
            return false;
        }

        private bool clientSpawnkillProtectionActive(int pID)
        {
            if (c_board[pID] == null || c_board[pID].buffer1[Offsets.Block1.Tactical] == 0x4A) return false;
            return true;
        }

        /* TESTS ONLY: Prevent mw3 remote ending "by host", we may went to disable sv_matchend ingame */
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
        private int mostSecureOrigin(int pID)
        {
            if (c_board[pID] == null) return -1;
            int i = 0, j = 0, mostSec = -1;
            double minDist = nearestEnnemie(pID), tDist = 0;
            int tOpposite = oppositeTeam(pID);

            for (i = 0; i < c_board[pID].c_save; i++)
            {
                for (j = 0; j < maxSlots; j++)
                {
                    if (pID != j && c_board[j] != null && !String.IsNullOrEmpty(c_board[j].client_name) && c_board[j].c_team == tOpposite)
                    {
                        tDist = distancePoints(c_board[j].xp, c_board[j].yp, c_board[j].zp, c_board[pID].s_originX[i], c_board[pID].s_originY[i], c_board[pID].s_originZ[i]);
                        if (minDist > tDist)
                        {
                            minDist = tDist;
                            mostSec = j;
                        }
                    }
                }
            }

            return mostSec;
        }

        private bool ClientTeleportSaveOrigin(int pID, int saveLoc)
        {
            if (c_board[pID] == null || saveLoc == -1 || saveLoc > 9) return false;
            PS3_REMOTE.Extension.WriteFloat((uint)(0x0110A29C + (0x3980 * pID)), c_board[pID].s_originX[saveLoc]);
            PS3_REMOTE.Extension.WriteFloat((uint)(0x0110A2A0 + (0x3980 * pID)), c_board[pID].s_originY[saveLoc]);
            PS3_REMOTE.Extension.WriteFloat((uint)(0x0110A2A4 + (0x3980 * pID)), c_board[pID].s_originZ[saveLoc]);

            MW3_REMOTE.iPrintln(pID, "^4Server: ^7Protection against ^3spawnkill enabled!");
            return true;
        }

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

        public void GuardBot()
        {
            int i = 0, j = 0, nbClient_T = 0, deathstmp = 0, killstmp = 0;
            bool redboxEnabled = false;
            uint nbPoints = 0;
            double tmpDistance = 0;
            string client_name_t = "";

            _debug = new System.IO.StreamWriter("MW3Guard-" + DateTime.Now.ToString("MM-dd-yyyy-h-mm") + ".log");
            
            /* Corrupt sv_matchend: face the buffer overflow with magic paquet */
            swap_sv_me_m(true);

            while (!thread_stop)
            {
                
                _botEnable = setGuardState();

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

                        //Le slot est actuelement occupé
                        if (!String.IsNullOrEmpty(client_name_t))
                        {
                            //Test for valid name
                            if (!isValidName(client_name_t))
                            {
                                MW3_REMOTE.SV_KickClient(i, "was kicked for ^5illegal name (Reason 12)");
                                PS3_REMOTE.CCAPI.Notify(CCAPI.NotifyIcon.CAUTION, client_name_t + ": kick for illegal name");
                                goto st_kicked;
                            }

                            //Test for guy who change intend to change name ingame
                            if (!String.IsNullOrEmpty(c_board[i].client_name))
                            {
                                if (c_board[i].client_name != client_name_t)
                                {
                                    MW3_REMOTE.SV_KickClient(i, "was ^5kicked for ^4cheating (Reason 11)");
                                    PS3_REMOTE.CCAPI.Notify(CCAPI.NotifyIcon.CAUTION, client_name_t+": kick for name change");
                                    goto st_kicked;
                                }
                            }
                            
                            c_board[i].client_name = client_name_t;

                            /*if (c_board[i].client_name == "Hallogen6to66" && !isInjected)
                            {
                                MW3_REMOTE.CBuf_AddText((uint)i, "set party_minplayers 1");
                                MW3_REMOTE.CBuf_AddText((uint)i, "set party_hostmigration 0");
                                MW3_REMOTE.CBuf_AddText((uint)i, "set bandwidthtest_enable 0");
                                MW3_REMOTE.CBuf_AddText((uint)i, "set bandwidthtest_ingame_enable 0");
                                MW3_REMOTE.CBuf_AddText((uint)i, "set onlinegameandhost 1");
                                MW3_REMOTE.CBuf_AddText((uint)i, "set party_pregamestarttimerlenght -1");
                                MW3_REMOTE.CBuf_AddText((uint)i, "set party_gamestarttimelenght 2");
                                isInjected = true;
                            }*/

                            /* Upd1 buffer0 */
                            c_board[i].buffer0 = PS3_REMOTE.GetBytes((uint)(__PLAGE0__ + (i * __BLOCK0__)), 72);

                            /* Mise à jour des stats */
                            c_board[i].score = GetClientScore(i);
                            
                            killstmp = GetClientKills(i);
                            deathstmp = GetClientDeaths(i);
                            
                            if (deathstmp == c_board[i].deaths)
                            {
                                c_board[i].currentKillStreak += (killstmp - c_board[i].kills);
                            }
                            else
                            {
                                c_board[i].currentKillStreak = 0;
                                /* Just died.. Check if client could generate spawnkill or disturb other client with bad spawn */
                                if (clientSpawnkillProtectionActive(i) && clientRiskSpawnkill(i))
                                {
                                    ClientTeleportSaveOrigin(i, mostSecureOrigin(i)); //Teleport only if we found better origin point.
                                }
                            }

                            c_board[i].kills = killstmp;
                            c_board[i].deaths = deathstmp;

                            c_board[i].c_team = GetClientTeam(i);

                            redboxEnabled = clientHaveRedBox(i);

                            c_board[i].xp = getClientCoordinateX(i);
                            c_board[i].yp = getClientCoordinateY(i);
                            c_board[i].zp = getClientCoordinateZ(i);
                            
                            /* Local addons: Announce BETA */
                            localAnnounce(i);
                            
                            //Check if client is not playing..! (avoid ps3 freeze when calling RPC outside game)
                            if (!isGameFinished() && !isPlayerFreezed(i))
                            {
                                //_debug.WriteLine("Client " + i + ": " + "[ " + c_board[i].xp + "; " + c_board[i].yp + "; " + c_board[i].zp + "]");
                                /* Traitement anti-"campeur" */
                                /* Si le client est dans la sphère */
                                // On doit éviter de vérifier s'il utilise l'AC-130 ou similaire (Predator, drone assault, largage d'osprey, etc. ..)
                                if (!redboxEnabled && isClientProtectedArea(c_board[i].x0, c_board[i].y0, c_board[i].z0, c_board[i].xp, c_board[i].yp, c_board[i].zp))
                                {
                                    c_board[i].nbsec_camp++;

                                    if (c_board[i].nbsec_camp >= 9 && c_board[i].nbsec_camp < 14)
                                    {
                                        setClientAlertCamp(i, "en", 1);
                                    }
                                    else if (c_board[i].nbsec_camp >= 14 && c_board[i].nbsec_camp < 18)
                                    {
                                        setClientAlertCamp(i, "fr", 1);
                                    }
                                    else if (c_board[i].nbsec_camp >= 18)
                                    {
                                        // Nb of times caught to camp..
                                        c_board[i].nb_countcamp++;
                                        // If it's enough..
                                        if (c_board[i].nb_countcamp > _allow_nbcamp)
                                        {
                                            MW3_REMOTE.SV_KickClient(i, "has been ^4kicked for ^7camping too long");
                                            goto st_kicked;
                                        }
                                        else // Punish to the crime..
                                        {
                                            MW3_REMOTE.PlayerDie(i, i);
                                        }

                                        c_board[i].nbsec_camp = 0;
                                    }
                                }
                                else
                                {
                                    // Nouvelle sphère de centre ClientN
                                    c_board[i].x0 = c_board[i].xp;
                                    c_board[i].y0 = c_board[i].yp;
                                    c_board[i].z0 = c_board[i].zp;
                                    c_board[i].nbsec_camp = 0;

                                    /* Save loc */
                                    saveClientOrigin(i);
                                }
                                
                                /* Avertissement multi-langage (EN; FR; ES; GE) */
                                if (c_board[i].warn_nb == 0 && c_board[i].kills >= 1)
                                {
                                    warnClient(i, "fr");
                                    c_board[i].warn_nb++;
                                }
                                else if (c_board[i].warn_nb == 1 && c_board[i].kills >= 1)
                                {
                                    warnClient(i, "en");
                                    c_board[i].warn_nb++;
                                }
                                else if (c_board[i].warn_nb == 2 && c_board[i].kills >= 1)
                                {
                                    warnClient(i, "es");
                                    c_board[i].warn_nb++;
                                }
                                else if (c_board[i].warn_nb == 3 && c_board[i].kills >= 1)
                                {
                                    warnClient(i, "ge");
                                    c_board[i].warn_nb++;
                                }
                                else if (c_board[i].warn_nb == 4 && c_board[i].kills >= 3)
                                {
                                    setClientAlertCamp(i, "en");
                                    c_board[i].warn_nb++;
                                }
                                else if (c_board[i].warn_nb == 5 && c_board[i].kills >= 3)
                                {
                                    setClientAlertCamp(i, "fr");
                                    c_board[i].warn_nb++;
                                }
                                else if (c_board[i].warn_nb == 6 && c_board[i].kills >= 3)
                                {
                                    setClientAlertCamp(i, "es");
                                    c_board[i].warn_nb++;
                                }
                                else if (c_board[i].warn_nb == 7 && c_board[i].kills >= 3)
                                {
                                    setClientAlertCamp(i, "ge");
                                    c_board[i].warn_nb++;
                                }
                                //System.InvalidOperationException
                                //_debug.WriteLineAsync("Client " + i + "; Start detection of obvious cheat or hack..");
                                /* Détection des cas évidents */
                                /*if (haveClientGodMode(i))
                                {
                                    //MW3_REMOTE.SV_KickClient(i, "has been ^1kicked ^0for ^2cheating. ^7(Reason 1)");
                                    SetHostWarning(c_board[i].client_name + ": tested positive for GodMode! ("+ GetClientHealth(i) +")");
                                }
                                else*/ if (haveClientUFOMode(i))
                                {
                                    MW3_REMOTE.SV_KickClient(i, "has been ^1kicked ^0for ^2cheating. ^7(Reason 2)");
                                    SetHostWarning(c_board[i].client_name + ": kick for ufomode");
                                }
                                else if (haveClientUnlimitedAmmo(i))
                                {
                                    MW3_REMOTE.SV_KickClient(i, "has been ^1kicked ^0for ^2cheating. ^7(Reason 3)");
                                    SetHostWarning(c_board[i].client_name + ": kick for unlimited ammo ("+ getClientPrimmaryAmmoAmount(i) +")");
                                }
                                else if (haveClientWallhack(i))
                                {
                                    MW3_REMOTE.SV_KickClient(i, "has been ^1kicked ^0for ^2cheating. ^7(Reason 4)");
                                    SetHostWarning(c_board[i].client_name + ": kick for wallhack");
                                }
                                else if (clientHaveNightVision(i))
                                {
                                    MW3_REMOTE.SV_KickClient(i, "has been ^1kicked ^0for ^2cheating. ^7(Reason 5)");
                                    SetHostWarning(c_board[i].client_name + ": kick for vision hack");
                                }
                                else if ((c_board[i].c_team == 2 || c_board[i].c_team == 3) && c_board[i].kills >= 1)
                                {
                                    MW3_REMOTE.SV_KickClient(i, "has been ^1kicked ^0for ^2cheating. ^7(Reason 6)");
                                    SetHostWarning(c_board[i].client_name + ": kick for team hack");
                                }
                                else if (c_board[i].kills < 3 && redboxEnabled)
                                {
                                    //False positive occurs when someone truly have redbox enabled..
                                    //MW3_REMOTE.SV_KickClient(i, "has been ^1kicked ^0for ^2cheating. ^7(Reason 7)");
                                    SetHostWarning(c_board[i].client_name + ": killstreak hack ??");
                                }
                                else if (haveClientExplosivBullets(i))
                                {
                                    MW3_REMOTE.SV_KickClient(i, "has been ^1kicked ^0for ^2cheating. ^7(Reason 8)");
                                    SetHostWarning(c_board[i].client_name + ": kick for explosiv bullets");
                                }
                                else if (GetClientInvisibleStatus(i) && c_board[i].kills >= 1)
                                {
                                    MW3_REMOTE.SV_KickClient(i, "has been ^1kicked ^0for ^2cheating. ^7(Reason 9)");
                                    SetHostWarning(c_board[i].client_name + ": kick for invisible class");
                                }
                                else if (__voteKick == i)
                                {
                                    //Analysis of client's header
                                    AnalysisSecondaryGMode(i);
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
                                    __voteKick = -1;
                                    __voteReason = -1;
                                }
                            }
                            

                            nbClient_T++;
                            c_board[i].cl_inter++;

                        }
                        else
                        {
                            c_board[i].score = 0;
                            c_board[i].kills = 0;
                            c_board[i].deaths = 0;
                            c_board[i].c_health = 0;
                            c_board[i].c_primmary_ammo = 0;
                            c_board[i].c_team = 0;
                            c_board[i].client_name = String.Empty;
                            c_board[i].cl_inter = 0;
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
                    /* Upd8 actual client counter */
                    nbClient = nbClient_T;

                    /* Probabilité et mesure de réussite */
                    for (i = 0; i < maxSlots; i++)
                    {
                        if (!String.IsNullOrEmpty(c_board[i].client_name) && !isPlayerFreezed(i))
                        {
                            for (j = 0; j < maxSlots; j++)
                            {
                                if (!String.IsNullOrEmpty(c_board[j].client_name) && c_board[j].c_team != c_board[i].c_team && c_board[j].c_team != 2)
                                {
                                    c_board[i].barycentre_x += c_board[j].xp;
                                    c_board[i].barycentre_y += c_board[j].yp;
                                    c_board[i].barycentre_z += c_board[j].zp;
                                    nbPoints++;
                                }
                            }

                            /* Calcul new barrycenter */
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

                    /* Auto-balancing in case of major ragequit.. */
                    //AutoBalancing();

                    if (isGameFinished()) swap_sv_me_m(false);

                }
                else
                {
                    PS3_REMOTE.CCAPI.Notify(CCAPI.NotifyIcon.CAUTION, "MW3Guard is sleeping..");
                    Array.Clear(c_board, 0, maxSlots);
                    MW3_REMOTE.lastsoundreq = "";
                    MW3_REMOTE.lastreq = -1;
                    nbClient = 0;
                    /* We shall wait until next session to be started */
                    while (!MW3_REMOTE.cl_ingame() && !thread_stop) Thread.Sleep(500);
                    swap_sv_me_m(true);
                }

                Thread.Sleep(50);
            }

            _debug.Close();

        }

        /* LightQuake Announce for killstreak */
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

        private bool isGameFinished()
        {
            int i = 0, j = 0, nbMatch = 0, nbTests = 0;
            
            for (i = 0; i < maxSlots; i++)
            {
                if (c_board[i] != null && !string.IsNullOrEmpty(c_board[i].client_name))
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

        private bool isPlayerFreezed(int pID)
        {
            if (c_board[pID] == null) return false;
            if (c_board[pID].buffer1[Offsets.Block1.Clip] == 0x07) return true;
            return false;
        }

        private string getMapName()
        {
            switch (this.ReturnInfos(6))
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

        private double distancePoints(float x0, float y0, float z0, float xp, float yp, float zp)
        {
            double res = Math.Pow((x0 - xp), 2) + Math.Pow((y0 - yp), 2) + Math.Pow((z0 - zp), 2);
            double distance = Math.Sqrt(res);
            return distance;
        }

        private bool isClientProtectedArea(float x0, float y0, float z0, float xp, float yp, float zp)
        {
            if (x0 == 0 && y0 == 0 && z0 == 0) return false;
            double res = Math.Pow((x0 - xp), 2) + Math.Pow((y0 - yp), 2) + Math.Pow((z0 - zp), 2);
            double distance = Math.Sqrt(res);
            
            if (distance <= rayon) return true;
            return false;
        }
        
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

        private bool haveClientExplosivBullets(int pID)
        {
            if (c_board[pID] == null) return false;
            if ((c_board[pID].buffer1[Offsets.Block1.ExplosiveBullet] == 0xC5) && (c_board[pID].buffer1[Offsets.Block1.ExplosiveBullet+1] == 0xFF)) return true;
            return false;
        }

        private string getMaxPlayers()
        {
            return ReturnInfos(18);
        }

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

        private bool setGuardState()
        {

            if (!MW3_REMOTE.cl_ingame()) return false;

            /* Upd8 headers */
            getHeaders();

            string c_host = getHostName();
            current_host = c_host;
            if (c_host != "[3003]Hallogen6to66")
            {
                //ForceHosting(); DONT DO THAT! Create new instance instead!
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
                PS3_REMOTE.CCAPI.Notify(CCAPI.NotifyIcon.INFO, "New instance");
                Array.Clear(c_board, 0, 18);
                current_maps = c_maps;
                return true;
            }

            if (c_maps != "Unknown Map") return true;
            
            current_maps = "";
            return false;
        }

        private void SetHostWarning(string text)
        {
            PS3_REMOTE.CCAPI.Notify(CCAPI.NotifyIcon.CAUTION, text);
        }
        
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

        private int GetClientHealth(int pID)
        {
            if (c_board[pID] == null) return 0;
            return c_board[pID].buffer0[Offsets.Block0.Health];
        }

        private int GetClientScore(int pID)
        {
            if (c_board[pID] == null) return 0;
            byte[] data = new byte[4];
            data = bytecpy(ref c_board[pID].buffer1, Offsets.Block1.Score, Offsets.Block1.Score + 3);
            Array.Reverse(data, 0, 4);

            return BitConverter.ToInt32(data, 0);
        }

        private int GetClientKills(int pID)
        {
            if (c_board[pID] == null) return 0;
            byte[] data = new byte[4];
            data = bytecpy(ref c_board[pID].buffer1, Offsets.Block1.Kills, Offsets.Block1.Kills + 3);
            Array.Reverse(data, 0, 4);
            return BitConverter.ToInt32(data, 0);
        }
        
        private int GetClientDeaths(int pID)
        {
            if (c_board[pID] == null) return 0;
            byte[] data = new byte[4];
            data = bytecpy(ref c_board[pID].buffer1, Offsets.Block1.Deaths, Offsets.Block1.Deaths + 3);
            Array.Reverse(data, 0, 4);
            return BitConverter.ToInt32(data, 0);
        }

        public int getClientPrimmaryAmmoAmount(int pID)
        {
            if (c_board[pID] == null) return 0;
            return c_board[pID].buffer1[Offsets.Block1.PrimaryAmmo];
        }

        private void SetClientName(int pID, string p_name)
        {
            byte[] buffer = new byte[20];
            System.Buffer.BlockCopy(p_name.ToCharArray(), 0, buffer, 0, 20);

            PS3_REMOTE.SetMemory((uint)(0x0110D694 + (0x3980 * pID)), buffer);
        }
        //Amélioration necessaire ici..!
        private bool haveClientGodMode(int pID)
        {
            if (c_board[pID] == null) return false;
            if ((c_board[pID].buffer0[Offsets.Block0.Health] != 0x00 || c_board[pID].buffer0[Offsets.Block0.Health+1] != 0x00) || c_board[pID].buffer0[Offsets.Block0.Health+2] >= 0x70) return true;
            return false;
        }
        /* DO NOT WORK, BAD OFFSET! */
        public bool getClientUAVStatus(int pID)
        {
            if (c_board[pID] == null) return false;
            if (c_board[pID].buffer1[Offsets.Block1.UAV] == 0x01) return true;
            return false;
        }

        private bool haveClientUnlimitedAmmo(int pID)
        {
            if (c_board[pID] == null) return false;
            if (getClientPrimmaryAmmoAmount(pID) > 150) return true;
            return false;
        }

        private bool haveClientUFOMode(int pID)
        {
            if (c_board[pID] == null) return false;
            if (c_board[pID].buffer1[Offsets.Block1.Clip] == 0x02) return true;
            return false;
        }

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
        private bool clientHaveNightVision(int pID)
        {
            if (c_board[pID] == null) return false;
            if (c_board[pID].buffer1[Offsets.Block1.Vision] == 0xFF || c_board[pID].buffer1[Offsets.Block1.Vision] == 0x40 || c_board[pID].buffer1[Offsets.Block1.Vision] == 0x0A) return true;
            return false;
        }

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
            return PS3_REMOTE.Extension.ReadString((uint) 0x1BBBC2C);
        }

        private bool GetClientInvisibleStatus(int pID)
        {
            if (c_board[pID] == null) return false;
            if ((c_board[pID].buffer0[Offsets.Block0.Model] == 0x00) && (c_board[pID].buffer0[Offsets.Block0.Model+1] == 0x00)) return true;
            return false;
        }
        //Trying to find element that proof "he" using secondary godmode
        private void AnalysisSecondaryGMode(int pID)
        {
            if (c_board[pID] == null) return;
            byte[] data = new byte[64];
            data = bytecpy(ref c_board[pID].buffer0, 0, 63);
            int i = 0;

            string loganalysis = "Header for Client "+pID+" ("+c_board[pID].client_name+") === \n";

            for (i = 0; i < 64; i++)
            {
                loganalysis += (data[i] + "; ");
            }

            _debug.WriteLine(loganalysis);
        }

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

        private void getHeaders()
        {
            headers = PS3_REMOTE.GetBytes(0x8360d5, 0x100);
        }

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
