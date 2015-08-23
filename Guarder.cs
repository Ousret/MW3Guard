using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Diagnostics;
using PS3Lib;
using System.Media;

namespace PS3API_Demo
{
    class Guarder
    {

        private const string __version__ = "v0.9.3";

        private PS3API PS3_REMOTE;
        private RPC MW3_REMOTE;

        public class client_data
        {
            public string client_name = "";

            public int n_prestige = 0;
            public int n_level = 0;

            public int score = 0;
            public int kills = 0;
            public int deaths = 0;

            public int c_health = 0;

            public int c_primmary_ammo = 0;
            public int c_secondary_ammo = 0;

            public string c_primary_weapon;

            public int c_team = 0;
            public int warn_nb = 0;

            public int report = 0;

            public float xp, yp, zp, x0, y0, z0;
            public int nbsec_camp;
            public int nb_countcamp = 0;

            /* Prototype de mesure d'excelence ! */
            public float barycentre_x = 0, barycentre_y = 0, barycentre_z = 0;
            public double last_distance = 0;

            public uint rapprochements = 0, eloignements = 0;
            public float probaSuccess;

            /* Prototype CustomKillstreak! */
            public int lastKillStreak = 0, currentKillStreak = 0;
        }

        public volatile client_data[] c_board = new client_data[18];
        public volatile bool thread_stop = true;

        public volatile string current_host;
        public volatile string current_maps;
        public volatile string current_gamemode;
        public volatile string current_maxplayer = "18";

        private int maxSlots = 0;

        public volatile int nbClient = 0;
        private bool _botEnable = false;
        public volatile int __voteKick = -1;
        public volatile int __voteReason = -1;

        public const int _allow_nbcamp = 0;
        private const float rayon = 800.0F;

        private const uint __PLAGE0__ = 0x00FCA3E8;
        private const uint __BLOCK0__ = 0x280;
        private const uint __PLAGE1__ = 0x0110A293;
        private const uint __BLOCK1__ = 0x3980;

        protected byte[] buffer0 = new byte[__BLOCK0__], buffer1 = new byte[__BLOCK1__];

        private class Offsets {

            const uint Wallhack = 0x00173B62;

            private class Block0
            {
                const uint Model = 0;
                const uint Health = 54;
            }

            private class Block1
            {
                const uint Redbox = 0;
                const uint OriginX = 9, OriginY = 13, OriginZ = 17;
                const uint Vision = 868;
                const uint PrimaryAmmo = 1048;
                const uint Score = 13061, Kills = 13069, Deaths = 13065;
                const uint Team = 13252;
                const uint Name = 13313;
                const uint UAV = 13480;
                const uint Clip = 13804;
            }
        }

        System.IO.StreamWriter _debug = new System.IO.StreamWriter("output.txt");
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

