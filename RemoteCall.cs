using PS3Lib;
using System;
using System.Text;
using System.Threading;

public static class RPC
{
    public static uint func_address = 0x277208;
    private static PS3API PS3 = new PS3API(SelectAPI.TargetManager);

    public static int Call(uint address, params object[] parameters)
    {
        int length = parameters.Length;
        int index = 0;
        uint num3 = 0;
        uint num4 = 0;
        uint num5 = 0;
        uint num6 = 0;
        while (index < length)
        {
            if (parameters[index] is int)
            {
                PS3.Extension.WriteInt32(0x10050000 + (num3 * 4), (int)parameters[index]);
                num3++;
            }
            else if (parameters[index] is uint)
            {
                PS3.Extension.WriteUInt32(0x10050000 + (num3 * 4), (uint)parameters[index]);
                num3++;
            }
            else if (parameters[index] is short)
            {
                PS3.Extension.WriteInt16(0x10050000 + (num3 * 4), (short)parameters[index]);
                num3++;
            }
            else if (parameters[index] is ushort)
            {
                PS3.Extension.WriteUInt16(0x10050000 + (num3 * 4), (ushort)parameters[index]);
                num3++;
            }
            else if (parameters[index] is byte)
            {
                PS3.Extension.WriteByte(0x10050000 + (num3 * 4), (byte)parameters[index]);
                num3++;
            }
            else
            {
                uint num7;
                if (parameters[index] is string)
                {
                    num7 = 0x10052000 + (num4 * 0x400);
                    PS3.Extension.WriteString(num7, Convert.ToString(parameters[index]));
                    PS3.Extension.WriteUInt32(0x10050000 + (num3 * 4), num7);
                    num3++;
                    num4++;
                }
                else if (parameters[index] is float)
                {
                    WriteSingle(0x10050024 + (num5 * 4), (float)parameters[index]);
                    num5++;
                }
                else if (parameters[index] is float[])
                {
                    float[] input = (float[])parameters[index];
                    num7 = 0x10051000 + (num6 * 4);
                    WriteSingle(num7, input);
                    PS3.Extension.WriteUInt32(0x10050000 + (num3 * 4), num7);
                    num3++;
                    num6 += (uint)input.Length;
                }
            }
            index++;
        }
        PS3.Extension.WriteUInt32(0x10050048, address);
        Thread.Sleep(20);
        return PS3.Extension.ReadInt32(0x1005004c);
    }

    public static void CBuf_AddText(int client, string command)
    {
        Call(Offsets.Addresses.CBuf_AddText, new object[] { (uint)client, command, 0, 0, 0 });
    }

    public static void Cmd_ExecuteSingleCommand(uint client, string command)
    {
        Call(Offsets.Addresses.Cmd_ExecuteSingleCommand, new object[] { client, command, 0, 0, 0 });
    }

    public static void DestroyAll()
    {
        byte[] buffer = new byte[0x2d000];
        PS3.SetMemory(0xf0e10c, buffer);
    }

    public static void Enable()
    {
        byte[] buffer = new byte[] {
                0x3f, 0x80, 0x10, 5, 0x81, 0x9c, 0, 0x48, 0x2c, 12, 0, 0, 0x41, 130, 0, 120,
                0x80, 0x7c, 0, 0, 0x80, 0x9c, 0, 4, 0x80, 0xbc, 0, 8, 0x80, 220, 0, 12,
                0x80, 0xfc, 0, 0x10, 0x81, 0x1c, 0, 20, 0x81, 60, 0, 0x18, 0x81, 0x5c, 0, 0x1c,
                0x81, 0x7c, 0, 0x20, 0xc0, 60, 0, 0x24, 0xc0, 0x5c, 0, 40, 0xc0, 0x7c, 0, 0x2c,
                0xc0, 0x9c, 0, 0x30, 0xc0, 0xbc, 0, 0x34, 0xc0, 220, 0, 0x38, 0xc0, 0xfc, 0, 60,
                0xc1, 0x1c, 0, 0x40, 0xc1, 60, 0, 0x44, 0x7d, 0x89, 3, 0xa6, 0x4e, 0x80, 4, 0x21,
                0x38, 0x80, 0, 0, 0x90, 0x9c, 0, 0x48, 0x90, 0x7c, 0, 0x4c, 0xd0, 60, 0, 80,
                0x48, 0, 0, 20
             };
        PS3.SetMemory(func_address, new byte[] { 0x41 });
        PS3.SetMemory(func_address + 4, buffer);
        PS3.SetMemory(func_address, new byte[] { 0x40 });
        Thread.Sleep(10);
        DestroyAll();
    }

