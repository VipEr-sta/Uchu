using System.Xml.Serialization;

namespace Uchu.Core
{
    [XmlRoot("Sso")]
    public class SsoConfiguration
    {
        [XmlElement] public string Domain { get; set; } = "";
    }
}