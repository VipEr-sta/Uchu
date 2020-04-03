using System.Xml.Serialization;

namespace Uchu.Core
{
    [XmlRoot("Database")]
    public class DatabaseConfiguration
    {
        [XmlElement] public string Provider { get; set; }
        
        [XmlElement] public string Database { get; set; }

        [XmlElement] public string Host { get; set; }

        [XmlElement] public string Username { get; set; }

        [XmlElement] public string Password { get; set; }
    }
}