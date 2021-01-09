using RakDotNet.IO;
using Uchu.Core;

namespace Uchu.Auth
{
    public class ServerLoginInfoPacket : Packet
    {
        public override RemoteConnectionType RemoteConnectionType => RemoteConnectionType.Server;

        public override uint PacketId => 0x0;

        public LoginCode LoginCode { get; set; }

        public string[] GatingStrings { get; set; } = { "guilds", "test" };

        public GameVersion Version { get; set; } = new GameVersion { Major = 1, Current = 10, Minor = 64 };

        public string UserKey { get; set; } = "";

        public string CharacterInstanceAddress { get; set; }

        public string ChatInstanceAddress { get; set; }

        public ushort CharacterInstancePort { get; set; }

        public ushort ChatInstancePort { get; set; }

        public string UnknownIp { get; set; } = "127.0.0.1";

        public string LegoUUID { get; set; } = "00000000-0000-0000-0000-000000000000";

        public string Locale { get; set; } = "US";

        public bool FirstLoginWithSubscription { get; set; }

        public bool FreeToPlay { get; set; }

        public ErrorMessage Error { get; set; } = new ErrorMessage { Message = null };

        public uint StampCount { get; set; } = 4;

        // TODO: add stamps

        public struct GameVersion
        {
            public ushort Major { get; set; }

            public ushort Current { get; set; }

            public ushort Minor { get; set; }
        }

        public class ErrorMessage : ISerializable
        {
            public string Message { get; set; }

            public void Serialize(BitWriter writer)
            {
                if (!string.IsNullOrEmpty(Message))
                {
                    writer.Write((ushort)Message.Length);
                    writer.WriteString(Message, Message.Length, true);
                }
                else
                {
                    writer.Write<ushort>(0);
                }
            }

            public void Deserialize(BitReader reader)
            {
                var length = reader.Read<uint>();

                Message = length > 0 ? reader.ReadString((int)length, true) : null;
            }
        }

        public override void SerializePacket(BitWriter writer)
        {
            writer.Write((byte)LoginCode);

            foreach (string item in GatingStrings)
            {
                writer.WriteString(item);
            }

            writer.WriteString("", 33 * (8 - GatingStrings.Length));

            writer.Write(Version);

            writer.WriteString(UserKey, wide: true);

            writer.WriteString(CharacterInstanceAddress);

            writer.WriteString(ChatInstanceAddress);
            writer.Write(CharacterInstancePort);

            writer.Write(ChatInstancePort);
            writer.WriteString(UnknownIp);

            writer.WriteString(LegoUUID, 37);

            writer.Write<uint>(0);

            writer.WriteString(Locale, 3);

            writer.Write((byte)(FirstLoginWithSubscription ? 1 : 0));

            writer.Write((byte)(FreeToPlay ? 1 : 0));

            writer.Write<ulong>(0);

            writer.Write(Error);

            writer.Write(StampCount);
        }
    }
}