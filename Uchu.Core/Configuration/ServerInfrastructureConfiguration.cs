using System.Xml.Serialization;

namespace Uchu.Core
{
    [XmlRoot("Infrastructure")]
    public class ServerInfrastructureConfiguration
    {
        [XmlElement("Authentication")] public AuthenticationConfiguration Authentication { get; set; } = new AuthenticationConfiguration
        {
            Port = 21836
        };

        [XmlElement("Character")] public CharacterConfiguration Character { get; set; } = new CharacterConfiguration
        {
            Port = 40000
        };

        [XmlElement("World")] public WorldConfiguration[] WorldConfigurations { get; set; } = {
            new WorldConfiguration
            {
                Port = 20000,
                Zone = ZoneId.Invalid
            },
            new WorldConfiguration
            {
                Port = 20001,
                Zone = ZoneId.Invalid
            },
            new WorldConfiguration
            {
                Port = 20002,
                Zone = ZoneId.Invalid
            }
        };

        [XmlElement] public int Capacity { get; set; } = 3;
    }
}