        public void GuardBot()
        {
            int i = 0, j = 0, nbClient_T = 0, deathstmp = 0, killstmp = 0;
            bool redboxEnabled = false;
            uint nbPoints = 0;
            double tmpDistance = 0;

            while (!thread_stop)
            {
                _botEnable = setGuardState();

                if (_botEnable)
                {
                    nbClient_T = 0;

                    /* Mesure de temps necessaire à l'analyse de n joueur(s) */
                    //_bench.Start();

                    /* Mettre à jour la liste des joueurs */
                    for (i = 0; i < maxSlots; i++)
                    {
                        if (c_board[i] == null) c_board[i] = new client_data();
                        /* Download buffer from PS3 Host */
                        buffer0 = PS3_REMOTE.GetBytes((uint)(__PLAGE0__ + (i * __BLOCK0__)), 54);
                        buffer1 = PS3_REMOTE.GetBytes((uint)(__PLAGE1__ + (i * __BLOCK1__)), 13804);

                        c_board[i].client_name = GetClientName(i);

                        //Le slot est actuelement occupé
                        if (!String.IsNullOrEmpty(c_board[i].client_name))
                        {
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
                            }

                            c_board[i].kills = killstmp;
                            c_board[i].deaths = deathstmp;

                            //c_board[i].c_primmary_ammo = getClientPrimmaryAmmoAmount(i);
                            c_board[i].c_team = GetClientTeam(i);
                            //c_board[i].c_health = GetClientHealth(i);
                            
                            redboxEnabled = clientHaveRedBox(i);

                            _bench.Start();
                            c_board[i].xp = getClientCoordinateX(i);
                            c_board[i].yp = getClientCoordinateY(i);
                            c_board[i].zp = getClientCoordinateZ(i);
                            _bench.Stop();

                            _debug.WriteLine("GetOrigin for Client " + i + " === " + _bench.Elapsed);
                            _bench.Reset();

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

                                    if (c_board[i].nbsec_camp >= 4 && c_board[i].nbsec_camp < 6)
                                    {
                                        setClientAlertCamp(i, "en", 1);
                                    }
                                    else if (c_board[i].nbsec_camp >= 6 && c_board[i].nbsec_camp < 9)
                                    {
                                        setClientAlertCamp(i, "fr", 1);
                                    }
                                    else if (c_board[i].nbsec_camp >= 10)
                                    {
                                        // On tue le clientN
                                        c_board[i].nb_countcamp++;

                                        if (c_board[i].nbsec_camp > _allow_nbcamp)
                                        {
                                            MW3_REMOTE.SV_KickClient(i, "has been ^4kicked for ^7camping too long");
                                        }
                                        else
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

                                /* Détection des cas évidents */
                                if (haveClientGodMode(i) && c_board[i].deaths <= 3)
                                {
                                    MW3_REMOTE.SV_KickClient(i, "has been ^1kicked ^0for ^2cheating. ^7(Reason 1)");
                                    SetHostWarning(c_board[i].client_name + ": kick for godmode");
                                }
                                else if (haveClientUFOMode(i))
                                {
                                    MW3_REMOTE.SV_KickClient(i, "has been ^1kicked ^0for ^2cheating. ^7(Reason 2)");
                                    SetHostWarning(c_board[i].client_name + ": kick for ufomode");
                                }
                                else if (haveClientUnlimitedAmmo(i))
                                {
                                    MW3_REMOTE.SV_KickClient(i, "has been ^1kicked ^0for ^2cheating. ^7(Reason 3)");
                                    SetHostWarning(c_board[i].client_name + ": kick for unlimited ammo");
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
                                else if (c_board[i].kills < 3 && clientHaveRedBox(i))
                                {
                                    MW3_REMOTE.SV_KickClient(i, "has been ^1kicked ^0for ^2cheating. ^7(Reason 7)");
                                    SetHostWarning(c_board[i].client_name + ": kick for killstreak hack");
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
                                else
                                {
                                    nbClient_T++;
                                }
                            }
                            
                        }
                        else
                        {
                            c_board[i].score = 0;
                            c_board[i].kills = 0;
                            c_board[i].deaths = 0;
                            c_board[i].c_health = 0;
                            c_board[i].c_primmary_ammo = 0;
                            c_board[i].c_team = 0;

                            if (__voteKick == i)
                            {
                                __voteKick = -1;
                                PS3_REMOTE.CCAPI.Notify(CCAPI.NotifyIcon.CAUTION, "Client "+i+": Already gone");
                            }

                        }
                        redboxEnabled = false;
                    }
                    nbClient = nbClient_T;
                    //_bench.Stop();

                    //_debug.WriteLine("Benchmark Sample: (" + nbClient + " Client(s)) = " + _bench.Elapsed);

                    //_bench.Reset();

                    /* Probabilité et mesure de réussite */
                    for (i = 0; i < maxSlots; i++)
                    {
                        if (!String.IsNullOrEmpty(c_board[i].client_name))
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

                            /* Calcul new barrycentre */
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
                                c_board[i].probaSuccess = -1;
                            }
                            else
                            {
                                c_board[i].probaSuccess = 0;
                            }

                            //_debug.WriteLine("Client " + i + " (" + c_board[i].client_name + "): TargetSucces = " + c_board[i].probaSuccess + "%"+" --R: "+ c_board[i].rapprochements+" --E: "+ c_board[i].eloignements);
                        }
                    }

                }
                else
                {


                    //Test de demande de 0x3980 bytes sur une demande..! 
                   /* _bench.Start();
                    byte[] data = PS3_REMOTE.GetBytes((uint)0x0110A293, 0x3980);
                    _bench.Stop();

                    _debug.WriteLine("Benchmark for 0x3980 bytes === " + _bench.Elapsed);

                    _bench.Reset();

                    for (int l = 0; l < 0x3980; l++)
                    {
                        _debug.WriteLine("Offeset " + l + ": " + data[l]);
                    }*/

                    PS3_REMOTE.CCAPI.Notify(CCAPI.NotifyIcon.CAUTION, "MW3Guard is sleeping..");
                    Array.Clear(c_board, 0, maxSlots);
                    nbClient = 0;
                    
                    Thread.Sleep(1000 * 30);
                }

                Thread.Sleep(100);

            }
        }

