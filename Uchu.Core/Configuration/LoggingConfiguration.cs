using System.Xml.Serialization;

namespace Uchu.Core
{
    [XmlRoot("Logging")]
    public class LoggingConfiguration
    {
        [XmlElement] public string Level { get; set; }

        [XmlElement] public string File { get; set; }

        [XmlElement] public bool Timestamp { get; set; } = true;
    }
}