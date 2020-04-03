using System.Xml.Serialization;

namespace Uchu.Core
{
    [XmlRoot("Security")]
    public class SecurityConfiguration
    {
        [XmlElement] public string Certificate { get; set; } = "";
        
        [XmlElement] public string Hostname { get; set; } = "";
    }
}