        /* LightQuake Announce for killstreak */
        private void localAnnounce(int pID)
        {
            if (c_board[pID].currentKillStreak == c_board[pID].lastKillStreak) return;

            switch (c_board[pID].currentKillStreak)
            {
                case 3: //Triple kill
                    MW3_REMOTE.iPrintlnBold(-1, c_board[pID].client_name+" starting with ^3triple-kill!");
                    MW3_REMOTE.PlaySound(-1, "mp_bonus_start");
                    announceSound = new SoundPlayer(@"QuakeSounds\triplekill.wav");
                    announceSound.Play();
                    break;
                case 5:
                    MW3_REMOTE.iPrintlnBold(-1, c_board[pID].client_name +" comes to ^4multi-kill!");
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
                    MW3_REMOTE.iPrintlnBold(-1, c_board[pID].client_name + " is ^7dominating!");
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
                    MW3_REMOTE.iPrintlnBold(-1, c_board[pID].client_name + " have made a ^4pact with ^7Lucifer!");
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
                    MW3_REMOTE.PlaySound(-1, "mp_bonus_end");
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
                            if (c_board[i].xp == c_board[j].xp && c_board[i].yp == c_board[j].yp && c_board[i].zp == c_board[j].zp)
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
            byte[] data = new byte[1] { 0x00 };
            data = PS3_REMOTE.GetBytes((uint)(0x0110d87f + (0x3980 * pID)), 1);

            if (data[0] == 0x07) return true;
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
            byte[] data = new byte[2] { 0x00, 0x00 };
            data = PS3_REMOTE.GetBytes((uint)(0x0110A773 + (0x3980 * pID)), 2);
            if ((data[0] == 0xC5) && (data[1] == 0xFF)) return true;
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

            if (MW3_REMOTE.isServerEnabled())
            {
                string c_host = getHostName();
                current_host = c_host;
                if (c_host != "[3003]Hallogen6to66") return false;
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
            }
            current_maps = "";
            return false;
        }

        private void SetHostWarning(string text)
        {
            PS3_REMOTE.CCAPI.Notify(CCAPI.NotifyIcon.CAUTION, text);
        }
        
        private string GetClientName(int pID)
        {
            return PS3_REMOTE.Extension.ReadString((uint)(0x110d694 + (0x3980 * pID)));
        }

        private int GetClientHealth(int pID)
        {
            byte[] data = new byte[4] { 0x00, 0x00, 0x00, 0x00 };
            data = PS3_REMOTE.GetBytes((uint)(0x0FCA41E + (0x280 * pID)), 4);
            
            return BitConverter.ToInt32(data, 0);
        }

        private int GetClientScore(int pID)
        {
            return PS3_REMOTE.Extension.ReadInt32((uint)(0x0110d598 + (0x3980 * pID)));
        }

        private int GetClientKills(int pID)
        {
            return PS3_REMOTE.Extension.ReadInt32((uint)(0x0110d5a0 + (0x3980 * pID)));
        }
        
        private int GetClientDeaths(int pID)
        {
            return PS3_REMOTE.Extension.ReadInt32((uint)(0x0110d59c + (0x3980 * pID)));
        }

        public int getClientPrimmaryAmmoAmount(int pID)
        {
            byte[] ammo = new byte[1];
            ammo = PS3_REMOTE.GetBytes((uint)(0x0110a6ab + (0x3980 * pID)), 1);

            return ammo[0];
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
            byte[] data = new byte[3] { 0x00, 0x00, 0x00 };
            data = PS3_REMOTE.GetBytes((uint)(0x0FCA41E + (0x280 * pID)), 3);

            if (data[0] >= 0xFE && data[1] >= 0xFE && data[0] >= 0xFE) return true;
            return false;
        }
        
        private bool getClientUAVStatus(int pID)
        {
            byte[] data = new byte[1];
            data = PS3_REMOTE.GetBytes((uint)(0x0110D73B + (0x3980 * pID)), 1);

            if (data[0] == 0x01) return true;
            return false;
        }

        private bool haveClientUnlimitedAmmo(int pID)
        {
            if (getClientPrimmaryAmmoAmount(pID) > 0x64) return true;
            return false;
        }

        private bool haveClientUFOMode(int pID)
        {
            byte[] data = new byte[1];
            data = PS3_REMOTE.GetBytes((uint)(0x0110D87F + (0x3980 * pID)), 1);

            if (data[0] == 0x02) return true;
            return false;
        }

        private bool haveClientWallhack(int pID)
        {
            byte[] data = new byte[2] { 0x02, 0x81 };

            data = PS3_REMOTE.GetBytes((uint)(0x00173b62 + (0x3980 * pID)), 2);
            if (data[0] == 0x01 && data[1] == 0x2c) return true;
            return false;
        }

        //Night vision + shoot hand grenade detection
        private bool clientHaveNightVision(int pID)
        {
            byte[] data = new byte[1] { 0x00 };
            data = PS3_REMOTE.GetBytes((uint)(0x0110a5f7 + (0x3980 * pID)), 1);
            if (data[0] == 0xFF || data[0] == 0x40 || data[0] == 0x0A) return true;
            return false;
        }

        private bool clientHaveRedBox(int pID)
        {
            byte[] data = new byte[1] { 0x00 };
            data = PS3_REMOTE.GetBytes((uint)(0x0110a293 + (0x3980 * pID)), 1);
            if (data[0] == 0x10) return true;
            return false;
        }

        /*private void setClientRedBox(int pID)
        {
            PS3_REMOTE.SetMemory((uint)(0x0110a293 + (0x3980 * pID)), new byte[] { 0x10 });
        }*/

        private float getClientCoordinateX(int pID)
        {
            float[] dataX = MW3_REMOTE.ReadSingle((uint)(0x0110A29C + (0x3980 * pID)), 1);
            return dataX[0];
        }

        private float getClientCoordinateY(int pID)
        {
            float[] dataY = MW3_REMOTE.ReadSingle((uint)(0x0110A2A0 + (0x3980 * pID)), 1);
            return dataY[0];
        }

        private float getClientCoordinateZ(int pID)
        {
            float[] dataZ = MW3_REMOTE.ReadSingle((uint)(0x0110A2A4 + (0x3980 * pID)), 1);
            return dataZ[0];
        }

        private string GetPrimaryName()
        {
            return PS3_REMOTE.Extension.ReadString((uint) 0x1BBBC2C);
        }

        private bool GetClientInvisibleStatus(int pID)
        {
            //byte[] authstd = new byte[16] { 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01 };
            //Test0: 0x0110976C -- Test1: 0x0110D60C -- Test2: 0x0FCA41E -- Test3: 0x01C19865
            //Model change: 0x00fca3e8
            byte[] data = new byte[2];
            data = PS3_REMOTE.GetBytes((uint)(0x00fca3e8 + (0x280 * pID)), 2);

            //_debug.WriteLine("Client " + pID + ": Models: [" + data[0] + "; " + data[1] + "]");

            if ((data[0] == 0x00) && (data[1] == 0x00)) return true;
            return false;
            
        }
        //Trying to find element that proof "he" using secondary godmode
        private void AnalysisSecondaryGMode(int pID)
        {
            byte[] data = new byte[64];
            int i = 0;
            data = PS3_REMOTE.GetBytes((uint)(0x00fca3e8 + (0x280 * pID)), 64);

            string loganalysis = "Header for Client "+pID+" ("+c_board[pID].client_name+") === ";

            for (i = 0; i < 64; i++)
            {
                loganalysis += (data[i] + "; ");
            }

            _debug.WriteLine(loganalysis);
        }

        private int GetClientTeam(int pID)
        {
            byte[] data = new byte[1] { 0x00 };
            data = PS3_REMOTE.GetBytes((uint)(0x0110d657 + (0x3980 * pID)), 1);

            switch (data[0])
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

        private void SetClientTeam(uint pID, byte team)
        {
            if (team != 0x01 || team != 0x02) return;
            if (pID > 17) return;
            PS3_REMOTE.SetMemory((uint)(0x0110d657 + (0x3980 * pID)), new byte[] { team });
        }

        public void warnClient(int pID, string lang)
        {
            switch (lang)
            {
                case "en":
                    MW3_REMOTE.iPrintln(pID, "^1Warning: ^2Server ^7cheat-protected! ^8"+ __version__);
                    break;
                case "fr":
                    MW3_REMOTE.iPrintln(pID, "^1Attention: ^2Serveur avec ^7protection contre la ^2triche! ^8" + __version__);
                    break;
                case "es":
                    MW3_REMOTE.iPrintln(pID, "^7Queda prohibido de ^2utilizar ^7'mods'^0, y/o ^7'hacks' ! ^8" + __version__);
                    break;
                case "ge":
                    MW3_REMOTE.iPrintln(pID, "^1Aktiver Schutz gegen ^5betrugen, ^7Vorsicht! ^8" + __version__);
                    break;
            }

        }

        private string ReturnInfos(int Index)
        {
            string data = Encoding.ASCII.GetString(PS3_REMOTE.GetBytes(0x8360d5, 0x100));

            data = data.Replace(@"\", "|");
            int k = data.Split(new char[] { '|' }).Length;
            if (k >= Index) return data.Split(new char[] { '|' })[Index];
            
            return "nullObject";
        }

        private void DumpInfos()
        {
            string data = Encoding.ASCII.GetString(PS3_REMOTE.GetBytes(0x8360d5, 0x100));
            data = data.Replace(@"\", "|");

            //_debug.WriteLine("Infos: "+data);
        }
    }
}
