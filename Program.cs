using ENet;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
namespace CriticalServer
{
    class Program
    {
        private static string LocalIP = "192.168.0.11";
        private static int HttpPort = 8080;
        private static int GamePort = 1234;
        private static string AssetFolderPath = @"C:\Users\serme\Desktop\CopsAssets";
        static Dictionary<string, string> IpToDeviceId = new Dictionary<string, string>();
        public class OpenPackResult
        {
            public bool Success;
            public string Message;
            public int SkinID;
            public bool AlreadyOwned;
            public int TokensGained;
            public int PacksLeft;
            public int CurrentTokens;
        }
        public static class DatabaseHelper
        {
            private static string DbPath = "referans.db";
            private static object DbLock = new object();
            private static int[] GetUserSkins(int userId, SqliteConnection connection)
            {
                var skins = new List<int>();
                try
                {
                   var cmd = connection.CreateCommand();
                   cmd.CommandText = "SELECT SkinID FROM UserWeaponSkins WHERE UserID = $uid";
                   cmd.Parameters.AddWithValue("$uid", userId);
                   using (var r = cmd.ExecuteReader())
                   {
                       while (r.Read()) skins.Add(r.GetInt32(0));
                   }
                } catch {}
                return skins.ToArray();
            }
            public static bool ProcessPurchase(int userId, int cost, int quantity, out int newCredits, out int newPacks)
            {
                newCredits = 0;
                newPacks = 0;
                lock (DbLock)
                {
                    using (var connection = new SqliteConnection($"Data Source={DbPath};Pooling=False"))
                    {
                        connection.Open();
                        var checkCmd = connection.CreateCommand();
                        checkCmd.CommandText = "SELECT Credits, SkinPacks FROM Users WHERE UserID = $uid";
                        checkCmd.Parameters.AddWithValue("$uid", userId);
                        using (var reader = checkCmd.ExecuteReader())
                        {
                            if (!reader.Read()) return false; 
                            int currentCredits = reader.GetInt32(0);
                            int currentPacks = reader.GetInt32(1);
                            if (currentCredits < cost) return false; 
                            newCredits = currentCredits - cost;
                            newPacks = currentPacks + quantity;
                        }
                        var updateCmd = connection.CreateCommand();
                        updateCmd.CommandText = "UPDATE Users SET Credits = $cred, SkinPacks = $packs WHERE UserID = $uid";
                        updateCmd.Parameters.AddWithValue("$cred", newCredits);
                        updateCmd.Parameters.AddWithValue("$packs", newPacks);
                        updateCmd.Parameters.AddWithValue("$uid", userId);
                        updateCmd.ExecuteNonQuery();
                        return true;
                    }
                }
            }
            public static OpenPackResult ProcessOpenPack(int userId)
            {
                var result = new OpenPackResult { Success = false };
                int[] tier1 = { 115, 136, 156, 158, 171, 195, 215, 217, 218, 220, 222, 223, 225, 226, 228, 230, 250, 252, 260, 261, 264, 278, 291, 307, 370, 374, 388, 393, 396, 399, 403, 404, 406, 409, 413, 415, 417, 420, 421, 470, 499, 904, 906, 907 };
                int[] tier2 = { 231, 232, 233, 234, 235, 236, 237, 238, 239, 240, 241, 242, 243, 244, 245, 246, 247, 277, 344, 346, 358, 371, 376, 389, 394, 401, 424, 425, 426, 427, 428, 429, 430, 431, 432, 433, 524, 525, 526, 527, 528, 529, 530, 550, 551, 552, 553, 554, 556, 557, 558, 559, 560, 561, 562, 564, 565, 566, 567, 570, 573, 574, 679, 680, 681, 725, 802, 803 };
                int[] tier3 = { 121, 128, 143, 155, 163, 165, 166, 185, 187, 197, 202, 209, 273, 283, 285, 286, 289, 299, 300, 361, 362, 372, 377, 390, 397, 445, 446, 449, 451, 452, 453, 454, 455, 456, 457, 459, 460, 496, 541, 642, 646, 648, 651, 652, 667, 677, 678, 683, 686, 726, 744, 748, 789, 791, 804, 805, 806, 807, 808, 809, 810, 811, 881, 889, 895, 909 };
                int[] tier4 = { 114, 124, 144, 148, 302, 318, 321, 330, 435, 438, 440, 466, 480, 481, 487, 514, 515, 535, 537, 538, 568, 569, 578, 580, 583, 604, 623, 656, 668, 669, 685, 693, 695, 707, 709, 712, 713, 716, 719, 721, 732, 737, 743, 751, 752, 782, 784, 790, 812, 814, 815, 816, 817, 818, 819, 820, 822, 851, 857, 858, 874, 888, 897, 900 };
                int[] tier5 = { 324, 325, 326, 327, 328, 341, 410, 436, 439, 443, 444, 468, 476, 477, 484, 505, 509, 531, 532, 533, 539, 643, 644, 653, 654, 658, 675, 682, 708, 727, 745, 771, 793, 828, 832, 859, 866, 868, 879, 882, 885, 898, 910 };
                int[] tier6 = { 700, 717, 731 };
                int[] tier7 = { 701, 702, 703, 704, 705, 718, 741, 753, 754, 756, 824, 827 };
                int[] tierWeights = { 5, 5, 20, 25, 20, 15, 10 };
                int[][] tierPools = { tier1, tier2, tier3, tier4, tier5, tier6, tier7 };
                Random rng = new Random();
                int roll = rng.Next(100);
                int cumulative = 0;
                int selectedTier = 0;
                for (int t = 0; t < tierWeights.Length; t++)
                {
                    cumulative += tierWeights[t];
                    if (roll < cumulative)
                    {
                        selectedTier = t;
                        break;
                    }
                }
                int[] selectedPool = tierPools[selectedTier];
                int randomSkinId = selectedPool[rng.Next(selectedPool.Length)];
                result.SkinID = randomSkinId;
                lock (DbLock)
                {
                    using (var connection = new SqliteConnection($"Data Source={DbPath};Pooling=False"))
                    {
                        connection.Open();
                        var checkCmd = connection.CreateCommand();
                        checkCmd.CommandText = "SELECT SkinPacks, Tokens FROM Users WHERE UserID = $uid";
                        checkCmd.Parameters.AddWithValue("$uid", userId);
                        int currentPacks = 0;
                        int currentTokens = 0;
                        using (var reader = checkCmd.ExecuteReader())
                        {
                            if (!reader.Read()) { result.Message = "User not found"; return result; }
                            currentPacks = reader.GetInt32(0);
                            currentTokens = reader.GetInt32(1);
                        }
                        if (currentPacks <= 0) { result.Message = "No packs"; return result; }
                        currentPacks--;
                        result.PacksLeft = currentPacks;
                        var skinCheckCmd = connection.CreateCommand();
                        skinCheckCmd.CommandText = "SELECT Count(*) FROM UserWeaponSkins WHERE UserID = $uid AND SkinID = $sid";
                        skinCheckCmd.Parameters.AddWithValue("$uid", userId);
                        skinCheckCmd.Parameters.AddWithValue("$sid", randomSkinId);
                        long count = (long)skinCheckCmd.ExecuteScalar();
                        if (count > 0)
                        {
                            result.AlreadyOwned = true;
                            int[] tierTokens = { 25, 50, 75, 100, 125, 150, 175 }; 
                            result.TokensGained = tierTokens[selectedTier];
                            currentTokens += tierTokens[selectedTier];
                        }
                        else
                        {
                            result.AlreadyOwned = false;
                            result.TokensGained = 0;
                            var addSkinCmd = connection.CreateCommand();
                            addSkinCmd.CommandText = "INSERT INTO UserWeaponSkins (UserID, SkinID) VALUES ($uid, $sid)";
                            addSkinCmd.Parameters.AddWithValue("$uid", userId);
                            addSkinCmd.Parameters.AddWithValue("$sid", randomSkinId);
                            addSkinCmd.ExecuteNonQuery();
                        }
                        result.CurrentTokens = currentTokens;
                        var updateCmd = connection.CreateCommand();
                        updateCmd.CommandText = "UPDATE Users SET SkinPacks = $packs, Tokens = $tok WHERE UserID = $uid";
                        updateCmd.Parameters.AddWithValue("$packs", currentPacks);
                        updateCmd.Parameters.AddWithValue("$tok", currentTokens);
                        updateCmd.Parameters.AddWithValue("$uid", userId);
                        updateCmd.ExecuteNonQuery();
                        result.Success = true;
                        return result;
                    }
                }
            }
            public static void Initialize()
            {
                if (!File.Exists(DbPath))
                {
                }
                lock (DbLock)
                {
                    using (var connection = new SqliteConnection($"Data Source={DbPath};Pooling=False"))
                    {
                        connection.Open();
                        var command = connection.CreateCommand();
                        command.CommandText =
                        @"
                            CREATE TABLE IF NOT EXISTS Users (
                                UserID INTEGER PRIMARY KEY AUTOINCREMENT,
                                DeviceID TEXT UNIQUE NOT NULL,
                                Username TEXT NOT NULL,
                                Platform TEXT DEFAULT 'Guest',
                                Credits INTEGER DEFAULT 15000,
                                Tokens INTEGER DEFAULT 1000,
                                SkinPacks INTEGER DEFAULT 0,
                                UserType INTEGER DEFAULT 1,
                                LastIP TEXT,
                                CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
                                LastLogin DATETIME
                            );
                             CREATE TABLE IF NOT EXISTS UserWeaponSkins (
                                ID INTEGER PRIMARY KEY AUTOINCREMENT,
                                UserID INTEGER NOT NULL,
                                SkinID INTEGER NOT NULL,
                                FOREIGN KEY (UserID) REFERENCES Users(UserID)
                            );
                             CREATE TABLE IF NOT EXISTS UserLoadout (
                                UserID INTEGER NOT NULL,
                                WeaponID INTEGER NOT NULL,
                                SkinID INTEGER NOT NULL,
                                PRIMARY KEY (UserID, WeaponID)
                            );
                        ";
                        command.ExecuteNonQuery();
                        try {
                            var alterCmd = connection.CreateCommand();
                            alterCmd.CommandText = "ALTER TABLE Users ADD COLUMN LastIP TEXT;";
                            alterCmd.ExecuteNonQuery();
                        } catch {  }
                    }
                }
            }
            public static UserData GetOrCreateUser(string deviceId, string platform, string ip)
            {
                lock (DbLock)
                {
                    using (var connection = new SqliteConnection($"Data Source={DbPath};Pooling=False"))
                    {
                        connection.Open();
                        var command = connection.CreateCommand();
                        command.CommandText = "SELECT * FROM Users WHERE DeviceID = $id";
                        command.Parameters.AddWithValue("$id", deviceId);
                        using (var reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                var user = ReadUser(reader);
                                reader.Close(); 
                                UpdateLastLogin(user.UserID, ip, connection);
                                user.OwnedSkins = GetUserSkins(user.UserID, connection);
                                return user;
                            }
                        }
                        string randomSuffix = new Random().Next(10000, 99999).ToString();
                        string newUsername = $"player_{randomSuffix}";
                        var insertCmd = connection.CreateCommand();
                        insertCmd.CommandText =
                        @"
                            INSERT INTO Users (DeviceID, Username, Platform, Credits, UserType, LastIP, LastLogin)
                            VALUES ($did, $uname, $plat, 15000, 1, $ip, CURRENT_TIMESTAMP);
                            SELECT last_insert_rowid();
                        ";
                        insertCmd.Parameters.AddWithValue("$did", deviceId);
                        insertCmd.Parameters.AddWithValue("$uname", newUsername);
                        insertCmd.Parameters.AddWithValue("$plat", platform);
                        insertCmd.Parameters.AddWithValue("$ip", ip);
                        long newId = (long)insertCmd.ExecuteScalar();
                        return new UserData
                        {
                            UserID = (int)newId,
                            DeviceID = deviceId,
                            Username = newUsername,
                            Platform = platform,
                            Credits = 15000,
                            Tokens = 1000,
                            UserType = 1,
                            LastIP = ip
                        };
                    }
                }
            }
            public static UserData GetUserByDeviceID(string deviceId)
            {
                lock (DbLock)
                {
                     using (var connection = new SqliteConnection($"Data Source={DbPath};Pooling=False"))
                    {
                        connection.Open();
                        var command = connection.CreateCommand();
                        command.CommandText = "SELECT * FROM Users WHERE DeviceID = $id";
                        command.Parameters.AddWithValue("$id", deviceId);
                        using (var reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                var user = ReadUser(reader);
                                reader.Close();
                                user.OwnedSkins = GetUserSkins(user.UserID, connection);
                                return user;
                            }
                        }
                    }
                }
                return null;
            }
            private static void UpdateLastLogin(int userId, string ip, SqliteConnection openConnection)
            {
                var cmd = openConnection.CreateCommand();
                cmd.CommandText = "UPDATE Users SET LastLogin = CURRENT_TIMESTAMP, LastIP = $ip WHERE UserID = $uid";
                cmd.Parameters.AddWithValue("$uid", userId);
                cmd.Parameters.AddWithValue("$ip", ip);
                cmd.ExecuteNonQuery();
            }
            private static UserData ReadUser(SqliteDataReader reader)
            {
                string ip = reader["LastIP"] != DBNull.Value ? reader["LastIP"].ToString() : "";
                return new UserData
                {
                    UserID = Convert.ToInt32(reader["UserID"]),
                    DeviceID = reader["DeviceID"].ToString(),
                    Username = reader["Username"].ToString(),
                    Platform = reader["Platform"].ToString(),
                    Credits = Convert.ToInt32(reader["Credits"]),
                    Tokens = Convert.ToInt32(reader["Tokens"]),
                    UserType = Convert.ToInt32(reader["UserType"]),
                    SkinPacks = Convert.ToInt32(reader["SkinPacks"]),
                    LastIP = ip
                };
            }
            public static bool IsUsernameAvailable(string username)
            {
                lock (DbLock)
                {
                    using (var connection = new SqliteConnection($"Data Source={DbPath};Pooling=False"))
                    {
                        connection.Open();
                        var cmd = connection.CreateCommand();
                        cmd.CommandText = "SELECT COUNT(*) FROM Users WHERE LOWER(Username) = LOWER($name)";
                        cmd.Parameters.AddWithValue("$name", username);
                        long count = (long)cmd.ExecuteScalar();
                        return count == 0; 
                    }
                }
            }
            public static bool ChangeUsername(int userId, string newUsername, out string resultMessage)
            {
                resultMessage = "";
                if (!IsUsernameAvailable(newUsername))
                {
                    resultMessage = "Username already taken";
                    return false;
                }
                lock (DbLock)
                {
                    using (var connection = new SqliteConnection($"Data Source={DbPath};Pooling=False"))
                    {
                        connection.Open();
                        var cmd = connection.CreateCommand();
                        cmd.CommandText = "UPDATE Users SET Username = $name WHERE UserID = $uid";
                        cmd.Parameters.AddWithValue("$name", newUsername);
                        cmd.Parameters.AddWithValue("$uid", userId);
                        int affected = cmd.ExecuteNonQuery();
                        if (affected > 0)
                        {
                            resultMessage = "Success";
                            return true;
                        }
                        else
                        {
                            resultMessage = "User not found";
                            return false;
                        }
                    }
                }
            }
            public static void UpdateCredits(int userId, int newCredits)
            {
                lock (DbLock)
                {
                    using (var connection = new SqliteConnection($"Data Source={DbPath};Pooling=False"))
                    {
                        connection.Open();
                        var cmd = connection.CreateCommand();
                        cmd.CommandText = "UPDATE Users SET Credits = $credits WHERE UserID = $uid";
                        cmd.Parameters.AddWithValue("$credits", newCredits);
                        cmd.Parameters.AddWithValue("$uid", userId);
                        cmd.ExecuteNonQuery();
                }
            }
            }
            public static void UpdateTokens(int userId, int newTokens)
            {
                lock (DbLock)
                {
                    using (var connection = new SqliteConnection($"Data Source={DbPath};Pooling=False"))
                    {
                        connection.Open();
                        var cmd = connection.CreateCommand();
                        cmd.CommandText = "UPDATE Users SET Tokens = $tokens WHERE UserID = $uid";
                        cmd.Parameters.AddWithValue("$tokens", newTokens);
                        cmd.Parameters.AddWithValue("$uid", userId);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            public static void AddSkinToUser(int userId, int skinID)
            {
                lock (DbLock)
                {
                    using (var connection = new SqliteConnection($"Data Source={DbPath};Pooling=False"))
                    {
                        connection.Open();
                        var checkCmd = connection.CreateCommand();
                        checkCmd.CommandText = "SELECT COUNT(*) FROM UserWeaponSkins WHERE UserID = $uid AND SkinID = $sid";
                        checkCmd.Parameters.AddWithValue("$uid", userId);
                        checkCmd.Parameters.AddWithValue("$sid", skinID);
                        long count = (long)checkCmd.ExecuteScalar();
                        if (count == 0)
                        {
                            var insertCmd = connection.CreateCommand();
                            insertCmd.CommandText = "INSERT INTO UserWeaponSkins (UserID, SkinID) VALUES ($uid, $sid)";
                            insertCmd.Parameters.AddWithValue("$uid", userId);
                            insertCmd.Parameters.AddWithValue("$sid", skinID);
                            insertCmd.ExecuteNonQuery();
                        }
                    }
                }
            }
            public static bool SetUserLoadout(int userId, int weaponId, int skinId)
            {
                lock (DbLock)
                {
                    using (var connection = new SqliteConnection($"Data Source={DbPath};Pooling=False"))
                    {
                        connection.Open();
                        bool ownsSkin = false;
                        if (skinId <= 0) ownsSkin = true;
                        else
                        {
                            var checkCmd = connection.CreateCommand();
                            checkCmd.CommandText = "SELECT COUNT(*) FROM UserWeaponSkins WHERE UserID = $uid AND SkinID = $sid";
                            checkCmd.Parameters.AddWithValue("$uid", userId);
                            checkCmd.Parameters.AddWithValue("$sid", skinId);
                            long count = (long)checkCmd.ExecuteScalar();
                            ownsSkin = count > 0;
                        }
                        if (!ownsSkin) return false;
                        var cmd = connection.CreateCommand();
                        cmd.CommandText = "INSERT OR REPLACE INTO UserLoadout (UserID, WeaponID, SkinID) VALUES ($uid, $wid, $sid)";
                        cmd.Parameters.AddWithValue("$uid", userId);
                        cmd.Parameters.AddWithValue("$wid", weaponId);
                        cmd.Parameters.AddWithValue("$sid", skinId);
                        cmd.ExecuteNonQuery();
                        return true;
                    }
                }
            }
            public static int GetUserLoadout(int userId, int weaponId)
            {
                lock (DbLock)
                {
                    using (var connection = new SqliteConnection($"Data Source={DbPath};Pooling=False"))
                    {
                        connection.Open();
                        var cmd = connection.CreateCommand();
                        cmd.CommandText = "SELECT SkinID FROM UserLoadout WHERE UserID = $uid AND WeaponID = $wid";
                        cmd.Parameters.AddWithValue("$uid", userId);
                        cmd.Parameters.AddWithValue("$wid", weaponId);
                        var result = cmd.ExecuteScalar();
                        if (result != null && result != DBNull.Value)
                        {
                            return Convert.ToInt32(result);
                        }
                        return 0; 
                    }
                }
            }
        }
        public class UserData
        {
            public int UserID { get; set; }
            public string DeviceID { get; set; }
            public string Username { get; set; }
            public string Platform { get; set; }
            public int Credits { get; set; }
            public int Tokens { get; set; }
            public int UserType { get; set; }
            public int SkinPacks { get; set; }
            public string LastIP { get; set; }
            public int[] OwnedSkins { get; set; } = new int[0]; 
        }
        class ClientState
        {
            public ushort PeerId;
            public IPEndPoint EndPoint;
            public bool VerifyConnectSent;
            public bool Connected;
            public int Sequence;  
            public byte[] Challenge;
            public Dictionary<byte, int> ChannelSequence = new Dictionary<byte, int>();
            public int PlayerID = 0; 
            public int RoomID = 0; 
            public int Team = 0; 
            public string Username = "";
            public byte[] LastCharacterUpdateData;
            public int LastActivityTime = Environment.TickCount;
            public Dictionary<int, int> WeaponIDs = new Dictionary<int, int>();
            public int GetNextSequence(byte channel)
            {
                if (!ChannelSequence.ContainsKey(channel))
                    ChannelSequence[channel] = 1;  
                return ChannelSequence[channel]++;
            }
            public Dictionary<byte, int> ChannelUnreliableSequence = new Dictionary<byte, int>(); 
            public int GetNextUnreliableSequence(byte channel)
            {
                if (!ChannelUnreliableSequence.ContainsKey(channel))
                    ChannelUnreliableSequence[channel] = 1;
                return ChannelUnreliableSequence[channel]++;
            }
        }
        static bool roomActive = false;
        static ushort roomOwnerPeerId = 0;
        class Room
        {
            public int RoomId;
            public ushort OwnerPeerId;
            public string Name = "Room";
            public List<ClientState> Players = new List<ClientState>();
        public int MatchStartTime;
        public int MatchEndTime; 
        public int Tick = 0;
        public Room() {
            int now = Environment.TickCount & 0x7FFFFFFF;
            MatchStartTime = now;
            MatchEndTime = now + 600000; 
        }        }
        static Dictionary<int, Room> rooms = new Dictionary<int, Room>();
        static int nextRoomId = 1;
        static int nextWeaponInstanceId = 1000;
        static Dictionary<string, ClientState> clients = new Dictionary<string, ClientState>();
        static void Main(string[] args)
        {
            Console.Title = "srkorwho custom server";
            DatabaseHelper.Initialize();
            Task httpTask = Task.Run(() => StartHttpServer());
            Task photonTask = Task.Run(() => StartPhotonSimServer());
            while (true)
            {
                System.Threading.Thread.Sleep(500);
            }
        }
        static void StartPhotonSimServer()
        {
            using (UdpClient udp = new UdpClient(GamePort))
            {
                try {
                    const int SIO_UDP_CONNRESET = -1744830452;
                    udp.Client.IOControl(SIO_UDP_CONNRESET, new byte[] { 0 }, null);
                } catch {  }
                IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);
                ushort nextPeerId = 1234;
                while (true)
                {
                    try
                    {
                        byte[] data = udp.Receive(ref remoteEP);
                        string key = $"{remoteEP.Address}:{remoteEP.Port}";
                        if (data.Length < 4) continue;
                        ushort peerIdFromPacket = (ushort)((data[0] << 8) | data[1]);
                        byte commandType = (byte)(data[3] & 0x0F);
                        if (commandType != 7 && commandType != 4)
                        {
                        }
                        if (!clients.ContainsKey(key))
                        {
                            clients[key] = new ClientState
                            {
                                PeerId = nextPeerId++,
                                EndPoint = remoteEP,
                                VerifyConnectSent = false,
                                Connected = false,
                                Sequence = 1  
                            };
                        }
                        ClientState client = clients[key];
                        HandlePacket(udp, remoteEP, data, client);
                    }
                    catch (Exception ex)
                    {
                    }
                }
            }
        }
        static void BroadcastEvent(UdpClient udp, byte[] packet, int senderPeerId, bool includeSender, byte channel, int roomId)
        {
            int sentCount = 0;
            foreach (var kvp in clients)
            {
                var targetClient = kvp.Value;
                if (!targetClient.Connected) continue;
                if (targetClient.RoomID != roomId) {
                    continue;
                }
                if (targetClient.PeerId == senderPeerId && !includeSender) {
                    continue;
                }
                byte[] clone = new byte[packet.Length];
                Buffer.BlockCopy(packet, 0, clone, 0, packet.Length);
                ushort pid = targetClient.PeerId;
                clone[0] = (byte)(pid >> 8);
                clone[1] = (byte)(pid);
                int seq = targetClient.GetNextSequence(channel);
                clone[20] = (byte)(seq >> 24);
                clone[21] = (byte)(seq >> 16);
                clone[22] = (byte)(seq >> 8);
                clone[23] = (byte)(seq);
                try
                {
                    udp.Send(clone, clone.Length, targetClient.EndPoint);
                    sentCount++;
                }
                catch (Exception ex)
                {
                }
            }
        }
        static void HandlePacket(UdpClient udp, IPEndPoint ep, byte[] data, ClientState client)
        {
            if (data.Length < 12) return;
            int offset = 0;
            ushort peerIdFromPacket = (ushort)((data[offset++] << 8) | data[offset++]);
            byte crcFlag = data[offset++];
            byte commandCount = data[offset++];
            offset = 12;
            for (int i = 0; i < commandCount && offset < data.Length; i++)
            {
                if (offset + 12 > data.Length) break;
                byte cmdType = data[offset];
                byte channel = data[offset + 1];
                byte flags = data[offset + 2];
                byte reserved = data[offset + 3];
                int cmdSize = (data[offset + 4] << 24) | (data[offset + 5] << 16) |
                              (data[offset + 6] << 8) | data[offset + 7];
                int relSeq = (data[offset + 8] << 24) | (data[offset + 9] << 16) |
                             (data[offset + 10] << 8) | data[offset + 11];
                switch (cmdType)
                {
                    case 1: 
                        offset += 20; 
                        break;
                    case 2: 
                        if (client.VerifyConnectSent)
                        {
                             byte[] verify = BuildVerifyConnect(client.PeerId, data);
                             udp.Send(verify, verify.Length, ep);
                        }
                        else
                        {
                            byte[] verify = BuildVerifyConnect(client.PeerId, data);
                            udp.Send(verify, verify.Length, ep);
                            client.VerifyConnectSent = true;
                        }
                        offset += cmdSize;
                        break;
                    case 5: 
                        byte[] pingAck = BuildAck(client.PeerId, channel, relSeq, data);
                        udp.Send(pingAck, pingAck.Length, ep);
                        client.LastActivityTime = Environment.TickCount;
                        offset += cmdSize;
                        break;
                    case 6: 
                        int payloadStart = offset + 12;
                        if (payloadStart < data.Length && data[payloadStart] == 0xF3)
                        {
                            byte msgType = data[payloadStart + 1];
                            if (msgType == 0x00) 
                            {
                                byte[] ack = BuildAck(client.PeerId, channel, relSeq, data);
                                udp.Send(ack, ack.Length, ep);
                                int seq0 = client.GetNextSequence(0);
                                byte[] initResp = BuildInitResponse(client.PeerId, seq0, data);
                                udp.Send(initResp, initResp.Length, ep);
                            }
                            else if (msgType == 0x02) 
                            {
                                byte opCode = (payloadStart + 2 < data.Length) ? data[payloadStart + 2] : (byte)0;
                                byte[] ack = BuildAck(client.PeerId, channel, relSeq, data);
                                udp.Send(ack, ack.Length, ep);
                                if (opCode == 1) 
                                {
                                    if (client.Connected)
                                    {
                                        int seqCh = client.GetNextSequence(channel);
                                        byte[] opResp = BuildOperationResponse(client.PeerId, seqCh, opCode, channel, data);
                                        udp.Send(opResp, opResp.Length, ep);
                                    }
                                    else
                                    {
                                        string clientIp = ep.Address.ToString();
                                        string deviceId = null;
                                        lock(IpToDeviceId) { IpToDeviceId.TryGetValue(clientIp, out deviceId); }
                                        if (!string.IsNullOrEmpty(deviceId))
                                        {
                                            var user = DatabaseHelper.GetUserByDeviceID(deviceId);
                                            if (user != null)
                                            {
                                                client.PlayerID = user.UserID;
                                                foreach(var otherClient in clients.Values)
                                                {
                                                    if (otherClient != client && otherClient.Connected && otherClient.PlayerID == client.PlayerID)
                                                    {
                                                        if (otherClient.RoomID != 0 && rooms.ContainsKey(otherClient.RoomID))
                                                        {
                                                            Room r = rooms[otherClient.RoomID];
                                                            if (r.Players.Contains(otherClient))
                                                            {
                                                                r.Players.Remove(otherClient);
                                                                List<byte> leavePayload = new List<byte>();
                                                                leavePayload.Add(0xF3); leavePayload.Add(0x04); leavePayload.Add(9); 
                                                                leavePayload.Add(0x00); leavePayload.Add(0x02); 
                                                                leavePayload.Add(0x00); leavePayload.Add(0x69);
                                                                int leavePid = otherClient.PlayerID != 0 ? otherClient.PlayerID : otherClient.PeerId;
                                                                leavePayload.Add((byte)(leavePid >> 24)); leavePayload.Add((byte)(leavePid >> 16)); leavePayload.Add((byte)(leavePid >> 8)); leavePayload.Add((byte)leavePid);
                                                                leavePayload.Add(0x01); leavePayload.Add(0x6F);
                                                                leavePayload.Add(0); 
                                                                BroadcastEvent(udp, leavePayload.ToArray(), otherClient.PeerId, false, 0, otherClient.RoomID);
                                                            }
                                                        }
                                                        otherClient.Connected = false;
                                                        otherClient.RoomID = 0; 
                                                        otherClient.PlayerID = 0; 
                                                    }
                                                }
                                                if (client.PlayerID != 0)
                                            }
                                        }
                                        if (client.PlayerID == 0) client.PlayerID = client.PeerId;
                                        int seqCh = client.GetNextSequence(channel);
                                        byte[] opResp = BuildAuthResponse(client.PeerId, seqCh, channel, data, client.PlayerID, client.Username);
                                        udp.Send(opResp, opResp.Length, ep);
                                        client.Connected = true;
                                    }
                                }
                                else if (opCode == 10 || opCode == 11 || opCode == 13) 
                                {
                                    int desiredMapId = 617; 
                                    int desiredMode = 1; 
                                    string roomName = "Custom Room";
                                    int maxPlayers = 16;
                                    int newRoomId = nextRoomId++;
                                    if (opCode == 11) 
                                    {
                                        desiredMapId = 617; 
                                        desiredMode = 1; 
                                        roomName = "Joined Room"; 
                                        maxPlayers = 16; 
                                        if (rooms.Count > 0) newRoomId = rooms.Last().Key; 
                                    }
                                    Room r;
                                    if (!rooms.ContainsKey(newRoomId))
                                    {
                                        r = new Room();
                                        r.RoomId = newRoomId;
                                        r.OwnerPeerId = client.PeerId;
                                        r.Name = opCode == 10 ? roomName : $"Room {newRoomId}"; 
                                        r.Players.Add(client);
                                        rooms[newRoomId] = r;
                                        client.RoomID = newRoomId;
                                        if (client.PlayerID == 0) client.PlayerID = client.PeerId;
                                    }
                                    else
                                    {
                                        r = rooms[newRoomId];
                                        if (!r.Players.Contains(client)) r.Players.Add(client);
                                        desiredMapId = 1; 
                                        maxPlayers = 16;
                                        client.RoomID = newRoomId;
                                        if (client.PlayerID == 0) client.PlayerID = client.PeerId;
                                    }
                                    if (client.RoomID == 0) client.RoomID = newRoomId; 
                                    if (client.PlayerID == 0) client.PlayerID = client.PeerId;
                                    int seqCh = client.GetNextSequence(channel);
                                    byte[] roomResp = BuildRoomResponse(client.PeerId, seqCh, opCode, channel, data, newRoomId);
                                    udp.Send(roomResp, roomResp.Length, ep);
                                    byte[] internalJoinEvt = BuildEventInternalJoin(client.PeerId, 0, channel, data, client.PlayerID);
                                    BroadcastEvent(udp, internalJoinEvt, client.PeerId, false, channel, newRoomId);
                                    byte[] propsEvt = BuildPlayerPropertiesChangedEvent(client.PeerId, 0, channel, data, client.Team, client.PlayerID, client.Username);
                                    BroadcastEvent(udp, propsEvt, client.PeerId, false, channel, newRoomId);
                                    byte[] joinEvt = BuildEventPlayerJoined(client.PeerId, 0, channel, data, client.PlayerID, client.Username);
                                    BroadcastEvent(udp, joinEvt, client.PeerId, false, channel, newRoomId); 
                                    byte[] connEvt = BuildEventPlayerConnected(client.PeerId, 0, channel, data, client.PlayerID, true);
                                    BroadcastEvent(udp, connEvt, client.PeerId, false, channel, newRoomId); 
                                    int seqGame = client.GetNextSequence(channel);
                                    byte[] gameStateEvt = BuildGameStateEvent(client.PeerId, seqGame, channel, data, r);
                                    udp.Send(gameStateEvt, gameStateEvt.Length, ep);
                                }
                                else if (opCode == 100) 
                                {
                                    int seqCh = client.GetNextSequence(channel);
                                    byte[] opResp = BuildOperationResponse(client.PeerId, seqCh, opCode, channel, data);
                                    udp.Send(opResp, opResp.Length, ep);
                                    string clientIpSync = ep.Address.ToString();
                                    string deviceIdSync = null;
                                    string userNameSync = "Player";
                                    lock(IpToDeviceId) { IpToDeviceId.TryGetValue(clientIpSync, out deviceIdSync); }
                                    if (!string.IsNullOrEmpty(deviceIdSync))
                                    {
                                        var userSync = DatabaseHelper.GetUserByDeviceID(deviceIdSync);
                                        if (userSync != null) userNameSync = userSync.Username;
                                    }
                                    client.Username = userNameSync; 
                                    int seqMulti = client.GetNextSequence(channel);
                                    byte[] multiEvt = BuildInitialSyncMultiEvent(client.PeerId, seqMulti, channel, data, client);
                                    udp.Send(multiEvt, multiEvt.Length, ep);
                                }
                                else if (opCode == 101) 
                                {
                                    int desiredTeam = 1; 
                                    try {
                                        int pOff = 12; 
                                        while(pOff < data.Length - 4) {
                                            if (data[pOff] == 0xF3 && data[pOff+1] == 0x02 && data[pOff+2] == 101) {
                                                int iter = pOff + 5; 
                                                for(int k=0; k<50; k++) {
                                                    if (iter + k + 5 >= data.Length) break;
                                                    if (data[iter+k] == 0x00 && data[iter+k+1] == 0x69) { 
                                                        desiredTeam = (data[iter+k+2] << 24) | (data[iter+k+3] << 16) | (data[iter+k+4] << 8) | data[iter+k+5];
                                                        break;
                                                    }
                                                }
                                                break;
                                            }
                                            pOff++;
                                        }
                                    } catch {}
                                    client.Team = desiredTeam;
                                    int seqCh = client.GetNextSequence(channel);
                                    byte[] opResp = BuildOperationResponse(client.PeerId, seqCh, opCode, channel, data);
                                    udp.Send(opResp, opResp.Length, ep);
                                    byte[] propEvt = BuildPlayerPropertiesChangedEvent(client.PeerId, 0, channel, data, desiredTeam, client.PlayerID, client.Username);
                                    BroadcastEvent(udp, propEvt, client.PeerId, true, channel, client.RoomID); 
                                    if (desiredTeam == 1 || desiredTeam == 2)
                                    {
                                        int seqCharSelf = client.GetNextSequence(channel);
                                        byte[] charEvtSelf = BuildCharacterCreatedEvent(client.PeerId, seqCharSelf, channel, data, desiredTeam, client.PlayerID);
                                        udp.Send(charEvtSelf, charEvtSelf.Length, ep);
                                        byte[] charEvtOthers = BuildCharacterCreatedEvent(client.PeerId, 0, channel, data, desiredTeam, client.PlayerID);
                                        BroadcastEvent(udp, charEvtOthers, client.PeerId, false, channel, client.RoomID); 
                                        int knifeDefId = 103;
                                        int knifeInstId = ++nextWeaponInstanceId;
                                        client.WeaponIDs[knifeDefId] = knifeInstId;
                                        int skinKnife = DatabaseHelper.GetUserLoadout(client.PlayerID, knifeDefId);
                                        byte[] wpnEvt1 = BuildWeaponCreatedEvent(client.PeerId, 0, channel, data, knifeInstId, knifeDefId, skinKnife);
                                        BroadcastEvent(udp, wpnEvt1, client.PeerId, true, channel, client.RoomID);
                                        byte[] acqEvt1 = BuildWeaponAcquiredEvent(client.PeerId, 0, channel, data, knifeInstId, true, client.PlayerID);
                                        BroadcastEvent(udp, acqEvt1, client.PeerId, true, channel, client.RoomID);
                                        int pistolDefId = 21;
                                        int pistolInstId = ++nextWeaponInstanceId;
                                        client.WeaponIDs[pistolDefId] = pistolInstId;
                                        int skinPistol = DatabaseHelper.GetUserLoadout(client.PlayerID, pistolDefId);
                                        byte[] wpnEvt2 = BuildWeaponCreatedEvent(client.PeerId, 0, channel, data, pistolInstId, pistolDefId, skinPistol);
                                        BroadcastEvent(udp, wpnEvt2, client.PeerId, true, channel, client.RoomID);
                                        byte[] acqEvt2 = BuildWeaponAcquiredEvent(client.PeerId, 0, channel, data, pistolInstId, true, client.PlayerID);
                                        BroadcastEvent(udp, acqEvt2, client.PeerId, true, channel, client.RoomID);
                                        int customDefId = 4;
                                        int customInstId = ++nextWeaponInstanceId;
                                        client.WeaponIDs[customDefId] = customInstId;
                                        int skinCustom = DatabaseHelper.GetUserLoadout(client.PlayerID, customDefId);
                                        byte[] wpnEvt3 = BuildWeaponCreatedEvent(client.PeerId, 0, channel, data, customInstId, customDefId, skinCustom);
                                        BroadcastEvent(udp, wpnEvt3, client.PeerId, true, channel, client.RoomID);
                                        byte[] acqEvt3 = BuildWeaponAcquiredEvent(client.PeerId, 0, channel, data, customInstId, true, client.PlayerID);
                                        BroadcastEvent(udp, acqEvt3, client.PeerId, true, channel, client.RoomID);
                                        Room clientRoom = null;
                                        if (rooms.ContainsKey(client.RoomID)) clientRoom = rooms[client.RoomID];
                                        byte[] gameEvt = BuildGameStateEventOnGoing(client.PeerId, 0, channel, data, clientRoom);
                                        BroadcastEvent(udp, gameEvt, client.PeerId, true, channel, client.RoomID);
                                        byte[] stateEvt = BuildCharacterStateUpdateEvent(client.PeerId, 0, channel, data, client.PlayerID);
                                        BroadcastEvent(udp, stateEvt, client.PeerId, true, channel, client.RoomID);
                                    } 
                                }
                                else if(opCode == 21) 
                            {
                                List<byte> roomDataBytes = new List<byte>();
                                byte[] roomListResponse = BuildListRoomsResponse(client.PeerId, client.GetNextSequence(channel), channel, rooms);
                                udp.Send(roomListResponse, roomListResponse.Length, ep);
                            }
                            else
                            {
                                int seqCh = client.GetNextSequence(channel);
                                byte[] opResp = BuildOperationResponse(client.PeerId, seqCh, opCode, channel, data);
                                udp.Send(opResp, opResp.Length, ep);
                            }
                        }
                    }
                    offset += cmdSize;
                    break;
                case 12: 
                        byte[] stAck = BuildAck(client.PeerId, channel, relSeq, data);
                        udp.Send(stAck, stAck.Length, ep);
                        byte[] stResp = BuildServerTimeResponse(client.PeerId, data);
                        udp.Send(stResp, stResp.Length, ep);
                        offset += cmdSize;
                        break;
                    case 4: 
                        byte[] ping4Ack = BuildAck(client.PeerId, channel, relSeq, data);
                        udp.Send(ping4Ack, ping4Ack.Length, ep);
                        client.LastActivityTime = Environment.TickCount;
                        offset += cmdSize;
                        break;
                    case 7: 
                        int payloadStartUr = offset + 16; 
                        int payloadLen = cmdSize - 16; 
                        if (payloadLen > 0 && payloadStartUr + payloadLen <= data.Length)
                        {
                            byte[] urPayload = new byte[payloadLen];
                            Buffer.BlockCopy(data, payloadStartUr, urPayload, 0, payloadLen);
                            try
                            {
                                HandleUnreliable(udp, ep, urPayload, client);
                            }
                            catch (Exception ex)
                            {
                            }
                        }
                        offset += cmdSize;
                        break;
                    default:
                        offset += cmdSize;
                        break;
                }
            }
        }
        static byte[] BuildServerTimeResponse(ushort peerId, byte[] data)
        {
            byte[] packet = new byte[12 + 20]; 
            int offset = 0;
            packet[offset++] = (byte)(peerId >> 8);
            packet[offset++] = (byte)(peerId & 0xFF);
            packet[offset++] = 0x00; 
            packet[offset++] = 0x01; 
            packet[offset++] = 12;   
            packet[offset++] = 0xFF; 
            packet[offset++] = 0x00; 
            packet[offset++] = 0x00; 
            packet[offset++] = 0x00; 
            packet[offset++] = 0x00;
            packet[offset++] = 0x00;
            packet[offset++] = 20;   
            packet[offset++] = 0x00; 
            packet[offset++] = 0x00;
            packet[offset++] = 0x00;
            packet[offset++] = 0x00;
            int time = Environment.TickCount & 0x7FFFFFFF;
            packet[offset++] = (byte)(time >> 24);
            packet[offset++] = (byte)(time >> 16);
            packet[offset++] = (byte)(time >> 8);
            packet[offset++] = (byte)time;
            if (data.Length >= 16) {
                packet[offset++] = data[12];
                packet[offset++] = data[13];
                packet[offset++] = data[14];
                packet[offset++] = data[15];
            } else {
                packet[offset++] = 0x00;
                packet[offset++] = 0x00;
                packet[offset++] = 0x00;
                packet[offset++] = 0x00;
            }
            return packet;
        }
        static byte[] BuildInitResponse(ushort peerId, int seq, byte[] data)
        {
            byte[] payload = new byte[] { 0xF3, 0x01 };
            int cmdSize = 12 + payload.Length; 
            int packetSize = 12 + cmdSize;     
            byte[] packet = new byte[packetSize];
            int offset = 0;
            packet[offset++] = (byte)(peerId >> 8);
            packet[offset++] = (byte)(peerId & 0xFF);
            packet[offset++] = 0x00; 
            packet[offset++] = 0x01; 
            if (data.Length >= 8)
            {
                packet[offset++] = data[4];
                packet[offset++] = data[5];
                packet[offset++] = data[6];
                packet[offset++] = data[7];
            }
            else
            {
                int timeInt = Environment.TickCount & 0x7FFFFFFF;
                packet[offset++] = (byte)(timeInt >> 24);
                packet[offset++] = (byte)(timeInt >> 16);
                packet[offset++] = (byte)(timeInt >> 8);
                packet[offset++] = (byte)timeInt;
            }
            if (data.Length >= 12)
            {
                packet[offset++] = data[8];
                packet[offset++] = data[9];
                packet[offset++] = data[10];
                packet[offset++] = data[11];
            }
            else
            {
                packet[offset++] = 0x00;
                packet[offset++] = 0x00;
                packet[offset++] = 0x00;
                packet[offset++] = 0x01;
            }
            packet[offset++] = 0x06; 
            packet[offset++] = 0x00; 
            packet[offset++] = 0x01; 
            packet[offset++] = 0x04; 
            packet[offset++] = (byte)(cmdSize >> 24);
            packet[offset++] = (byte)(cmdSize >> 16);
            packet[offset++] = (byte)(cmdSize >> 8);
            packet[offset++] = (byte)cmdSize;
            packet[offset++] = (byte)(seq >> 24);
            packet[offset++] = (byte)(seq >> 16);
            packet[offset++] = (byte)(seq >> 8);
            packet[offset++] = (byte)seq;
            Buffer.BlockCopy(payload, 0, packet, offset, payload.Length);
            return packet;
        }
        static byte[] BuildOperationResponse(ushort peerId, int seq, byte opCode, byte channel, byte[] data)
        {
            List<byte> payload = new List<byte>();
            payload.Add(0xF3);       
            payload.Add(0x03);       
            payload.Add(opCode);     
            payload.Add(0x00);       
            payload.Add(0x00);       
            payload.Add(0x2A);       
            payload.Add(0x00);       
            payload.Add(0x00);       
            byte[] payloadBytes = payload.ToArray();
            int cmdSize = 12 + payloadBytes.Length;
            int packetSize = 12 + cmdSize;
            byte[] packet = new byte[packetSize];
            int offset = 0;
            packet[offset++] = (byte)(peerId >> 8);
            packet[offset++] = (byte)(peerId & 0xFF);
            packet[offset++] = 0x00; 
            packet[offset++] = 0x01; 
            if (data.Length >= 8)
            {
                packet[offset++] = data[4];
                packet[offset++] = data[5];
                packet[offset++] = data[6];
                packet[offset++] = data[7];
            }
            else
            {
                int timeInt = Environment.TickCount & 0x7FFFFFFF;
                packet[offset++] = (byte)(timeInt >> 24);
                packet[offset++] = (byte)(timeInt >> 16);
                packet[offset++] = (byte)(timeInt >> 8);
                packet[offset++] = (byte)timeInt;
            }
            if (data.Length >= 12)
            {
                packet[offset++] = data[8];
                packet[offset++] = data[9];
                packet[offset++] = data[10];
                packet[offset++] = data[11];
            }
            else
            {
                packet[offset++] = 0x00;
                packet[offset++] = 0x00;
                packet[offset++] = 0x00;
                packet[offset++] = 0x01;
            }
            packet[offset++] = 0x06; 
            packet[offset++] = channel; 
            packet[offset++] = 0x01; 
            packet[offset++] = 0x04; 
            packet[offset++] = (byte)(cmdSize >> 24);
            packet[offset++] = (byte)(cmdSize >> 16);
            packet[offset++] = (byte)(cmdSize >> 8);
            packet[offset++] = (byte)cmdSize;
            packet[offset++] = (byte)(seq >> 24);
            packet[offset++] = (byte)(seq >> 16);
            packet[offset++] = (byte)(seq >> 8);
            packet[offset++] = (byte)seq;
            Buffer.BlockCopy(payloadBytes, 0, packet, offset, payloadBytes.Length);
            return packet;
        }
        static byte[] BuildAuthResponse(ushort peerId, int seq, byte channel, byte[] data, int playerId, string username)
        {
            List<byte> payload = new List<byte>();
            payload.Add(0xF3);       
            payload.Add(0x03);       
            payload.Add(1);          
            payload.Add(0x00);       
            payload.Add(0x00);       
            payload.Add(0x2A);       
            payload.Add(0x00);
            payload.Add(0x02);
            payload.Add(0x00);       
            payload.Add(0x69);       
            payload.Add((byte)(playerId >> 24));
            payload.Add((byte)(playerId >> 16));
            payload.Add((byte)(playerId >> 8));
            payload.Add((byte)playerId);
            payload.Add(0x01);       
            payload.Add(0x73);       
            byte[] nameBytes = System.Text.Encoding.UTF8.GetBytes(username);
            payload.Add((byte)(nameBytes.Length >> 8));
            payload.Add((byte)nameBytes.Length);
            payload.AddRange(nameBytes);
            byte[] payloadBytes = payload.ToArray();
            int cmdSize = 12 + payloadBytes.Length;
            int packetSize = 12 + cmdSize;
            byte[] packet = new byte[packetSize];
            int offset = 0;
            packet[offset++] = (byte)(peerId >> 8);
            packet[offset++] = (byte)(peerId & 0xFF);
            packet[offset++] = 0x00;
            packet[offset++] = 0x01;
            if (data.Length >= 8) {
                packet[offset++] = data[4]; packet[offset++] = data[5];
                packet[offset++] = data[6]; packet[offset++] = data[7];
            } else {
                int t = Environment.TickCount & 0x7FFFFFFF;
                packet[offset++] = (byte)(t >> 24); packet[offset++] = (byte)(t >> 16);
                packet[offset++] = (byte)(t >> 8); packet[offset++] = (byte)t;
            }
            if (data.Length >= 12) {
                packet[offset++] = data[8]; packet[offset++] = data[9];
                packet[offset++] = data[10]; packet[offset++] = data[11];
            } else {
                packet[offset++] = 0x00; packet[offset++] = 0x00;
                packet[offset++] = 0x00; packet[offset++] = 0x01;
            }
            packet[offset++] = 0x06; 
            packet[offset++] = channel;
            packet[offset++] = 0x01; 
            packet[offset++] = 0x04; 
            packet[offset++] = (byte)(cmdSize >> 24);
            packet[offset++] = (byte)(cmdSize >> 16);
            packet[offset++] = (byte)(cmdSize >> 8);
            packet[offset++] = (byte)cmdSize;
            packet[offset++] = (byte)(seq >> 24);
            packet[offset++] = (byte)(seq >> 16);
            packet[offset++] = (byte)(seq >> 8);
            packet[offset++] = (byte)seq;
            Buffer.BlockCopy(payloadBytes, 0, packet, offset, payloadBytes.Length);
            return packet;
        }
        static byte[] BuildEventPlayerJoined(ushort peerId, int seq, byte channel, byte[] data, int playerId, string username = "")
        {
            List<byte> payload = new List<byte>();
            payload.Add(0xF3);       
            payload.Add(0x04);       
            payload.Add(1);          
            payload.Add(0x00);
            payload.Add(0x01);
            payload.Add(0x00);       
            payload.Add(0x69);       
            payload.Add((byte)(playerId >> 24));
            payload.Add((byte)(playerId >> 16));
            payload.Add((byte)(playerId >> 8));
            payload.Add((byte)playerId);
            byte[] payloadBytes = payload.ToArray();
            int cmdSize = 12 + payloadBytes.Length;
            int packetSize = 12 + cmdSize;
            byte[] packet = new byte[packetSize];
            int offset = 0;
            packet[offset++] = (byte)(peerId >> 8);
            packet[offset++] = (byte)(peerId & 0xFF);
            packet[offset++] = 0x00;
            packet[offset++] = 0x01;
            if (data.Length >= 8) {
                packet[offset++] = data[4]; packet[offset++] = data[5];
                packet[offset++] = data[6]; packet[offset++] = data[7];
            } else {
                int t = Environment.TickCount & 0x7FFFFFFF;
                packet[offset++] = (byte)(t >> 24); packet[offset++] = (byte)(t >> 16);
                packet[offset++] = (byte)(t >> 8); packet[offset++] = (byte)t;
            }
            if (data.Length >= 12) {
                packet[offset++] = data[8]; packet[offset++] = data[9];
                packet[offset++] = data[10]; packet[offset++] = data[11];
            } else {
                packet[offset++] = 0x00; packet[offset++] = 0x00;
                packet[offset++] = 0x00; packet[offset++] = 0x01;
            }
            packet[offset++] = 0x06; packet[offset++] = channel;
            packet[offset++] = 0x01; packet[offset++] = 0x04;
            packet[offset++] = (byte)(cmdSize >> 24); packet[offset++] = (byte)(cmdSize >> 16);
            packet[offset++] = (byte)(cmdSize >> 8); packet[offset++] = (byte)cmdSize;
            packet[offset++] = (byte)(seq >> 24); packet[offset++] = (byte)(seq >> 16);
            packet[offset++] = (byte)(seq >> 8); packet[offset++] = (byte)seq;
            Buffer.BlockCopy(payloadBytes, 0, packet, offset, payloadBytes.Length);
            return packet;
        }
        static byte[] BuildEventPlayerConnected(ushort peerId, int seq, byte channel, byte[] data, int playerId, bool connected)
        {
            List<byte> payload = new List<byte>();
            payload.Add(0xF3);       
            payload.Add(0x04);       
            payload.Add(9);          
            payload.Add(0x00);
            payload.Add(0x02);
            payload.Add(0x00);       
            payload.Add(0x69);       
            payload.Add((byte)(playerId >> 24));
            payload.Add((byte)(playerId >> 16));
            payload.Add((byte)(playerId >> 8));
            payload.Add((byte)playerId);
            payload.Add(0x01);       
            payload.Add(0x6F);       
            payload.Add((byte)(connected ? 1 : 0));
            byte[] payloadBytes = payload.ToArray();
            int cmdSize = 12 + payloadBytes.Length;
            int packetSize = 12 + cmdSize;
            byte[] packet = new byte[packetSize];
            int offset = 0;
            packet[offset++] = (byte)(peerId >> 8);
            packet[offset++] = (byte)(peerId & 0xFF);
            packet[offset++] = 0x00;
            packet[offset++] = 0x01;
            if (data.Length >= 8) {
                packet[offset++] = data[4]; packet[offset++] = data[5];
                packet[offset++] = data[6]; packet[offset++] = data[7];
            } else {
                int t = Environment.TickCount & 0x7FFFFFFF;
                packet[offset++] = (byte)(t >> 24); packet[offset++] = (byte)(t >> 16);
                packet[offset++] = (byte)(t >> 8); packet[offset++] = (byte)t;
            }
            if (data.Length >= 12) {
                packet[offset++] = data[8]; packet[offset++] = data[9];
                packet[offset++] = data[10]; packet[offset++] = data[11];
            } else {
                packet[offset++] = 0x00; packet[offset++] = 0x00;
                packet[offset++] = 0x00; packet[offset++] = 0x01;
            }
            packet[offset++] = 0x06; packet[offset++] = channel;
            packet[offset++] = 0x01; packet[offset++] = 0x04;
            packet[offset++] = (byte)(cmdSize >> 24); packet[offset++] = (byte)(cmdSize >> 16);
            packet[offset++] = (byte)(cmdSize >> 8); packet[offset++] = (byte)cmdSize;
            packet[offset++] = (byte)(seq >> 24); packet[offset++] = (byte)(seq >> 16);
            packet[offset++] = (byte)(seq >> 8); packet[offset++] = (byte)seq;
            Buffer.BlockCopy(payloadBytes, 0, packet, offset, payloadBytes.Length);
            return packet;
        }
        static byte[] BuildEventInternalJoin(ushort peerId, int seq, byte channel, byte[] data, int actorNr)
        {
            List<byte> payload = new List<byte>();
            payload.Add(0xF3);       
            payload.Add(0x04);       
            payload.Add(255);        
            payload.Add(0x00);
            payload.Add(0x02);
            payload.Add(0xFE);
            payload.Add(0x69);
            payload.Add((byte)(actorNr >> 24));
            payload.Add((byte)(actorNr >> 16));
            payload.Add((byte)(actorNr >> 8));
            payload.Add((byte)actorNr);
            payload.Add(0xF9);
            payload.Add(0x68); 
            payload.Add(0x00); 
            payload.Add(0x00); 
            byte[] payloadBytes = payload.ToArray();
            int cmdSize = 12 + payloadBytes.Length;
            int packetSize = 12 + cmdSize;
            byte[] packet = new byte[packetSize];
            int offset = 0;
            packet[offset++] = (byte)(peerId >> 8);
            packet[offset++] = (byte)(peerId & 0xFF);
            packet[offset++] = 0x00;
            packet[offset++] = 0x01;
            if (data.Length >= 8) {
                packet[offset++] = data[4]; packet[offset++] = data[5];
                packet[offset++] = data[6]; packet[offset++] = data[7];
            } else {
                int t = Environment.TickCount & 0x7FFFFFFF;
                packet[offset++] = (byte)(t >> 24); packet[offset++] = (byte)(t >> 16);
                packet[offset++] = (byte)(t >> 8); packet[offset++] = (byte)t;
            }
            if (data.Length >= 12) {
                packet[offset++] = data[8]; packet[offset++] = data[9];
                packet[offset++] = data[10]; packet[offset++] = data[11];
            } else {
                packet[offset++] = 0x00; packet[offset++] = 0x00;
                packet[offset++] = 0x00; packet[offset++] = 0x01;
            }
            packet[offset++] = 0x06; packet[offset++] = channel;
            packet[offset++] = 0x01; packet[offset++] = 0x04;
            packet[offset++] = (byte)(cmdSize >> 24); packet[offset++] = (byte)(cmdSize >> 16);
            packet[offset++] = (byte)(cmdSize >> 8); packet[offset++] = (byte)cmdSize;
            packet[offset++] = (byte)(seq >> 24); packet[offset++] = (byte)(seq >> 16);
            packet[offset++] = (byte)(seq >> 8); packet[offset++] = (byte)seq;
            Buffer.BlockCopy(payloadBytes, 0, packet, offset, payloadBytes.Length);
            return packet;
        }
        static byte[] BuildRoomResponse(ushort peerId, int seq, byte opCode, byte channel, byte[] data, int roomId = 1)
        {
            List<byte> payload = new List<byte>();
            payload.Add(0xF3);       
            payload.Add(0x03);       
            payload.Add(opCode);     
            payload.Add(0x00);       
            payload.Add(0x00);       
            payload.Add(0x2A);       
            payload.Add(0x00);       
            payload.Add(0x02);       
            payload.Add(0x00);       
            payload.Add(0x69);       
            payload.Add((byte)(roomId >> 24));
            payload.Add((byte)(roomId >> 16));
            payload.Add((byte)(roomId >> 8));
            payload.Add((byte)roomId);
            payload.Add(0x01);       
            payload.Add(0x44);       
            payload.Add(0x62);       
            payload.Add(0x00);       
            payload.Add(0x00);       
            payload.Add(0x06);       
            payload.Add(0x00);       
            payload.Add(0x73);       
            string roomName = "TestRoom";
            byte[] nameBytes = System.Text.Encoding.UTF8.GetBytes(roomName);
            payload.Add((byte)(nameBytes.Length >> 8));
            payload.Add((byte)nameBytes.Length);
            payload.AddRange(nameBytes);
            payload.Add(0x01);       
            payload.Add(0x62);       
            payload.Add(0x0A);       
            payload.Add(0x02);       
            payload.Add(0x73);       
            payload.Add(0x00);       
            payload.Add(0x00);       
            payload.Add(0x03);       
            payload.Add(0x69);       
            int mapId = 617;         
            payload.Add((byte)(mapId >> 24));
            payload.Add((byte)(mapId >> 16));
            payload.Add((byte)(mapId >> 8));
            payload.Add((byte)mapId);
            payload.Add(0x04);       
            payload.Add(0x62);       
            payload.Add(0x00);       
            payload.Add(0x05);       
            payload.Add(0x69);       
            int gameType = 0;        
            payload.Add((byte)(gameType >> 24));
            payload.Add((byte)(gameType >> 16));
            payload.Add((byte)(gameType >> 8));
            payload.Add((byte)gameType);
            byte[] payloadBytes = payload.ToArray();
            int cmdSize = 12 + payloadBytes.Length;
            int packetSize = 12 + cmdSize;
            byte[] packet = new byte[packetSize];
            int offset = 0;
            packet[offset++] = (byte)(peerId >> 8);
            packet[offset++] = (byte)(peerId & 0xFF);
            packet[offset++] = 0x00; 
            packet[offset++] = 0x01; 
            if (data.Length >= 8)
            {
                packet[offset++] = data[4];
                packet[offset++] = data[5];
                packet[offset++] = data[6];
                packet[offset++] = data[7];
            }
            else
            {
                int timeInt = Environment.TickCount & 0x7FFFFFFF;
                packet[offset++] = (byte)(timeInt >> 24);
                packet[offset++] = (byte)(timeInt >> 16);
                packet[offset++] = (byte)(timeInt >> 8);
                packet[offset++] = (byte)timeInt;
            }
            if (data.Length >= 12)
            {
                packet[offset++] = data[8];
                packet[offset++] = data[9];
                packet[offset++] = data[10];
                packet[offset++] = data[11];
            }
            else
            {
                packet[offset++] = 0x00;
                packet[offset++] = 0x00;
                packet[offset++] = 0x00;
                packet[offset++] = 0x01;
            }
            packet[offset++] = 0x06; 
            packet[offset++] = channel;
            packet[offset++] = 0x01; 
            packet[offset++] = 0x04; 
            packet[offset++] = (byte)(cmdSize >> 24);
            packet[offset++] = (byte)(cmdSize >> 16);
            packet[offset++] = (byte)(cmdSize >> 8);
            packet[offset++] = (byte)cmdSize;
            packet[offset++] = (byte)(seq >> 24);
            packet[offset++] = (byte)(seq >> 16);
            packet[offset++] = (byte)(seq >> 8);
            packet[offset++] = (byte)seq;
            Buffer.BlockCopy(payloadBytes, 0, packet, offset, payloadBytes.Length);
            return packet;
        }
        static byte[] BuildListRoomsResponse(ushort peerId, int seq, byte channel, Dictionary<int, Room> activeRooms)
        {
            List<byte> msgPack = new List<byte>();
            int count = activeRooms.Count;
            if (count <= 15) msgPack.Add((byte)(0x90 | count));
            else if (count <= 65535) { msgPack.Add(0xdc); msgPack.Add((byte)(count >> 8)); msgPack.Add((byte)count); }
            else { msgPack.Add(0xdd); msgPack.AddRange(BitConverter.GetBytes(count).Reverse().ToArray()); } 
            foreach(var kvp in activeRooms)
            {
                Room r = kvp.Value;
                int fieldCount = 7;
                msgPack.Add((byte)(0x80 | fieldCount)); 
                msgPack.Add(0x00); 
                PackInt(msgPack, r.RoomId);
                msgPack.Add(0x01); 
                PackString(msgPack, r.Name);
                msgPack.Add(0x02); 
                PackInt(msgPack, r.Players.Count);
                msgPack.Add(0x03); 
                PackInt(msgPack, 16);
                msgPack.Add(0x04); 
                PackInt(msgPack, 617); 
                msgPack.Add(0x05); 
                PackInt(msgPack, 0); 
                msgPack.Add(0x06); 
                PackBool(msgPack, false);
            }
            byte[] roomDataBytes = msgPack.ToArray();
            List<byte> payload = new List<byte>();
            payload.Add(0xF3);       
            payload.Add(0x03);       
            payload.Add(21);         
            payload.Add(0x00);       
            payload.Add(0x00);       
            payload.Add(0x2A);       
            payload.Add(0x00); payload.Add(0x01);
            payload.Add(0x00); 
            payload.Add(0x78); 
            int blobLen = roomDataBytes.Length;
            payload.Add((byte)(blobLen >> 24)); payload.Add((byte)(blobLen >> 16)); payload.Add((byte)(blobLen >> 8)); payload.Add((byte)blobLen);
            payload.AddRange(roomDataBytes);
            byte[] payloadBytes = payload.ToArray();
            int cmdSize = 12 + payloadBytes.Length;
            int packetSize = 12 + cmdSize; 
            byte[] packet = new byte[packetSize];
            int offset = 0;
            packet[offset++] = (byte)(peerId >> 8);
            packet[offset++] = (byte)(peerId & 0xFF);
            packet[offset++] = 0x00; 
            packet[offset++] = 0x01; 
            int timeInt = Environment.TickCount & 0x7FFFFFFF;
            packet[offset++] = (byte)(timeInt >> 24);
            packet[offset++] = (byte)(timeInt >> 16);
            packet[offset++] = (byte)(timeInt >> 8);
            packet[offset++] = (byte)timeInt;
            packet[offset++] = 0x00;
            packet[offset++] = 0x00;
            packet[offset++] = 0x00;
            packet[offset++] = 0x01;
            packet[offset++] = 0x06; 
            packet[offset++] = channel;
            packet[offset++] = 0x01; 
            packet[offset++] = 0x04; 
            packet[offset++] = (byte)(cmdSize >> 24);
            packet[offset++] = (byte)(cmdSize >> 16);
            packet[offset++] = (byte)(cmdSize >> 8);
            packet[offset++] = (byte)cmdSize;
            packet[offset++] = (byte)(seq >> 24);
            packet[offset++] = (byte)(seq >> 16);
            packet[offset++] = (byte)(seq >> 8);
            packet[offset++] = (byte)seq;
            Buffer.BlockCopy(payloadBytes, 0, packet, offset, payloadBytes.Length);
            return packet;
        }
        static void PackInt(List<byte> buf, int val)
        {
            if (val >= 0 && val <= 127) { buf.Add((byte)val); }
            else if (val >= -32 && val <= -1) { buf.Add((byte)val); }
            else if (val >= 0 && val <= 255) { buf.Add(0xcc); buf.Add((byte)val); }
            else if (val >= -128 && val <= 127) { buf.Add(0xd0); buf.Add((byte)val); }
            else if (val >= 0 && val <= 65535) { buf.Add(0xcd); buf.Add((byte)(val >> 8)); buf.Add((byte)val); }
            else if (val >= -32768 && val <= 32767) { buf.Add(0xd1); buf.Add((byte)(val >> 8)); buf.Add((byte)val); }
            else 
            { 
                buf.Add(0xd2); 
                buf.Add((byte)(val >> 24)); buf.Add((byte)(val >> 16)); buf.Add((byte)(val >> 8)); buf.Add((byte)val); 
            }
        }
        static void PackString(List<byte> buf, string val)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(val);
            int len = bytes.Length;
            if (len <= 31) buf.Add((byte)(0xa0 | len));
            else if (len <= 255) { buf.Add(0xd9); buf.Add((byte)len); }
            else if (len <= 65535) { buf.Add(0xda); buf.Add((byte)(len >> 8)); buf.Add((byte)len); }
            else { buf.Add(0xdb); buf.Add((byte)(len >> 24)); buf.Add((byte)(len >> 16)); buf.Add((byte)(len >> 8)); buf.Add((byte)len); }
            buf.AddRange(bytes);
        }
        static void PackBool(List<byte> buf, bool val)
        {
            buf.Add(val ? (byte)0xc3 : (byte)0xc2);
        }
        static byte[] BuildActorConnectedEvent(ushort peerId, int seq, byte channel, byte[] data, int roomId = 1)
        {
            List<byte> payload = new List<byte>();
            payload.Add(0xF3);       
            payload.Add(0x04);       
            payload.Add(8);          
            payload.Add(0x00);       
            payload.Add(0x03);       
            payload.Add(0x00);       
            payload.Add(0x69);       
            int serverId = 1;        
            payload.Add((byte)(serverId >> 24));
            payload.Add((byte)(serverId >> 16));
            payload.Add((byte)(serverId >> 8));
            payload.Add((byte)serverId);
            payload.Add(0x01);       
            payload.Add(0x69);       
            payload.Add((byte)(roomId >> 24));
            payload.Add((byte)(roomId >> 16));
            payload.Add((byte)(roomId >> 8));
            payload.Add((byte)roomId);
            payload.Add(0x02);       
            payload.Add(0x44);       
            payload.Add(0x62);       
            payload.Add(0x00);       
            payload.Add(0x00);       
            payload.Add(0x06);       
            payload.Add(0x00);       
            payload.Add(0x73);       
            string roomName = "TestRoom";
            byte[] roomNameBytes = System.Text.Encoding.UTF8.GetBytes(roomName);
            payload.Add(0x00);
            payload.Add((byte)roomNameBytes.Length);
            payload.AddRange(roomNameBytes);
            payload.Add(0x01);       
            payload.Add(0x62);       
            payload.Add(10);         
            payload.Add(0x02);       
            payload.Add(0x73);       
            payload.Add(0x00);
            payload.Add(0x00);       
            payload.Add(0x03);       
            payload.Add(0x69);       
            int mapId = 617;         
            payload.Add((byte)(mapId >> 24));
            payload.Add((byte)(mapId >> 16));
            payload.Add((byte)(mapId >> 8));
            payload.Add((byte)mapId);
            payload.Add(0x04);       
            payload.Add(0x62);       
            payload.Add(0x01);       
            payload.Add(0x05);       
            payload.Add(0x69);       
            int gameType = 0;        
            payload.Add((byte)(gameType >> 24));
            payload.Add((byte)(gameType >> 16));
            payload.Add((byte)(gameType >> 8));
            payload.Add((byte)gameType);
            byte[] payloadBytes = payload.ToArray();
            int cmdSize = 12 + payloadBytes.Length;
            int packetSize = 12 + cmdSize;
            byte[] packet = new byte[packetSize];
            int offset = 0;
            packet[offset++] = (byte)(peerId >> 8);
            packet[offset++] = (byte)(peerId & 0xFF);
            packet[offset++] = 0x00;
            packet[offset++] = 0x01;
            if (data.Length >= 8)
            {
                packet[offset++] = data[4];
                packet[offset++] = data[5];
                packet[offset++] = data[6];
                packet[offset++] = data[7];
            }
            else
            {
                int timeInt = Environment.TickCount & 0x7FFFFFFF;
                packet[offset++] = (byte)(timeInt >> 24);
                packet[offset++] = (byte)(timeInt >> 16);
                packet[offset++] = (byte)(timeInt >> 8);
                packet[offset++] = (byte)timeInt;
            }
            if (data.Length >= 12)
            {
                packet[offset++] = data[8];
                packet[offset++] = data[9];
                packet[offset++] = data[10];
                packet[offset++] = data[11];
            }
            else
            {
                packet[offset++] = 0x00;
                packet[offset++] = 0x00;
                packet[offset++] = 0x00;
                packet[offset++] = 0x01;
            }
            packet[offset++] = 0x06;
            packet[offset++] = channel;
            packet[offset++] = 0x01;
            packet[offset++] = 0x04;
            packet[offset++] = (byte)(cmdSize >> 24);
            packet[offset++] = (byte)(cmdSize >> 16);
            packet[offset++] = (byte)(cmdSize >> 8);
            packet[offset++] = (byte)cmdSize;
            packet[offset++] = (byte)(seq >> 24);
            packet[offset++] = (byte)(seq >> 16);
            packet[offset++] = (byte)(seq >> 8);
            packet[offset++] = (byte)seq;
            Buffer.BlockCopy(payloadBytes, 0, packet, offset, payloadBytes.Length);
            return packet;
        }
        static List<byte> GetCharacterCreatedParams(int playerID, int team)
        {
            List<byte> p = new List<byte>();
            p.Add(0x00); p.Add(0x69);
            p.Add((byte)(playerID >> 24)); p.Add((byte)(playerID >> 16)); p.Add((byte)(playerID >> 8)); p.Add((byte)playerID);
            p.Add(0x01); p.Add(0x78);
            float spawnX = (team == 1) ? -0.12f : -0.12f;
            float spawnY = 0.03f;
            float spawnZ = (team == 1) ? 0.22f : 0.22f;
            short posXShort = (short)(spawnX * 100);
            short posYShort = (short)(spawnY * 100);
            short posZShort = (short)(spawnZ * 100);
            short pitch = 0;
            short yaw = (short)((team == 1) ? 0 : 180 * 100);
            byte[] posData = new byte[19];
            posData[0] = (byte)(posXShort >> 8); posData[1] = (byte)posXShort;
            posData[2] = (byte)(posYShort >> 8); posData[3] = (byte)posYShort;
            posData[4] = (byte)(posZShort >> 8); posData[5] = (byte)posZShort;
            posData[6] = (byte)(pitch >> 8); posData[7] = (byte)pitch;
            posData[8] = (byte)(yaw >> 8); posData[9] = (byte)yaw;
            p.Add((byte)(posData.Length >> 24)); p.Add((byte)(posData.Length >> 16));
            p.Add((byte)(posData.Length >> 8)); p.Add((byte)posData.Length);
            p.AddRange(posData);
            return p;
        }
        static List<byte> GetWeaponCreatedParams(int weaponID, int weaponDefID, int skinID)
        {
            List<byte> p = new List<byte>();
            p.Add(0x00); p.Add(0x69);
            p.Add((byte)(weaponID >> 24)); p.Add((byte)(weaponID >> 16)); p.Add((byte)(weaponID >> 8)); p.Add((byte)weaponID);
            p.Add(0x01); p.Add(0x69);
            p.Add((byte)(weaponDefID >> 24)); p.Add((byte)(weaponDefID >> 16)); p.Add((byte)(weaponDefID >> 8)); p.Add((byte)weaponDefID);
            p.Add(0x03); p.Add(0x69);
            p.Add((byte)(skinID >> 24)); p.Add((byte)(skinID >> 16)); p.Add((byte)(skinID >> 8)); p.Add((byte)skinID);
            return p; 
        }
        static List<byte> GetWeaponAcquiredParams(int playerID, int weaponID, bool clientEquip = true)
        {
            List<byte> p = new List<byte>();
            p.Add(0x00); p.Add(0x69);
            p.Add((byte)(playerID >> 24)); p.Add((byte)(playerID >> 16)); p.Add((byte)(playerID >> 8)); p.Add((byte)playerID);
            p.Add(0x01); p.Add(0x69);
            p.Add((byte)(weaponID >> 24)); p.Add((byte)(weaponID >> 16)); p.Add((byte)(weaponID >> 8)); p.Add((byte)weaponID);
            p.Add(0x02); p.Add(0x6F);
            p.Add((byte)(clientEquip ? 1 : 0));
            return p;
        }
        static Dictionary<byte, object> GetCharacterStateUpdateParams(int playerID)
        {
            Dictionary<byte, object> parameters = new Dictionary<byte, object>();
            parameters.Add(0, playerID);
            parameters.Add(3, false); 
            return parameters;
        }
        static byte[] BuildInitialSyncMultiEvent(ushort peerId, int seq, byte channel, byte[] data, ClientState requestingClient)
        {
            List<byte> payload = new List<byte>();
            payload.Add(0xF3);       
            payload.Add(0x04);       
            payload.Add(6);          
            payload.Add(0x00);
            payload.Add(0x02);
            Room r = null;
            if (requestingClient.RoomID > 0 && rooms.ContainsKey(requestingClient.RoomID))
                r = rooms[requestingClient.RoomID];
            List<ClientState> playersToSync = new List<ClientState>();
            if (r != null) playersToSync.AddRange(r.Players);
            else playersToSync.Add(requestingClient); 
            int totalEvents = 1; 
            var eventsToSend = new List<(byte Code, List<byte> Params)>();
            List<byte> roomPropsParams = new List<byte>();
            roomPropsParams.Add(0x00); roomPropsParams.Add(0x73); 
            string roomName = "CustomPlayer Rooms";
            byte[] rNameB = System.Text.Encoding.UTF8.GetBytes(roomName);
            roomPropsParams.Add((byte)(rNameB.Length >> 8)); roomPropsParams.Add((byte)rNameB.Length); roomPropsParams.AddRange(rNameB);
            roomPropsParams.Add(0x01); roomPropsParams.Add(0x62); roomPropsParams.Add(10);
            roomPropsParams.Add(0x02); roomPropsParams.Add(0x73); roomPropsParams.Add(0x00); roomPropsParams.Add(0x00);
            roomPropsParams.Add(0x03); roomPropsParams.Add(0x69);
            int mapId = 617; 
            roomPropsParams.Add((byte)(mapId >> 24)); roomPropsParams.Add((byte)(mapId >> 16)); roomPropsParams.Add((byte)(mapId >> 8)); roomPropsParams.Add((byte)mapId);
            roomPropsParams.Add(0x04); roomPropsParams.Add(0x62); roomPropsParams.Add(1);
            roomPropsParams.Add(0x05); roomPropsParams.Add(0x69);
            roomPropsParams.Add(0x00); roomPropsParams.Add(0x00); roomPropsParams.Add(0x00); roomPropsParams.Add(0x00);
            List<byte> roomDict = new List<byte>();
            roomDict.Add(0x44); roomDict.Add(0x62); roomDict.Add(0x00); roomDict.Add(0x00); roomDict.Add(0x06);
            roomDict.AddRange(roomPropsParams);
            eventsToSend.Add((4, roomDict));
            foreach(var p in playersToSync)
            {
                int pid = (p.PlayerID != 0) ? p.PlayerID : p.PeerId;
                string uName = !string.IsNullOrEmpty(p.Username) ? p.Username : "Player " + pid;
                List<byte> pp = new List<byte>();
                pp.Add(0xFF); pp.Add(0x69);
                pp.Add((byte)(pid >> 24)); pp.Add((byte)(pid >> 16)); pp.Add((byte)(pid >> 8)); pp.Add((byte)pid);
                pp.Add(0x00); pp.Add(0x73);
                byte[] uNameB = System.Text.Encoding.UTF8.GetBytes(uName);
                pp.Add((byte)(uNameB.Length >> 8)); pp.Add((byte)uNameB.Length); pp.AddRange(uNameB);
                pp.Add(0x02); pp.Add(0x69);
                int t = p.Team;
                pp.Add((byte)(t >> 24)); pp.Add((byte)(t >> 16)); pp.Add((byte)(t >> 8)); pp.Add((byte)t);
                List<byte> ppDict = new List<byte>();
                ppDict.Add(0x44); ppDict.Add(0x62); ppDict.Add(0x00); ppDict.Add(0x00); ppDict.Add(0x03);
                ppDict.AddRange(pp);
                eventsToSend.Add((3, ppDict));
                if (p.Team == 1 || p.Team == 2)
                {
                    List<byte> charParams = GetCharacterCreatedParams(pid, p.Team);
                    List<byte> charDict = new List<byte>();
                    charDict.Add(0x44); charDict.Add(0x62); charDict.Add(0x00); charDict.Add(0x00); charDict.Add(0x02); 
                    charDict.AddRange(charParams);
                    eventsToSend.Add((10, charDict));
                    int knifeDefId = 103; int pistolDefId = 21; 
                    int skinKnife = DatabaseHelper.GetUserLoadout(pid, knifeDefId);
                    int skinPistol = DatabaseHelper.GetUserLoadout(pid, pistolDefId);
                    int wpn1Inst = 0; 
                    if (p.WeaponIDs.ContainsKey(knifeDefId)) wpn1Inst = p.WeaponIDs[knifeDefId];
                    else { wpn1Inst = ++nextWeaponInstanceId; p.WeaponIDs[knifeDefId] = wpn1Inst; }
                    int wpn2Inst = 0;
                    if (p.WeaponIDs.ContainsKey(pistolDefId)) wpn2Inst = p.WeaponIDs[pistolDefId];
                    else { wpn2Inst = ++nextWeaponInstanceId; p.WeaponIDs[pistolDefId] = wpn2Inst; }
                    List<byte> wpn1Params = GetWeaponCreatedParams(wpn1Inst, knifeDefId, skinKnife);
                    List<byte> wpn1Dict = new List<byte>();
                    wpn1Dict.Add(0x44); wpn1Dict.Add(0x62); wpn1Dict.Add(0x00); wpn1Dict.Add(0x00); wpn1Dict.Add(0x03);
                    wpn1Dict.AddRange(wpn1Params);
                    eventsToSend.Add((30, wpn1Dict));
                    List<byte> acq1Params = GetWeaponAcquiredParams(pid, wpn1Inst, true);
                    List<byte> acq1Dict = new List<byte>();
                    acq1Dict.Add(0x44); acq1Dict.Add(0x62); acq1Dict.Add(0x00); acq1Dict.Add(0x00); acq1Dict.Add(0x03);
                    acq1Dict.AddRange(acq1Params);
                    eventsToSend.Add((31, acq1Dict));
                    List<byte> wpn2Params = GetWeaponCreatedParams(wpn2Inst, pistolDefId, skinPistol);
                    List<byte> wpn2Dict = new List<byte>();
                    wpn2Dict.Add(0x44); wpn2Dict.Add(0x62); wpn2Dict.Add(0x00); wpn2Dict.Add(0x00); wpn2Dict.Add(0x03);
                    wpn2Dict.AddRange(wpn2Params);
                    eventsToSend.Add((30, wpn2Dict));
                    List<byte> acq2Params = GetWeaponAcquiredParams(pid, wpn2Inst, true);
                    List<byte> acq2Dict = new List<byte>();
                    acq2Dict.Add(0x44); acq2Dict.Add(0x62); acq2Dict.Add(0x00); acq2Dict.Add(0x00); acq2Dict.Add(0x03);
                    acq2Dict.AddRange(acq2Params);
                    eventsToSend.Add((31, acq2Dict));
                    var stateParams = GetCharacterStateUpdateParams(pid);
                    List<byte> stateDict = new List<byte>();
                    stateDict.Add(0x44); stateDict.Add(0x62); stateDict.Add(0x00); stateDict.Add(0x00); stateDict.Add((byte)stateParams.Count);
                    foreach(var kvp in stateParams)
                    {
                        stateDict.Add(kvp.Key);
                        object val = kvp.Value;
                        if (val is int iVal) { stateDict.Add(0x69); stateDict.Add((byte)(iVal >> 24)); stateDict.Add((byte)(iVal >> 16)); stateDict.Add((byte)(iVal >> 8)); stateDict.Add((byte)iVal); }
                        else if (val is bool bVal) { stateDict.Add(0x6F); stateDict.Add((byte)(bVal ? 1 : 0)); }
                    }
                    eventsToSend.Add((12, stateDict));
                }
            }
            payload.Add(0x00);       
            payload.Add(0x7A);       
            int finalCount = eventsToSend.Count;
            payload.Add((byte)(finalCount >> 8)); 
            payload.Add((byte)(finalCount));       
            foreach(var evt in eventsToSend)
            {
                payload.Add(0x62); payload.Add(evt.Code);
            }
            payload.Add(0x01);       
            payload.Add(0x7A);       
            payload.Add((byte)(finalCount >> 8));
            payload.Add((byte)(finalCount));       
            foreach(var evt in eventsToSend)
            {
                payload.AddRange(evt.Params); 
            }
            byte[] payloadBytes = payload.ToArray();            
            int cmdSize = 12 + payloadBytes.Length;
            int packetSize = 12 + cmdSize;
            byte[] packet = new byte[packetSize];
            int offset = 0;
            packet[offset++] = (byte)(peerId >> 8);
            packet[offset++] = (byte)(peerId & 0xFF);
            packet[offset++] = 0x00;
            packet[offset++] = 0x01;
            if (data.Length >= 8) {
                packet[offset++] = data[4]; packet[offset++] = data[5];
                packet[offset++] = data[6]; packet[offset++] = data[7];
            } else {
                int t = Environment.TickCount & 0x7FFFFFFF;
                packet[offset++] = (byte)(t >> 24); packet[offset++] = (byte)(t >> 16);
                packet[offset++] = (byte)(t >> 8); packet[offset++] = (byte)t;
            }
            if (data.Length >= 12) {
                packet[offset++] = data[8]; packet[offset++] = data[9];
                packet[offset++] = data[10]; packet[offset++] = data[11];
            } else {
                packet[offset++] = 0x00; packet[offset++] = 0x00;
                packet[offset++] = 0x00; packet[offset++] = 0x01;
            }
            packet[offset++] = 0x06; 
            packet[offset++] = channel;
            packet[offset++] = 0x01; 
            packet[offset++] = 0x04; 
            packet[offset++] = (byte)(cmdSize >> 24);
            packet[offset++] = (byte)(cmdSize >> 16);
            packet[offset++] = (byte)(cmdSize >> 8);
            packet[offset++] = (byte)cmdSize;
            packet[offset++] = (byte)(seq >> 24);
            packet[offset++] = (byte)(seq >> 16);
            packet[offset++] = (byte)(seq >> 8);
            packet[offset++] = (byte)seq;
            Buffer.BlockCopy(payloadBytes, 0, packet, offset, payloadBytes.Length);
            return packet;
        }
        static byte[] BuildPlayerPropertiesChangedEvent(ushort peerId, int seq, byte channel, byte[] data, int team, int playerID, string username = "")
        {
            List<byte> payload = new List<byte>();
            payload.Add(0xF3);       
            payload.Add(0x04);       
            payload.Add(3);          
            payload.Add(0x00);       
            payload.Add(0x03);       
            payload.Add(0xFF);
            payload.Add(0x69);       
            payload.Add((byte)(playerID >> 24));
            payload.Add((byte)(playerID >> 16));
            payload.Add((byte)(playerID >> 8));
            payload.Add((byte)playerID);
            payload.Add(0x00);
            payload.Add(0x73);       
            string uName = !string.IsNullOrEmpty(username) ? username : "Player " + playerID;
            byte[] uNameB = System.Text.Encoding.UTF8.GetBytes(uName);
            payload.Add((byte)(uNameB.Length >> 8));
            payload.Add((byte)uNameB.Length);
            payload.AddRange(uNameB);
            payload.Add(0x02);
            payload.Add(0x69);       
            payload.Add((byte)(team >> 24));
            payload.Add((byte)(team >> 16));
            payload.Add((byte)(team >> 8));
            payload.Add((byte)team);
            byte[] payloadBytes = payload.ToArray();
            int cmdSize = 12 + payloadBytes.Length;
            int packetSize = 12 + cmdSize;
            byte[] packet = new byte[packetSize];
            int offset = 0;
            packet[offset++] = (byte)(peerId >> 8);
            packet[offset++] = (byte)(peerId & 0xFF);
            packet[offset++] = 0x00;
            packet[offset++] = 0x01;
            if (data.Length >= 8) {
                packet[offset++] = data[4]; packet[offset++] = data[5];
                packet[offset++] = data[6]; packet[offset++] = data[7];
            } else {
                int t = Environment.TickCount & 0x7FFFFFFF;
                packet[offset++] = (byte)(t >> 24); packet[offset++] = (byte)(t >> 16);
                packet[offset++] = (byte)(t >> 8); packet[offset++] = (byte)t;
            }
            if (data.Length >= 12) {
                packet[offset++] = data[8]; packet[offset++] = data[9];
                packet[offset++] = data[10]; packet[offset++] = data[11];
            } else {
                packet[offset++] = 0x00; packet[offset++] = 0x00;
                packet[offset++] = 0x00; packet[offset++] = 0x01;
            }
            packet[offset++] = 0x06; packet[offset++] = channel;
            packet[offset++] = 0x01; packet[offset++] = 0x04;
            packet[offset++] = (byte)(cmdSize >> 24); packet[offset++] = (byte)(cmdSize >> 16);
            packet[offset++] = (byte)(cmdSize >> 8); packet[offset++] = (byte)cmdSize;
            packet[offset++] = (byte)(seq >> 24); packet[offset++] = (byte)(seq >> 16);
            packet[offset++] = (byte)(seq >> 8); packet[offset++] = (byte)seq;
            Buffer.BlockCopy(payloadBytes, 0, packet, offset, payloadBytes.Length);
            return packet;
        }
        static byte[] BuildCharacterCreatedEvent(ushort peerId, int seq, byte channel, byte[] data, int team, int playerID)
        {
            List<byte> payload = new List<byte>();
            payload.Add(0xF3);       
            payload.Add(0x04);       
            payload.Add(10);         
            payload.Add(0x00);       
            payload.Add(0x02);       
            payload.Add(0x00);
            payload.Add(0x69);       
            payload.Add((byte)(playerID >> 24));
            payload.Add((byte)(playerID >> 16));
            payload.Add((byte)(playerID >> 8));
            payload.Add((byte)playerID);
            payload.Add(0x01);
            payload.Add(0x78);       
            byte[] posData = new byte[19];
            float spawnX = (team == 1) ? -0.12f : -0.12f;  
            float spawnY = 0.03f;  
            float spawnZ = (team == 1) ? 0.22f : 0.22f;
            short posXShort = (short)(spawnX * 100);
            short posYShort = (short)(spawnY * 100);
            short posZShort = (short)(spawnZ * 100);
            short pitch = 0;
            short yaw = (short)((team == 1) ? 0 : 180 * 100); 
            posData[0] = (byte)(posXShort >> 8); posData[1] = (byte)posXShort;
            posData[2] = (byte)(posYShort >> 8); posData[3] = (byte)posYShort;
            posData[4] = (byte)(posZShort >> 8); posData[5] = (byte)posZShort;
            posData[6] = (byte)(pitch >> 8); posData[7] = (byte)pitch;
            posData[8] = (byte)(yaw >> 8); posData[9] = (byte)yaw;
            payload.Add((byte)(posData.Length >> 24));
            payload.Add((byte)(posData.Length >> 16));
            payload.Add((byte)(posData.Length >> 8));
            payload.Add((byte)posData.Length);
            payload.AddRange(posData);
            byte[] payloadBytes = payload.ToArray();
            int cmdSize = 12 + payloadBytes.Length;
            int packetSize = 12 + cmdSize;
            byte[] packet = new byte[packetSize];
            int offset = 0;
            packet[offset++] = (byte)(peerId >> 8);
            packet[offset++] = (byte)(peerId & 0xFF);
            packet[offset++] = 0x00;
            packet[offset++] = 0x01;
            if (data.Length >= 8) {
                packet[offset++] = data[4]; packet[offset++] = data[5];
                packet[offset++] = data[6]; packet[offset++] = data[7];
            } else {
                int t = Environment.TickCount & 0x7FFFFFFF;
                packet[offset++] = (byte)(t >> 24); packet[offset++] = (byte)(t >> 16);
                packet[offset++] = (byte)(t >> 8); packet[offset++] = (byte)t;
            }
            if (data.Length >= 12) {
                packet[offset++] = data[8]; packet[offset++] = data[9];
                packet[offset++] = data[10]; packet[offset++] = data[11];
            } else {
                packet[offset++] = 0x00; packet[offset++] = 0x00;
                packet[offset++] = 0x00; packet[offset++] = 0x01;
            }
            packet[offset++] = 0x06; packet[offset++] = channel;
            packet[offset++] = 0x01; packet[offset++] = 0x04;
            packet[offset++] = (byte)(cmdSize >> 24); packet[offset++] = (byte)(cmdSize >> 16);
            packet[offset++] = (byte)(cmdSize >> 8); packet[offset++] = (byte)cmdSize;
            packet[offset++] = (byte)(seq >> 24); packet[offset++] = (byte)(seq >> 16);
            packet[offset++] = (byte)(seq >> 8); packet[offset++] = (byte)seq;
            Buffer.BlockCopy(payloadBytes, 0, packet, offset, payloadBytes.Length);
            return packet;
        }
        static byte[] BuildWeaponCreatedEvent(ushort peerId, int seq, byte channel, byte[] data, int weaponID, int weaponDefID, int skinID)
        {
            List<byte> payload = new List<byte>();
            payload.Add(0xF3);       
            payload.Add(0x04);       
            payload.Add(30);         
            payload.Add(0x00);       
            payload.Add(0x03);       
            payload.Add(0x00);
            payload.Add(0x69);       
            payload.Add((byte)(weaponID >> 24));
            payload.Add((byte)(weaponID >> 16));
            payload.Add((byte)(weaponID >> 8));
            payload.Add((byte)weaponID);
            payload.Add(0x01);
            payload.Add(0x69);       
            payload.Add((byte)(weaponDefID >> 24));
            payload.Add((byte)(weaponDefID >> 16));
            payload.Add((byte)(weaponDefID >> 8));
            payload.Add((byte)weaponDefID);
            payload.Add(0x03);
            payload.Add(0x69);       
            payload.Add((byte)(skinID >> 24));
            payload.Add((byte)(skinID >> 16));
            payload.Add((byte)(skinID >> 8));
            payload.Add((byte)skinID);
            byte[] payloadBytes = payload.ToArray();
            int cmdSize = 12 + payloadBytes.Length;
            int packetSize = 12 + cmdSize;
            byte[] packet = new byte[packetSize];
            int offset = 0;
            packet[offset++] = (byte)(peerId >> 8);
            packet[offset++] = (byte)(peerId & 0xFF);
            packet[offset++] = 0x00;
            packet[offset++] = 0x01;
            if (data.Length >= 8) {
                packet[offset++] = data[4]; packet[offset++] = data[5];
                packet[offset++] = data[6]; packet[offset++] = data[7];
            } else {
                int t = Environment.TickCount & 0x7FFFFFFF;
                packet[offset++] = (byte)(t >> 24); packet[offset++] = (byte)(t >> 16);
                packet[offset++] = (byte)(t >> 8); packet[offset++] = (byte)t;
            }
            if (data.Length >= 12) {
                packet[offset++] = data[8]; packet[offset++] = data[9];
                packet[offset++] = data[10]; packet[offset++] = data[11];
            } else {
                packet[offset++] = 0x00; packet[offset++] = 0x00;
                packet[offset++] = 0x00; packet[offset++] = 0x01;
            }
            packet[offset++] = 0x06; packet[offset++] = channel;
            packet[offset++] = 0x01; packet[offset++] = 0x04;
            packet[offset++] = (byte)(cmdSize >> 24); packet[offset++] = (byte)(cmdSize >> 16);
            packet[offset++] = (byte)(cmdSize >> 8); packet[offset++] = (byte)cmdSize;
            packet[offset++] = (byte)(seq >> 24); packet[offset++] = (byte)(seq >> 16);
            packet[offset++] = (byte)(seq >> 8); packet[offset++] = (byte)seq;
            Buffer.BlockCopy(payloadBytes, 0, packet, offset, payloadBytes.Length);
            return packet;
        }
        static byte[] BuildWeaponAcquiredEvent(ushort peerId, int seq, byte channel, byte[] data, int weaponID, bool clientEquip, int playerID)
        {
            List<byte> payload = new List<byte>();
            payload.Add(0xF3);       
            payload.Add(0x04);       
            payload.Add(31);         
            payload.Add(0x00);       
            payload.Add(0x03);       
            payload.Add(0x00);
            payload.Add(0x69);       
            payload.Add((byte)(playerID >> 24));
            payload.Add((byte)(playerID >> 16));
            payload.Add((byte)(playerID >> 8));
            payload.Add((byte)playerID);
            payload.Add(0x01);
            payload.Add(0x69);       
            payload.Add((byte)(weaponID >> 24));
            payload.Add((byte)(weaponID >> 16));
            payload.Add((byte)(weaponID >> 8));
            payload.Add((byte)weaponID);
            payload.Add(0x02);
            payload.Add(0x6F);       
            payload.Add((byte)(clientEquip ? 1 : 0));
            byte[] payloadBytes = payload.ToArray();
            int cmdSize = 12 + payloadBytes.Length;
            int packetSize = 12 + cmdSize;
            byte[] packet = new byte[packetSize];
            int offset = 0;
            packet[offset++] = (byte)(peerId >> 8);
            packet[offset++] = (byte)(peerId & 0xFF);
            packet[offset++] = 0x00;
            packet[offset++] = 0x01;
            if (data.Length >= 8) {
                packet[offset++] = data[4]; packet[offset++] = data[5];
                packet[offset++] = data[6]; packet[offset++] = data[7];
            } else {
                int t = Environment.TickCount & 0x7FFFFFFF;
                packet[offset++] = (byte)(t >> 24); packet[offset++] = (byte)(t >> 16);
                packet[offset++] = (byte)(t >> 8); packet[offset++] = (byte)t;
            }
            if (data.Length >= 12) {
                packet[offset++] = data[8]; packet[offset++] = data[9];
                packet[offset++] = data[10]; packet[offset++] = data[11];
            } else {
                packet[offset++] = 0x00; packet[offset++] = 0x00;
                packet[offset++] = 0x00; packet[offset++] = 0x01;
            }
            packet[offset++] = 0x06; packet[offset++] = channel;
            packet[offset++] = 0x01; packet[offset++] = 0x04;
            packet[offset++] = (byte)(cmdSize >> 24); packet[offset++] = (byte)(cmdSize >> 16);
            packet[offset++] = (byte)(cmdSize >> 8); packet[offset++] = (byte)cmdSize;
            packet[offset++] = (byte)(seq >> 24); packet[offset++] = (byte)(seq >> 16);
            packet[offset++] = (byte)(seq >> 8); packet[offset++] = (byte)seq;
            Buffer.BlockCopy(payloadBytes, 0, packet, offset, payloadBytes.Length);
            return packet;
        }
        static byte[] BuildGameStateEvent(ushort peerId, int seq, byte channel, byte[] data, Room room)
        {
            List<byte> payload = new List<byte>();
            payload.Add(0xF3);       
            payload.Add(0x04);       
            payload.Add(50);         
            payload.Add(0x00);       
            payload.Add(0x08);       
            int currentTime = Environment.TickCount & 0x7FFFFFFF;
            payload.Add(0x00);       
            payload.Add(0x69);       
            int stateStartTime = (room != null) ? room.MatchStartTime : (currentTime - 60000);
            payload.Add((byte)(stateStartTime >> 24));
            payload.Add((byte)(stateStartTime >> 16));
            payload.Add((byte)(stateStartTime >> 8));
            payload.Add((byte)stateStartTime);
            payload.Add(0x01);       
            payload.Add(0x69);       
            int endTime = (room != null) ? room.MatchEndTime : (currentTime + 180000);
            payload.Add((byte)(endTime >> 24));
            payload.Add((byte)(endTime >> 16));
            payload.Add((byte)(endTime >> 8));
            payload.Add((byte)endTime);
            payload.Add(0x02);       
            payload.Add(0x69);       
            int gameState = 3;       
            payload.Add((byte)(gameState >> 24));
            payload.Add((byte)(gameState >> 16));
            payload.Add((byte)(gameState >> 8));
            payload.Add((byte)gameState);
            payload.Add(0x03);       
            payload.Add(0x69);       
            payload.Add(0x00); payload.Add(0x00); payload.Add(0x00); payload.Add(0x00); 
            payload.Add(0x04);       
            payload.Add(0x69);       
            payload.Add(0x00); payload.Add(0x00); payload.Add(0x00); payload.Add(0x00); 
            payload.Add(0x05);       
            payload.Add(0x6F);       
            payload.Add(0x00);       
            payload.Add(0x06);       
            payload.Add(0x69);       
            payload.Add(0x00); payload.Add(0x00); payload.Add(0x00); payload.Add(0x00);
            payload.Add(0x07);       
            payload.Add(0x69);       
            int round = 1;
            payload.Add((byte)(round >> 24));
            payload.Add((byte)(round >> 16));
            payload.Add((byte)(round >> 8));
            payload.Add((byte)round);
            byte[] payloadBytes = payload.ToArray();
            int cmdSize = 12 + payloadBytes.Length;
            int packetSize = 12 + cmdSize;
            byte[] packet = new byte[packetSize];
            int offset = 0;
            packet[offset++] = (byte)(peerId >> 8);
            packet[offset++] = (byte)(peerId & 0xFF);
            packet[offset++] = 0x00; 
            packet[offset++] = 0x01; 
            if (data.Length >= 8)
            {
                packet[offset++] = data[4];
                packet[offset++] = data[5];
                packet[offset++] = data[6];
                packet[offset++] = data[7];
            }
            else
            {
                int timeInt = Environment.TickCount & 0x7FFFFFFF;
                packet[offset++] = (byte)(timeInt >> 24);
                packet[offset++] = (byte)(timeInt >> 16);
                packet[offset++] = (byte)(timeInt >> 8);
                packet[offset++] = (byte)timeInt;
            }
            if (data.Length >= 12)
            {
                packet[offset++] = data[8];
                packet[offset++] = data[9];
                packet[offset++] = data[10];
                packet[offset++] = data[11];
            }
            else
            {
                packet[offset++] = 0x00;
                packet[offset++] = 0x00;
                packet[offset++] = 0x00;
                packet[offset++] = 0x01;
            }
            packet[offset++] = 0x06; 
            packet[offset++] = channel;
            packet[offset++] = 0x01; 
            packet[offset++] = 0x04; 
            packet[offset++] = (byte)(cmdSize >> 24);
            packet[offset++] = (byte)(cmdSize >> 16);
            packet[offset++] = (byte)(cmdSize >> 8);
            packet[offset++] = (byte)cmdSize;
            packet[offset++] = (byte)(seq >> 24);
            packet[offset++] = (byte)(seq >> 16);
            packet[offset++] = (byte)(seq >> 8);
            packet[offset++] = (byte)seq;
            Buffer.BlockCopy(payloadBytes, 0, packet, offset, payloadBytes.Length);
            return packet;
        }
        static byte[] BuildGameStateEventOnGoing(ushort peerId, int seq, byte channel, byte[] data, Room room)
        {
            List<byte> payload = new List<byte>();
            payload.Add(0xF3);       
            payload.Add(0x04);       
            payload.Add(50);         
            payload.Add(0x00);       
            payload.Add(0x08);       
            int currentTime = Environment.TickCount & 0x7FFFFFFF;
            payload.Add(0x00);       
            payload.Add(0x69);       
            int stateStartTime = (room != null) ? room.MatchStartTime : (currentTime - 60000);
            payload.Add((byte)(stateStartTime >> 24));
            payload.Add((byte)(stateStartTime >> 16));
            payload.Add((byte)(stateStartTime >> 8));
            payload.Add((byte)stateStartTime);
            payload.Add(0x01);       
            payload.Add(0x69);       
            int endTime = (room != null) ? room.MatchEndTime : (currentTime + 180000);
            payload.Add((byte)(endTime >> 24));
            payload.Add((byte)(endTime >> 16));
            payload.Add((byte)(endTime >> 8));
            payload.Add((byte)endTime);
            payload.Add(0x02);       
            payload.Add(0x69);       
            int gameState = 3;       
            payload.Add((byte)(gameState >> 24));
            payload.Add((byte)(gameState >> 16));
            payload.Add((byte)(gameState >> 8));
            payload.Add((byte)gameState);
            payload.Add(0x03);       
            payload.Add(0x69);       
            payload.Add(0x00); payload.Add(0x00); payload.Add(0x00); payload.Add(0x00);
            payload.Add(0x04);       
            payload.Add(0x69);       
            payload.Add(0x00); payload.Add(0x00); payload.Add(0x00); payload.Add(0x00);
            payload.Add(0x05);       
            payload.Add(0x6F);       
            payload.Add(0x00);       
            payload.Add(0x06);       
            payload.Add(0x69);       
            int buyTime = 0;
            payload.Add((byte)(buyTime >> 24));
            payload.Add((byte)(buyTime >> 16));
            payload.Add((byte)(buyTime >> 8));
            payload.Add((byte)buyTime);
            payload.Add(0x07);       
            payload.Add(0x69);       
            int round = 1;
            payload.Add((byte)(round >> 24));
            payload.Add((byte)(round >> 16));
            payload.Add((byte)(round >> 8));
            payload.Add((byte)round);
            byte[] payloadBytes = payload.ToArray();
            int cmdSize = 12 + payloadBytes.Length;
            int packetSize = 12 + cmdSize;
            byte[] packet = new byte[packetSize];
            int offset = 0;
            packet[offset++] = (byte)(peerId >> 8);
            packet[offset++] = (byte)(peerId & 0xFF);
            packet[offset++] = 0x00;
            packet[offset++] = 0x01;
            if (data.Length >= 8) {
                packet[offset++] = data[4]; packet[offset++] = data[5];
                packet[offset++] = data[6]; packet[offset++] = data[7];
            } else {
                int t = Environment.TickCount & 0x7FFFFFFF;
                packet[offset++] = (byte)(t >> 24); packet[offset++] = (byte)(t >> 16);
                packet[offset++] = (byte)(t >> 8); packet[offset++] = (byte)t;
            }
            if (data.Length >= 12) {
                packet[offset++] = data[8]; packet[offset++] = data[9];
                packet[offset++] = data[10]; packet[offset++] = data[11];
            } else {
                packet[offset++] = 0x00; packet[offset++] = 0x00;
                packet[offset++] = 0x00; packet[offset++] = 0x01;
            }
            packet[offset++] = 0x06; packet[offset++] = channel;
            packet[offset++] = 0x01; packet[offset++] = 0x04;
            packet[offset++] = (byte)(cmdSize >> 24); packet[offset++] = (byte)(cmdSize >> 16);
            packet[offset++] = (byte)(cmdSize >> 8); packet[offset++] = (byte)cmdSize;
            packet[offset++] = (byte)(seq >> 24); packet[offset++] = (byte)(seq >> 16);
            packet[offset++] = (byte)(seq >> 8); packet[offset++] = (byte)seq;
            Buffer.BlockCopy(payloadBytes, 0, packet, offset, payloadBytes.Length);
            return packet;
        }
        static void HandleUnreliable(UdpClient udp, IPEndPoint ep, byte[] data, ClientState client)
        {
            try
            {
                if (data.Length > 2 && data[0] == 0xF3 && data[1] == 0x02)
                {
                    byte opCode = data[2];
                    if (opCode == 102) 
                    {
                        int offset = 3;
                        if (offset + 2 > data.Length) return;
                        short pCount = (short)((data[offset] << 8) | data[offset+1]);
                        offset += 2;
                        for(int i=0; i<pCount; i++)
                        {
                            if(offset + 2 > data.Length) break;
                            byte key = data[offset++];
                            byte type = data[offset++];
                            if (type == 0x78) 
                            {
                                if (offset + 4 > data.Length) break;
                                int len = (data[offset] << 24) | (data[offset+1] << 16) | (data[offset+2] << 8) | data[offset+3];
                                offset += 4;
                                if (len > 0 && offset + len <= data.Length)
                                {
                                    if (key == 1)
                                    {
                                        byte[] val = new byte[len];
                                        Buffer.BlockCopy(data, offset, val, 0, len);
                                        client.LastCharacterUpdateData = val;
                                        BroadcastPositionUpdate(udp, client);
                                    }
                                }
                                offset += len;
                            }
                            else if (type == 0x69) offset += 4; 
                            else if (type == 0x62) offset += 1; 
                            else if (type == 0x6F) offset += 1; 
                            else if (type == 0x73) 
                            {
                                if (offset + 2 > data.Length) break;
                                short sLen = (short)((data[offset] << 8) | data[offset+1]);
                                offset += 2 + sLen;
                            }
                            else break;
                        }
                    }
                    else if (opCode == 104) 
                    {
                        BroadcastShootEvent(udp, client, data);
                    }
                    else if (opCode == 105) 
                    {
                        BroadcastAction(udp, client, 19);
                    }
                    else if (opCode == 103) 
                    {
                    int offset = 3;
                    if (offset + 2 <= data.Length)
                    {
                        short pCount = (short)((data[offset] << 8) | data[offset+1]);
                        offset += 2;
                            if (offset + 2 < data.Length && data[offset] == 0 && data[offset+1] == 0x62)
                            {
                                byte wpnId = data[offset+2];
                                BroadcastWeaponSelect(udp, client, wpnId);
                            }
                    }
                    }
                }
            }
            catch (Exception ex)
            {
            }
        }
        static void BroadcastPositionUpdate(UdpClient udp, ClientState sender)
        {
            if (sender.LastCharacterUpdateData == null) return;
            List<byte> payload = new List<byte>();
            payload.Add(0xF3);
            payload.Add(0x04); 
            payload.Add(13);   
            payload.Add(0x00); payload.Add(0x03); 
            payload.Add(0x00); 
            payload.Add(0x6E); 
            payload.Add(0x00); payload.Add(0x00); payload.Add(0x00); payload.Add(0x01); 
            int pid = sender.PlayerID != 0 ? sender.PlayerID : sender.PeerId; 
            payload.Add((byte)(pid >> 24)); payload.Add((byte)(pid >> 16)); payload.Add((byte)(pid >> 8)); payload.Add((byte)pid);
            payload.Add(0x01); 
            payload.Add(0x78); 
            byte[] rawData = sender.LastCharacterUpdateData;
            byte[] paddedData = rawData;
            if (rawData.Length < 19)
            {
               paddedData = new byte[19];
               Buffer.BlockCopy(rawData, 0, paddedData, 0, rawData.Length);
            }
            int dataLen = paddedData.Length;
            payload.Add((byte)(dataLen >> 24)); payload.Add((byte)(dataLen >> 16)); payload.Add((byte)(dataLen >> 8)); payload.Add((byte)dataLen);
            payload.AddRange(paddedData);
            payload.Add(0x02); 
            payload.Add(0x69); 
            int tick = 0;
            if (rooms.ContainsKey(sender.RoomID))
            {
                 int now = Environment.TickCount & 0x7FFFFFFF;
                 int elapsed = now - rooms[sender.RoomID].MatchStartTime;
                 if (elapsed < 0) elapsed = 0;
                 tick = elapsed / 16;
            }
            else
            {
                tick = (Environment.TickCount & 0x7FFFFFFF) / 16;
            }
            payload.Add((byte)(tick >> 24)); payload.Add((byte)(tick >> 16)); payload.Add((byte)(tick >> 8)); payload.Add((byte)(tick));
            byte[] eventBytes = payload.ToArray();
            foreach (var kvp in clients)
            {
                ClientState target = kvp.Value;
                if (!target.Connected) continue;
                if (target.RoomID != sender.RoomID) continue;
                int cmdSize = 12 + eventBytes.Length; 
                int packetSize = 4 + cmdSize; 
                byte[] packet = new byte[packetSize];
                int offset = 0;
                packet[offset++] = (byte)(target.PeerId >> 8);
                packet[offset++] = (byte)(target.PeerId & 0xFF);
                packet[offset++] = 0x00; 
                packet[offset++] = 0x01; 
                packet[offset++] = 0x07; 
                packet[offset++] = 0x00; 
                packet[offset++] = 0x00; 
                packet[offset++] = 0x00; 
                packet[offset++] = (byte)(cmdSize >> 24);
                packet[offset++] = (byte)(cmdSize >> 16);
                packet[offset++] = (byte)(cmdSize >> 8);
                packet[offset++] = (byte)cmdSize;
                int unseq = target.GetNextUnreliableSequence(0); 
                packet[offset++] = (byte)(unseq >> 24);
                packet[offset++] = (byte)(unseq >> 16);
                packet[offset++] = (byte)(unseq >> 8);
                packet[offset++] = (byte)unseq;
                Buffer.BlockCopy(eventBytes, 0, packet, offset, eventBytes.Length);
                udp.Send(packet, packet.Length, target.EndPoint);
            }
        }
        static void BroadcastAction(UdpClient udp, ClientState sender, byte eventCode)
        {
            List<byte> payload = new List<byte>();
            payload.Add(0xF3);
            payload.Add(0x04);
            payload.Add(eventCode);
            payload.Add(0x00); payload.Add(0x01); 
            payload.Add(0x00); 
            payload.Add(0x69); 
            int pid = sender.PlayerID != 0 ? sender.PlayerID : sender.PeerId;
            payload.Add((byte)(pid >> 24)); payload.Add((byte)(pid >> 16)); payload.Add((byte)(pid >> 8)); payload.Add((byte)pid);
            byte[] evt = payload.ToArray();
            BroadcastEvent(udp, evt, sender.PeerId, true, 0, sender.RoomID); 
        }
        static void BroadcastShootEvent(UdpClient udp, ClientState sender, byte[] requestData)
        {
            byte[] shootData = new byte[0];
            int offset = 3; 
            if (offset + 2 <= requestData.Length)
            {
                short pCount = (short)((requestData[offset] << 8) | requestData[offset+1]);
                offset += 2;
                for(int i=0; i<pCount; i++)
                {
                    if(offset + 2 > requestData.Length) break;
                    byte key = requestData[offset++];
                    byte type = requestData[offset++];
                    if (type == 0x78) 
                    {
                        if (offset + 4 > requestData.Length) break;
                        int len = (requestData[offset] << 24) | (requestData[offset+1] << 16) | (requestData[offset+2] << 8) | requestData[offset+3];
                        offset += 4;
                        if (len > 0 && offset + len <= requestData.Length)
                        {
                            if (key == 0) 
                            {
                                shootData = new byte[len];
                                Buffer.BlockCopy(requestData, offset, shootData, 0, len);
                            }
                        }
                        offset += len;
                    }
                    else if (type == 0x69) offset += 4;
                    else if (type == 0x62) offset += 1;
                    else if (type == 0x6F) offset += 1;
                }
            }
            List<byte> payload = new List<byte>();
            payload.Add(0xF3);
            payload.Add(0x04);
            payload.Add(18); 
            payload.Add(0x00); payload.Add(0x04); 
            payload.Add(0x00); payload.Add(0x69);
            int pid = sender.PlayerID != 0 ? sender.PlayerID : sender.PeerId;
            payload.Add((byte)(pid >> 24)); payload.Add((byte)(pid >> 16)); payload.Add((byte)(pid >> 8)); payload.Add((byte)pid);
            payload.Add(0x01); payload.Add(0x69);
            int bullets = 30; 
            payload.Add((byte)(bullets >> 24)); payload.Add((byte)(bullets >> 16)); payload.Add((byte)(bullets >> 8)); payload.Add((byte)bullets);
            payload.Add(0x02); payload.Add(0x78);
            int sdLen = shootData.Length;
            payload.Add((byte)(sdLen >> 24)); payload.Add((byte)(sdLen >> 16)); payload.Add((byte)(sdLen >> 8)); payload.Add((byte)sdLen);
            payload.AddRange(shootData);
            payload.Add(0x03); payload.Add(0x6F);
            payload.Add(0x00);
            byte[] evt = payload.ToArray();
            BroadcastEvent(udp, evt, sender.PeerId, true, 0, sender.RoomID); 
        }
        static void BroadcastWeaponSelect(UdpClient udp, ClientState sender, byte weaponId)
        {
            List<byte> payload = new List<byte>();
            payload.Add(0xF3);
            payload.Add(0x04);
            payload.Add(17);
            payload.Add(0x00); payload.Add(0x02); 
            payload.Add(0x00); 
            payload.Add(0x69); 
            int pid = sender.PlayerID != 0 ? sender.PlayerID : sender.PeerId;
            payload.Add((byte)(pid >> 24)); payload.Add((byte)(pid >> 16)); payload.Add((byte)(pid >> 8)); payload.Add((byte)pid);
            payload.Add(0x01); 
            payload.Add(0x69); 
            int wid = weaponId;
            payload.Add((byte)(wid >> 24)); payload.Add((byte)(wid >> 16)); payload.Add((byte)(wid >> 8)); payload.Add((byte)wid);
            byte[] evt = payload.ToArray();
            BroadcastEvent(udp, evt, sender.PeerId, true, 0, sender.RoomID);
        }
        static void HandleReliable(UdpClient udp, IPEndPoint ep, byte[] data, ClientState client)
        {
            if (!client.Connected)
            {
                return;
            }
            byte channel = 0;
            int relSeq = 1;
            if (data.Length >= 24)
            {
                channel = data[13];
                relSeq = (data[20] << 24) | (data[21] << 16) | (data[22] << 8) | data[23];
            }
            byte[] ack = BuildAck(client.PeerId, channel, relSeq, data);
            udp.Send(ack, ack.Length, ep);
            if (data.Length > 12)
            {
                int payloadStart = 9; 
                if (data[payloadStart] == 0xF3)
                {
                    byte msgType = data[payloadStart + 1];
                    if (msgType == 0x02) 
                    {
                        byte opCode = data[payloadStart + 2];
                        if (opCode == 0x01)
                        {
                            byte[] authResp = BuildAuthResponse(client.PeerId, client.Sequence++);
                            udp.Send(authResp, authResp.Length, ep);
                        }
                    }
                }
            }
        }
        static byte[] BuildVerifyConnect(ushort peerId, byte[] connectPacket)
        {
            byte[] response = new byte[56]; 
            int offset = 0;
            response[offset++] = (byte)(peerId >> 8);
            response[offset++] = (byte)(peerId & 0xFF);
            response[offset++] = 0x00;
            response[offset++] = 0x01;
            response[offset++] = connectPacket[4];
            response[offset++] = connectPacket[5];
            response[offset++] = connectPacket[6];
            response[offset++] = connectPacket[7];
            response[offset++] = connectPacket[8];
            response[offset++] = connectPacket[9];
            response[offset++] = connectPacket[10];
            response[offset++] = connectPacket[11];
            response[offset++] = 0x03;
            response[offset++] = 0xFF;
            response[offset++] = 0x01;
            response[offset++] = 0x04;
            response[offset++] = 0x00;
            response[offset++] = 0x00;
            response[offset++] = 0x00;
            response[offset++] = 0x2C;
            response[offset++] = 0x00;
            response[offset++] = 0x00;
            response[offset++] = 0x00;
            response[offset++] = 0x01;
            response[offset++] = (byte)(peerId >> 8);
            response[offset++] = (byte)(peerId & 0xFF);
            for (int i = 0; i < 30; i++)
            {
                if (24 + 2 + i < connectPacket.Length)
                    response[offset++] = connectPacket[24 + 2 + i];
                else
                    response[offset++] = 0x00;
            }
            return response;
        }
        static byte[] BuildInitPacket(ushort peerId)
        {
            List<byte> p = new List<byte>();
            p.Add((byte)(peerId >> 8));
            p.Add((byte)(peerId & 0xFF));
            p.Add(0x00); 
            p.Add(0x00); 
            p.Add(0xF3); 
            p.Add(0x01); 
            return p.ToArray();
        }
        static byte[] BuildAck(ushort peerId, byte channel, int relSeq, byte[] data)
        {
            byte[] packet = new byte[32];
            int offset = 0;
            packet[offset++] = (byte)(peerId >> 8);
            packet[offset++] = (byte)(peerId & 0xFF);
            packet[offset++] = 0x00; 
            packet[offset++] = 0x01; 
            int timeInt = Environment.TickCount & 0x7FFFFFFF;
            packet[offset++] = (byte)(timeInt >> 24);
            packet[offset++] = (byte)(timeInt >> 16);
            packet[offset++] = (byte)(timeInt >> 8);
            packet[offset++] = (byte)timeInt;
            if (data.Length >= 12)
            {
                packet[offset++] = data[8];
                packet[offset++] = data[9];
                packet[offset++] = data[10];
                packet[offset++] = data[11];
            }
            else
            {
                offset += 4;
            }
            packet[offset++] = 0x01; 
            packet[offset++] = channel; 
            packet[offset++] = 0x00; 
            packet[offset++] = 0x04; 
            packet[offset++] = 0x00;
            packet[offset++] = 0x00;
            packet[offset++] = 0x00;
            packet[offset++] = 0x14;
            packet[offset++] = 0x00;
            packet[offset++] = 0x00;
            packet[offset++] = 0x00;
            packet[offset++] = 0x00;
            packet[offset++] = (byte)(relSeq >> 24);
            packet[offset++] = (byte)(relSeq >> 16);
            packet[offset++] = (byte)(relSeq >> 8);
            packet[offset++] = (byte)relSeq;
            if (data.Length >= 8)
            {
                packet[offset++] = data[4];
                packet[offset++] = data[5];
                packet[offset++] = data[6];
                packet[offset++] = data[7];
            }
            else
            {
                packet[offset++] = 0x00;
                packet[offset++] = 0x00;
                packet[offset++] = 0x00;
                packet[offset++] = 0x00;
            }
            return packet;
        }
        static byte[] BuildAck(ushort peerId, byte[] cmd)
        {
            List<byte> p = new List<byte>();
            p.Add((byte)(peerId >> 8));
            p.Add((byte)(peerId & 0xFF));
            p.Add(0x00);
            p.Add(0x01); 
            int timeInt = Environment.TickCount & 0x7FFFFFFF;
            p.Add((byte)(timeInt >> 24));
            p.Add((byte)(timeInt >> 16));
            p.Add((byte)(timeInt >> 8));
            p.Add((byte)timeInt);
            if (cmd.Length >= 12)
            {
                p.Add(cmd[8]);
                p.Add(cmd[9]);
                p.Add(cmd[10]);
                p.Add(cmd[11]);
            }
            else
            {
                p.Add(0x00);
                p.Add(0x00);
                p.Add(0x00);
                p.Add(0x01);
            }
            p.Add(0x01); 
            p.Add(0xFF); 
            p.Add(0x00); 
            p.Add(0x04); 
            p.Add(0x00);
            p.Add(0x00);
            p.Add(0x00);
            p.Add(0x14);
            p.Add(0x00);
            p.Add(0x00);
            p.Add(0x00);
            p.Add(0x00);
            p.Add(0x00);
            p.Add(0x00);
            p.Add(0x00);
            p.Add(0x01);
            if (cmd.Length >= 8)
            {
                p.Add(cmd[4]);
                p.Add(cmd[5]);
                p.Add(cmd[6]);
                p.Add(cmd[7]);
            }
            else
            {
                p.Add(0x00);
                p.Add(0x00);
                p.Add(0x00);
                p.Add(0x11);
            }
            return p.ToArray();
        }
        static byte[] BuildAuthResponse(ushort peerId, int seq)
        {
            List<byte> p = new List<byte>();
            p.Add((byte)(peerId >> 8));
            p.Add((byte)(peerId & 0xFF));
            p.Add(0x00);
            p.Add(0x07); 
            p.Add((byte)((seq >> 24) & 0xFF));
            p.Add((byte)((seq >> 16) & 0xFF));
            p.Add((byte)((seq >> 8) & 0xFF));
            p.Add((byte)(seq & 0xFF));
            p.Add(0x00);
            List<byte> photon = new List<byte>();
            photon.Add(0xF3);
            photon.Add(0x03); 
            photon.Add(0x01); 
            photon.Add(0x00);
            photon.Add(0x00);
            photon.Add(0x2A);
            photon.Add(0x48);
            photon.Add(0x00);
            photon.Add(0x2A);
            photon.Add(0x00);
            photon.Add(0x00);
            int len = photon.Count;
            p.Add((byte)((len >> 24) & 0xFF));
            p.Add((byte)((len >> 16) & 0xFF));
            p.Add((byte)((len >> 8) & 0xFF));
            p.Add((byte)(len & 0xFF));
            p.AddRange(photon);
            return p.ToArray();
        }
        static byte[] BuildCharacterStateUpdateEvent(ushort peerId, int seq, byte channel, byte[] data, int playerID)
        {
            List<byte> payload = new List<byte>();
            payload.Add(0xF3);       
            payload.Add(0x04);       
            payload.Add(12);         
            var parameters = GetCharacterStateUpdateParams(playerID);
            payload.Add(0x00);       
            payload.Add((byte)parameters.Count); 
            foreach(var kvp in parameters)
            {
                payload.Add(kvp.Key);
                object val = kvp.Value;
                if (val is int iVal)
                {
                    payload.Add(0x69); 
                    payload.Add((byte)(iVal >> 24));
                    payload.Add((byte)(iVal >> 16));
                    payload.Add((byte)(iVal >> 8));
                    payload.Add((byte)iVal);
                }
                else if (val is bool bVal)
                {
                    payload.Add(0x6F); 
                    payload.Add((byte)(bVal ? 1 : 0));
                }
            }
            byte[] payloadBytes = payload.ToArray();
            int cmdSize = 12 + payloadBytes.Length;
            int packetSize = 12 + cmdSize;
            byte[] packet = new byte[packetSize];
            int offset = 0;
            packet[offset++] = (byte)(peerId >> 8);
            packet[offset++] = (byte)(peerId & 0xFF);
            packet[offset++] = 0x00;
            packet[offset++] = 0x01;
            if (data.Length >= 8) {
                packet[offset++] = data[4]; packet[offset++] = data[5];
                packet[offset++] = data[6]; packet[offset++] = data[7];
            } else {
                int t = Environment.TickCount & 0x7FFFFFFF;
                packet[offset++] = (byte)(t >> 24); packet[offset++] = (byte)(t >> 16);
                packet[offset++] = (byte)(t >> 8); packet[offset++] = (byte)t;
            }
            if (data.Length >= 12) {
                packet[offset++] = data[8]; packet[offset++] = data[9];
                packet[offset++] = data[10]; packet[offset++] = data[11];
            } else {
                packet[offset++] = 0x00; packet[offset++] = 0x00;
                packet[offset++] = 0x00; packet[offset++] = 0x01;
            }
            packet[offset++] = 0x06; 
            packet[offset++] = channel;
            packet[offset++] = 0x01; 
            packet[offset++] = 0x04; 
            packet[offset++] = (byte)(cmdSize >> 24);
            packet[offset++] = (byte)(cmdSize >> 16);
            packet[offset++] = (byte)(cmdSize >> 8);
            packet[offset++] = (byte)cmdSize;
            packet[offset++] = (byte)(seq >> 24);
            packet[offset++] = (byte)(seq >> 16);
            packet[offset++] = (byte)(seq >> 8);
            packet[offset++] = (byte)seq;
            Buffer.BlockCopy(payloadBytes, 0, packet, offset, payloadBytes.Length);
            return packet;
        }
        static void StartHttpServer()
        {
            HttpListener listener = new HttpListener();
            listener.Prefixes.Add($"http://{LocalIP}:{HttpPort}/");
            try { listener.Start();  }
            catch (Exception e) {  return; }
            while (true)
            {
                try { ProcessRequest(listener.GetContext()); }
                catch (Exception ex) {  }
            }
        }
        static void ProcessRequest(HttpListenerContext context)
        {
            string url = context.Request.Url.AbsolutePath;
            string method = context.Request.HttpMethod;
            string requestBody = "";
            if (context.Request.HasEntityBody)
            {
                using (var bodyReader = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding))
                {
                    requestBody = bodyReader.ReadToEnd();
                }
            }
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.ResetColor();
            if (!string.IsNullOrEmpty(requestBody))
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.ResetColor();
            }
            string responseString = "{}";
            if (url.StartsWith("/assets/"))
            {
                ServeFile(context, url);
                return;
            }
            else if (url.Contains("/api/app/start"))
            {
                try 
                {
                    dynamic json = JsonConvert.DeserializeObject(requestBody);
                    string deviceId = json.deviceid;
                    string clientIp = context.Request.RemoteEndPoint.Address.ToString();
                    lock(IpToDeviceId)
                    {
                        IpToDeviceId[clientIp] = deviceId;
                    }
                }
                catch(Exception ex)
                {
                }
                var startReturn = new
                {
                    udid = "emu-player",
                    deviceSessionID = 1,
                    deviceSessionToken = "token_1",
                    assetBundleServerURLs = new string[] { $"http://{LocalIP}:{HttpPort}/assets/" },
                    hubAddress = $"{LocalIP}:{GamePort}",
                    loginType = 0,
                    tutorialCompleted = 1,
                    config = new { EndTime = 2000000000 },
                    sessionKey = "sess_1",
                    featureSettings = new { }
                };
                responseString = JsonConvert.SerializeObject(startReturn);
            }
            else if (url.Contains("/api/user/login/"))
            {
                string clientIp = context.Request.RemoteEndPoint.Address.ToString();
                string deviceId = null;
                lock(IpToDeviceId)
                {
                    if (IpToDeviceId.ContainsKey(clientIp))
                        deviceId = IpToDeviceId[clientIp];
                }
                if (string.IsNullOrEmpty(deviceId))
                {
                    deviceId = "dummy_device_" + clientIp.Replace(".", "");
                }
                string platform = "Guest";
                try {
                    dynamic json = JsonConvert.DeserializeObject(requestBody);
                    platform = json.platform ?? "Guest";
                } catch {}
                var user = DatabaseHelper.GetOrCreateUser(deviceId, platform, clientIp);
                var loginData = new
                {
                    accountFound = true,
                    userSessionToken = Guid.NewGuid().ToString(), 
                    userSessionID = user.UserID,
                    accountLinks = new
                    {
                        facebook = false,
                        google = false,
                        gamecenter = false
                    },
                    nameChange = false,
                    nameChangePrice = 1000,
                    refunds = 0,
                    onboardingStage = 60,
                    profile = new
                    {
                        uid = user.UserID, 
                        username = user.Username,
                        credits = user.Credits,
                        tokens = user.Tokens,
                        skinpacks = user.SkinPacks, 
                        friendLimit = 100,
                        weaponskins = user.OwnedSkins, 
                        weapons = new object[] { },
                        rankedInfo = new
                        {
                            Rank = 0,
                            Stars = 0,
                            PlacementMatchesLeft = 0
                        },
                        missions = new object[] { },
                        userSettings = new { blockFriendRequests = false },
                        friends = new object[] { },
                        casualStats = new
                        {
                            Kills = 0, Deaths = 0, Headshots = 0,
                            TotalKills = 0, TotalDeaths = 0, TotalHeadshots = 0,
                            Wins = 0, Losses = 0
                        },
                        rankedStats = new
                        {
                            Kills = 0, Deaths = 0, Headshots = 0,
                            TotalKills = 0, TotalDeaths = 0, TotalHeadshots = 0,
                            Wins = 0, Losses = 0
                        },
                        clan = "", 
                        completedMissions = 0,
                        discardedMissions = 0,
                        canDiscard = false,
                        rankedKillLimit = 0,
                        rankedPenaltyLeft = 0
                    },
                    tierValues = new int[] {1,2,3,4,5,6,7}, 
                    products = new object[] { }, 
                    skinpackPrice = 400,
                    skinpackOffers = new int[] { 1, 3, 5 },
                    rate = new { CanAskToRate = true, UserHasNeverRated = true }, 
                    news = new
                    {
                        videos = new object[] { },
                        articles = new object[] { }
                    }
                };
                responseString = JsonConvert.SerializeObject(loginData);
            }
            else if (url.Contains("/api/user/profile/"))
            {
                string clientIp = context.Request.RemoteEndPoint.Address.ToString();
                string deviceId = null;
                lock(IpToDeviceId) { IpToDeviceId.TryGetValue(clientIp, out deviceId); }
                UserData user = null;
                if (!string.IsNullOrEmpty(deviceId))
                {
                    user = DatabaseHelper.GetUserByDeviceID(deviceId);
                }
                if (user == null)
                {
                     user = new UserData { UserID=0, Username="Unknown", Credits=0, Tokens=0, UserType=0 };
                }
                var userProfile = new
                {
                    uid = user.UserID, 
                    username = user.Username,
                    usertype = user.UserType,
                    ban = 0,
                    credits = user.Credits,
                    tokens = user.Tokens,
                    skinpacks = user.SkinPacks,
                    weaponskins = user.OwnedSkins, 
                    weapons = new object[] { },
                    rankedInfo = new
                    {
                        Rank = 5,
                        Stars = 0,
                        PlacementMatchesLeft = 0
                    },
                    missions = new object[] { },
                    userSettings = new { blockFriendRequests = false },
                    friends = new object[] { },
                    friendLimit = 100,
                    casualStats = new
                    {
                        Kills = 0, Deaths = 0, Headshots = 0,
                        TotalKills = 0, TotalDeaths = 0, TotalHeadshots = 0,
                        Wins = 0, Losses = 0
                    },
                    rankedStats = new
                    {
                        Kills = 0, Deaths = 0, Headshots = 0,
                        TotalKills = 0, TotalDeaths = 0, TotalHeadshots = 0,
                        Wins = 0, Losses = 0
                    },
                    clan = "", 
                    completedMissions = 0,
                    discardedMissions = 0,
                    canDiscard = false,
                    rankedKillLimit = 0,
                    rankedPenaltyLeft = 0
                };
                responseString = JsonConvert.SerializeObject(userProfile);
            }
            else if (url.Contains("/api/user/namecheck/"))
            {
                string usernameToCheck = "";
                try
                {
                    dynamic json = JsonConvert.DeserializeObject(requestBody);
                    usernameToCheck = json.username;
                }
                catch { }
                bool available = false;
                if (!string.IsNullOrEmpty(usernameToCheck))
                {
                    available = DatabaseHelper.IsUsernameAvailable(usernameToCheck);
                }
                var checkResult = new
                {
                    username = usernameToCheck,
                    available = available
                };
                responseString = JsonConvert.SerializeObject(checkResult);
            }
            else if (url.Contains("/api/purchase/namechange/") || url.Contains("/api/user/namechange/"))
            {
                string newUsername = "";
                int price = 1000; 
                try
                {
                    dynamic json = JsonConvert.DeserializeObject(requestBody);
                    newUsername = json.username;
                    if (json.amount != null) price = (int)json.amount;
                }
                catch { }
                string clientIp = context.Request.RemoteEndPoint.Address.ToString();
                string deviceId = null;
                lock (IpToDeviceId) { IpToDeviceId.TryGetValue(clientIp, out deviceId); }
                UserData user = null;
                if (!string.IsNullOrEmpty(deviceId))
                {
                    user = DatabaseHelper.GetUserByDeviceID(deviceId);
                }
                if (user == null)
                {
                    context.Response.StatusCode = 400;
                    responseString = JsonConvert.SerializeObject(new { error = "User not found" });
                }
                else if (string.IsNullOrEmpty(newUsername))
                {
                    context.Response.StatusCode = 400;
                    responseString = JsonConvert.SerializeObject(new { error = "No username provided" });
                }
                else if (user.Credits < price)
                {
                    context.Response.StatusCode = 400;
                    responseString = JsonConvert.SerializeObject(new { error = "Insufficient credits" });
                }
                else
                {
                    string resultMsg;
                    bool success = DatabaseHelper.ChangeUsername(user.UserID, newUsername, out resultMsg);
                    if (success)
                    {
                        int newCredits = user.Credits - price;
                        DatabaseHelper.UpdateCredits(user.UserID, newCredits);
                        responseString = JsonConvert.SerializeObject(new { 
                            username = newUsername,
                            currentCredits = newCredits
                        });
                    }
                    else
                    {
                        context.Response.StatusCode = 400;
                        responseString = JsonConvert.SerializeObject(new { error = resultMsg });
                    }
                }
            }
            else if (url.Contains("/api/purchase/weaponskin/"))
            {
                int skinID = 0;
                int amount = 0; 
                try
                {
                    dynamic json = JsonConvert.DeserializeObject(requestBody);
                    skinID = (int)json.skinID;
                    amount = (int)json.amount;
                }
                catch { }
                string clientIp = context.Request.RemoteEndPoint.Address.ToString();
                string deviceId = null;
                lock (IpToDeviceId) { IpToDeviceId.TryGetValue(clientIp, out deviceId); }
                UserData user = null;
                if (!string.IsNullOrEmpty(deviceId))
                {
                    user = DatabaseHelper.GetUserByDeviceID(deviceId);
                }
                if (user == null)
                {
                    context.Response.StatusCode = 400;
                    responseString = JsonConvert.SerializeObject(new { error = "User not found" });
                }
                else if (skinID <= 0)
                {
                    context.Response.StatusCode = 400;
                    responseString = JsonConvert.SerializeObject(new { error = "Invalid skin ID" });
                }
                else if (user.Tokens < amount)
                {
                    context.Response.StatusCode = 400;
                    responseString = JsonConvert.SerializeObject(new { error = "Insufficient tokens" });
                }
                else
                {
                    int newTokens = user.Tokens - amount;
                    DatabaseHelper.UpdateTokens(user.UserID, newTokens);
                    DatabaseHelper.AddSkinToUser(user.UserID, skinID);
                    responseString = JsonConvert.SerializeObject(new { 
                        skinBought = skinID,
                        tokensLeft = newTokens
                    });
                }
            }
            else if (url.Contains("/api/weaponskin/attach/"))
            {
                int weaponID = 0;
                int skinID = 0;
                try
                {
                    dynamic json = JsonConvert.DeserializeObject(requestBody);
                    weaponID = (int)json.weaponID;
                    skinID = (int)json.skinID;
                }
                catch { }
                string clientIp = context.Request.RemoteEndPoint.Address.ToString();
                string deviceId = null;
                lock (IpToDeviceId) { IpToDeviceId.TryGetValue(clientIp, out deviceId); }
                UserData user = null;
                if (!string.IsNullOrEmpty(deviceId))
                {
                    user = DatabaseHelper.GetUserByDeviceID(deviceId);
                }
                if (user == null)
                {
                    context.Response.StatusCode = 400;
                    responseString = "false"; 
                }
                else
                {
                    bool success = DatabaseHelper.SetUserLoadout(user.UserID, weaponID, skinID);
                    if (success)
                    {
                        responseString = "true";
                    }
                    else
                    {
                        responseString = "false";
                    }
                }
            }
            else if (url.Contains("/api/server/list/"))
            {
                var serverList = new[]
                {
        new
        {
            id = 1,
            addr = $"{LocalIP}:{GamePort}", 
            name = "srkorwho server"
        }
    };
                responseString = JsonConvert.SerializeObject(serverList);
            }
            else if (url.Contains("ping"))
            {
                responseString = JsonConvert.SerializeObject(new { version = "0.8.1.f133" });
            }
            else if (url.Contains("/api/purchase/weaponskinpack/"))
            {
                string clientIp = context.Request.RemoteEndPoint.Address.ToString();
                string deviceId = null;
                lock(IpToDeviceId) { IpToDeviceId.TryGetValue(clientIp, out deviceId); }
                var user = DatabaseHelper.GetUserByDeviceID(deviceId);
                string purchaseResponse = "{}";
                if (user != null)
                {
                    try
                    {
                        dynamic json = JsonConvert.DeserializeObject(requestBody);
                        int quantity = json.quantity;
                        int amount = json.amount;
                        int newCredits, newPacks;
                        bool success = DatabaseHelper.ProcessPurchase(user.UserID, amount, quantity, out newCredits, out newPacks);
                        if (success)
                        {
                             var result = new
                            {
                                currentSkinPacks = newPacks,
                                currentCredits = newCredits
                            };
                            purchaseResponse = JsonConvert.SerializeObject(result);
                        }
                        else
                        {
                            var result = new { currentSkinPacks = user.SkinPacks, currentCredits = user.Credits };
                            purchaseResponse = JsonConvert.SerializeObject(result);
                        }
                    }
                    catch { }
                }
                responseString = purchaseResponse;
            }
            else if (url.Contains("/api/weaponskin/pack/open/"))
            {
                string clientIp = context.Request.RemoteEndPoint.Address.ToString();
                string deviceId = null;
                lock(IpToDeviceId) { IpToDeviceId.TryGetValue(clientIp, out deviceId); }
                var user = DatabaseHelper.GetUserByDeviceID(deviceId);
                string openResponse = "{}";
                if (user != null)
                {
                    var result = DatabaseHelper.ProcessOpenPack(user.UserID);
                    if (result.Success)
                    {
                         var unpackResult = new
                        {
                            skinID = result.SkinID,
                            alreadyOwned = result.AlreadyOwned,
                            tokensGained = result.TokensGained,
                            packsLeft = result.PacksLeft
                        };
                        openResponse = JsonConvert.SerializeObject(unpackResult);
                    }
                    else
                    {
                    }
                }
                responseString = openResponse;
            }
            else if (url.Contains("log"))
            {
                responseString = "{}";
            }
            byte[] buffer = Encoding.UTF8.GetBytes(responseString);
            context.Response.ContentType = "application/json";
            context.Response.ContentLength64 = buffer.Length;
            context.Response.OutputStream.Write(buffer, 0, buffer.Length);
            context.Response.OutputStream.Close();
        }
        static void ServeFile(HttpListenerContext context, string url)
        {
            string fileName = Path.GetFileName(url);
            string localPath = Path.Combine(AssetFolderPath, fileName);
            if (!File.Exists(localPath))
            {
                string[] foundFiles = Directory.GetFiles(AssetFolderPath, fileName, SearchOption.AllDirectories);
                if (foundFiles.Length > 0) localPath = foundFiles[0];
            }
            if (File.Exists(localPath))
            {
                try
                {
                    byte[] fileBytes = File.ReadAllBytes(localPath);
                    context.Response.ContentType = "application/octet-stream"; 
                    context.Response.ContentLength64 = fileBytes.Length;
                    context.Response.OutputStream.Write(fileBytes, 0, fileBytes.Length);
                    context.Response.OutputStream.Close();
                }
                catch (Exception e)
                {
                    context.Response.StatusCode = 500;
                    context.Response.Close();
                }
            }
            else
            {
                context.Response.StatusCode = 404;
                context.Response.Close();
            }
        }
    }
}