    public static void Fov(int client, string Text)
    {
        SV_GameSendServerCommand(client, "q cg_fov " + Text);
        Thread.Sleep(20);
    }

    public static uint G_ClientFunction(int client)
    {
        return (Offsets.Addresses.G_Client + ((uint)(client * 0x3980)));
    }

    public static uint GetFuncReturn()
    {
        byte[] bytes = new byte[4];
        GetMemoryR(0x114ae64, ref bytes);
        Array.Reverse(bytes);
        return BitConverter.ToUInt32(bytes, 0);
    }

    public static byte[] GetMemory(uint offset, int length)
    {
        byte[] buffer = new byte[length];
        PS3.GetMemory(offset, buffer);
        return buffer;
    }

    public static void GetMemoryR(uint Address, ref byte[] Bytes)
    {
        PS3.GetMemory(Address, Bytes);
    }

    public static string GetNames(int clientNum)
    {
        byte[] bytes = new byte[0x12];
        GetMemoryR((uint)(0x110d60c + (clientNum * 0x3980)), ref bytes);
        string str = Encoding.ASCII.GetString(bytes);
        str.Replace(Convert.ToChar(0).ToString(), string.Empty);
        return str;
    }

    public static void GiveWeapon(int client, int weapon, int akimbo)
    {
        Call(Offsets.Addresses.G_GivePlayerWeapon, new object[] { G_ClientFunction(client), (uint)weapon, 0 });
        Call(Offsets.Addresses.Add_Ammo, new object[] { Offsets.Addresses.G_Entity + ((uint)(client * 640)), (uint)weapon, 0, 0x270f, 1 });
    }

    public static void iPrintln(int client, string Text)
    {
        string str = Text.Replace("[X]", "\x0001").Replace("[O]", "\x0002").Replace("[]", "\x0003").Replace("[Y]", "\x0004").Replace("[L1]", "\x0005").Replace("[R1]", "\x0006").Replace("[L3]", "\x0010").Replace("[R3]", "\x0011").Replace("[L2]", "\x0012").Replace("[R2]", "\x0013").Replace("[UP]", "\x0014").Replace("[DOWN]", "\x0015").Replace("[LEFT]", "\x0016").Replace("[RIGHT]", "\x0017").Replace("[START]", "\x000e").Replace("[SELECT]", "\x000f").Replace("[LINE]", "\n");
        SV_GameSendServerCommand(client, "c \"" + Text + "\"");
        Thread.Sleep(20);
    }

    public static void iPrintlnBold(int client, string Text)
    {
        string str = Text.Replace("[X]", "\x0001").Replace("[O]", "\x0002").Replace("[]", "\x0003").Replace("[Y]", "\x0004").Replace("[L1]", "\x0005").Replace("[R1]", "\x0006").Replace("[L3]", "\x0010").Replace("[R3]", "\x0011").Replace("[L2]", "\x0012").Replace("[R2]", "\x0013").Replace("[UP]", "\x0014").Replace("[DOWN]", "\x0015").Replace("[LEFT]", "\x0016").Replace("[RIGHT]", "\x0017").Replace("[START]", "\x000e").Replace("[SELECT]", "\x000f").Replace("[LINE]", "\n");
        SV_GameSendServerCommand(client, "f \"" + Text + "\"");
        Thread.Sleep(20);
    }

    public static string Key_IsDown(uint ClientNum)
    {
        byte[] bytes = new byte[3];
        GetMemoryR(0x110d5e1 + (0x3980 * ClientNum), ref bytes);
        string str4 = BitConverter.ToString(bytes).Replace("-", "").Replace(" ", "");
        switch (str4)
        {
            case "000000":
                return "Stand";

            case "080C20":
                return "[ ] + X + L1";

            case "000224":
                return "Crouch + R3 + [ ]";

            case "008001":
                return "R1 + L2";

            case "082802":
                return "L1 + L3";

            case "002402":
                return "X + L3";

            case "000020":
                return "[  ]";

            case "000200":
                return "Crouch";

            case "004020":
                return "R2 + [ ]";

            case "000220":
                return "[ ] + Crouch";

            case "000100":
                return "Prone";

            case "400100":
                return "Left + Prone";

            case "000400":
                return "X";

            case "000004":
                return "R3";

            case "002002":
                return "L3";

            case "004000":
                return "R2";

            case "008000":
                return "L2";

            case "080800":
                return "L1";

            case "000001":
                return "R1";

            case "002006":
                return "R3 + L3";

            case "000204":
                return "R3";

            case "002202":
                return "L3";

            case "004200":
                return "R2";

            case "008004":
                return "R3 + L2";

            case "008200":
                return "L2";

            case "082902":
                return "Prone + L1 + L3";

            case "082906":
                return "Prone + L1 + L3 + R3";

            case "00C100":
                return "Prone + R2 + L2";

            case "00C000":
                return "R2 + L2";

            case "002206":
                return "Crouch L3 + R3";

            case "002222":
                return "Crouch L3 + [ ]";

            case "Up":
                return "R2 + L2";

            case "002122":
                return "Prone + L3 + [ ]";

            case "000420":
                return "X + [ ]";

            case "002106":
                return "Prone + R3 + L3";
        }
        return str4;
    }

