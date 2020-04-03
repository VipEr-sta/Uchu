using System.Xml.Serialization;

namespace Uchu.Core
{
    [XmlRoot("Resources")]
    public class ResourceConfiguration
    {
        [XmlElement] public string Root { get; set; } = "/res";

        [XmlElement] public string Maps { get; set; } = "maps";

        [XmlElement] public string Names { get; set; } = "names";
    }
}