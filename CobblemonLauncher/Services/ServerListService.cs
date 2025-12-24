using System;
using System.IO;
using System.Text;

namespace CobblemonLauncher.Services
{
    public static class ServerListService
    {
        public static void SetSingleServer(string gameDir, string serverName, string serverAddress)
        {
            if (string.IsNullOrWhiteSpace(gameDir) || string.IsNullOrWhiteSpace(serverAddress))
                return;

            try
            {
                Directory.CreateDirectory(gameDir);
                string serversDatPath = Path.Combine(gameDir, "servers.dat");

                using var ms = new MemoryStream();
                using var writer = new BinaryWriter(ms);

                // 1. Root compound
                writer.Write((byte)10); // TAG_Compound
                WriteUtf(writer, "");   // Root tag name is empty

                // 2. TAG_List "servers"
                writer.Write((byte)9);  // TAG_List
                WriteUtf(writer, "servers");
                writer.Write((byte)10); // List elements type: TAG_Compound
                WriteInt32BigEndian(writer, 1); // Exactly 1 server, wiping all other servers

                // 3. Server Compound Tag
                // Tag "name"
                writer.Write((byte)8);  // TAG_String
                WriteUtf(writer, "name");
                WriteUtf(writer, serverName);

                // Tag "ip"
                writer.Write((byte)8);  // TAG_String
                WriteUtf(writer, "ip");
                WriteUtf(writer, serverAddress.Trim());

                // Tag "hidden" = 0 (visible in Multiplayer screen)
                writer.Write((byte)1);  // TAG_Byte
                WriteUtf(writer, "hidden");
                writer.Write((byte)0);

                // End of Server Compound
                writer.Write((byte)0);  // TAG_End

                // End of Root Compound
                writer.Write((byte)0);  // TAG_End

                File.WriteAllBytes(serversDatPath, ms.ToArray());
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ServerListService] Lỗi ghi servers.dat: {ex.Message}");
            }
        }

        private static void WriteUtf(BinaryWriter writer, string str)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(str);
            ushort len = (ushort)bytes.Length;
            writer.Write((byte)(len >> 8));
            writer.Write((byte)(len & 0xFF));
            if (bytes.Length > 0)
            {
                writer.Write(bytes);
            }
        }

        private static void WriteInt32BigEndian(BinaryWriter writer, int value)
        {
            writer.Write((byte)((value >> 24) & 0xFF));
            writer.Write((byte)((value >> 16) & 0xFF));
            writer.Write((byte)((value >> 8) & 0xFF));
            writer.Write((byte)(value & 0xFF));
        }
    }
}