    public static void Kick(int client, string Text)
    {
        SV_GameSendServerCommand(client, "r \"" + Text + "\"");
        Thread.Sleep(20);
    }

    private static byte[] ReadBytes(uint address, int length)
    {
        return GetMemory(address, length);
    }

    public static float[] ReadSingle(uint address, int length)
    {
        byte[] array = ReadBytes(address, length * 4);
        Array.Reverse(array);
        float[] numArray = new float[length];
        for (int i = 0; i < length; i++)
        {
            numArray[i] = BitConverter.ToSingle(array, ((length - 1) - i) * 4);
        }
        return numArray;
    }

    public static byte[] ReverseBytes(byte[] inArray)
    {
        Array.Reverse(inArray);
        return inArray;
    }

    public static void Set_ClientDvar(int client, string Text)
    {
        SV_GameSendServerCommand(client, "q " + Text);
        Thread.Sleep(20);
    }

    public static void SetModel(int client, string model)
    {
        Call(Offsets.Addresses.G_SetModel, new object[] { Offsets.Addresses.G_Entity + ((uint)(client * 640)), model, 0, 0, 0 });
    }

    public static void SV_GameSendServerCommand(int client, string command)
    {
        Call(Offsets.Addresses.SV_GameSendServerCommand, new object[] { (uint)client, 0, command, 0, 0 });
    }

    public static void SV_KickClient(int client, string text)
    {
        uint address = 0x223bd0;
        Call(address, new object[] { client, text });
    }

    public static void Vision(int client, string Text)
    {
        SV_GameSendServerCommand(client, "J \"" + Text + "\"");
        Thread.Sleep(20);
    }

    private static void WriteSingle(uint address, float input)
    {
        byte[] array = new byte[4];
        BitConverter.GetBytes(input).CopyTo(array, 0);
        Array.Reverse(array, 0, 4);
        PS3.SetMemory(address, array);
    }

    public static void WriteSingle(uint address, float[] input)
    {
        int length = input.Length;
        byte[] array = new byte[length * 4];
        for (int i = 0; i < length; i++)
        {
            ReverseBytes(BitConverter.GetBytes(input[i])).CopyTo(array, (int)(i * 4));
        }
        PS3.SetMemory(address, array);
    }

