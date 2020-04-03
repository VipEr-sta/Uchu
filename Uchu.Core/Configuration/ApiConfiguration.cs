using System.Xml.Serialization;

namespace Uchu.Core
{
    [XmlRoot("Api")]
    public class ApiConfiguration
    {
        [XmlElement] public string Protocol { get; set; } = "http";
        
        [XmlElement] public string Domain { get; set; } = "localhost";

        [XmlElement] public int Port { get; set; } = 10000;
    }
}