    public static class Offsets
    {
        public static class Addresses
        {
            public static uint Add_Ammo = 0x18a29c;
            public static uint AimTarget_RegisterDvars = 0x12098;
            public static uint BG_FindWeaponIndexForName = 0x3cfd0;
            public static uint BG_GetEntityTypeName = 0x1d1f0;
            public static uint BG_GetNumWeapons = 0x3cfbc;
            public static uint BG_GetPerkIndexForName = 0x210b0;
            public static uint BG_GetViewModelWeaponIndex = 0x3d7d8;
            public static uint BG_GetWeaponIndexForName = 0x3d434;
            public static uint BG_TakePlayerWeapon = 0x1c409c;
            public static uint BG_WeaponFireRecoil = 0x3fbd0;
            public static uint CalculateRanks = 0x19031c;
            public static uint CBuf_AddText = 0x1db240;
            public static uint CG_BoldGameMessage = 0x7a5c8;
            public static uint CG_FireWeapon = 0xbe498;
            public static uint CL_DrawText = 0xd9490;
            public static uint CL_DrawTextHook = 0xd93a8;
            public static uint CL_DrawTextRotate = 0xd9554;
            public static uint CL_GetClientState = 0xe26a8;
            public static uint CL_GetConfigString = 0xc5e7c;
            public static uint CL_RegisterFont = 0xd9734;
            public static uint ClientCommand = 0x182440;
            public static uint ClientConnect = 0x1771a0;
            public static uint ClientScr_SetMaxHealth = 0x176094;
            public static uint ClientScr_SetScore = 0x176150;
            public static uint ClientSpawn = 0x177468;
            public static uint Cmd_AddCommandInternal = 0x1dc4fc;
            public static uint Cmd_ExecuteSingleCommand = 0x1db240;
            public static uint Dvar_GetBool = 0x291060;
            public static uint Dvar_GetFloat = 0x291148;
            public static uint Dvar_GetInt = 0x2910dc;
            public static uint Dvar_IsValidName = 0x29019c;
            public static uint Dvar_RegisterBool = 0x2933f0;
            public static uint EntityIndex = 640;
            public static uint G_CallSpawnEntity = 0x1ba730;
            public static uint g_client = 0x110a280;
            public static uint G_Client = 0x110a280;
            public static uint G_ClientIndex = 0x3980;
            public static uint G_Damage = 0x183e18;
            public static uint G_Entity = 0xfca280;
            public static uint G_EntUnlink = 0x1c4a5c;
            public static uint G_FreeEntity = 0x1c0840;
            public static uint g_gametype = 0x8360d5;
            public static uint G_GetClientDeaths = 0x18ea98;
            public static uint G_GetClientScore = 0x18ea74;
            public static uint G_GivePlayerWeapon = 0x1c3034;
            public static uint G_LocalizedStringIndex = 0x1be6cc;
            public static uint G_MaterialIndex = 0x1be744;
            public static uint G_ModelIndex = 0x1be7a8;
            public static uint G_ModelName = 0x1be8a0;
            public static uint G_RadiusDamage = 0x185600;
            public static uint G_SetModel = 0x1bef5c;
            public static uint Info_ValueForKey = 0x299604;
            public static uint IntermissionClientEndFrame = 0x1745f8;
            public static uint Jump_RegisterDvars = 0x18e20;
            public static uint Key_Bind_f = 0xd247c;
            public static uint Key_IsDown = 0xd1cd8;
            public static uint Key_IsValidGamePadChar = 0xd1e64;
            public static uint Key_KeyNumToString = 0xd1ea4;
            public static uint Key_StringToKeynum = 0xd1d18;
            public static uint Key_Unbind_f = 0xd2368;
            public static uint MapBrushModel = 0x7f80;
            public static uint Material_RegisterHandle = 0x38b044;
            public static uint memset = 0x49b928;
            public static uint Origin = 0x191b00;
            public static uint Player_Die = 0x183748;
            public static uint PlayerCMD_ClonePlayer = 0x180f48;
            public static uint PlayerCmd_SetClientDvar = 0x17cb4c;
            public static uint PlayerCmd_SetPerk = 0x17ebe8;
            public static uint PlayerDie = 0x183748;
            public static uint R_AddCmdDrawStretchPic = 0x392d78;
            public static uint R_AddCmdDrawText = 0x393640;
            public static uint R_AddCmdDrawTextWithEffects = 0x3937c0;
            public static uint R_NormalizedTextScale = 0x3808f0;
            public static uint R_RegisterDvars = 0x37e420;
            public static uint R_RegisterFont = 0x3808b8;
            public static uint Scr_ConstructMessageString = 0x1b04f4;
            public static uint Scr_GetInt = 0x2201c4;
            public static uint Scr_MakeGameMessage = 0x1b07f0;
            public static uint Scr_Notify = 0x1bb1b0;
            public static uint SetClientViewAngle = 0x1767e0;
            public static uint str_pointer = 0x523b30;
            public static uint Sv_AddServerCommand = 0x22cba0;
            public static uint Sv_ClientCommand = 0x228178;
            public static uint Sv_DirectConnect = 0x255bb4;
            public static uint SV_DObjGetTree = 0x229a68;
            public static uint SV_DropClient = 0x2249fc;
            public static uint Sv_ExecuteClientCommand = 0x182dec;
            public static uint Sv_ExecuteClientMessage = 0x228b50;
            public static uint SV_GameDropClient = 0x229020;
            public static uint SV_GameSendServerCommand = 0x228fa8;
            public static uint SV_GetConfigString = 0x22a4a8;
            public static uint SV_KickClient = 0x223bd0;
            public static uint sv_map_f = 0x2235a0;
            public static uint Sv_Maprestart = 0x223774;
            public static uint sv_maprestart_f = 0x223b20;
            public static uint sv_matchend = 0x22f7a8;
            public static uint Sv_ReceiveStats = 0x2244e0;
            public static uint SV_SendClientGameState = 0x2284f8;
            public static uint SV_SendDisconnect = 0x22472c;
            public static uint SV_SendServerCommand = 0x22cebc;
            public static uint Sv_SetConfigstring = 0x22a208;
            public static uint SV_SetConfigString = 0x22a208;
            public static uint Sv_SetGametype = 0x229c1c;
            public static uint sv_spawnsever = 0x22adf8;
            public static uint TeleportPlayer = 0x191b00;
            public static uint UI_DrawLoadBar = 0x23a730;
            public static uint UI_FillRectPhysical = 0x23a810;
            public static uint va = 0x299490;
        }
    }
}
