using System.Xml.Serialization;

namespace Uchu.Core
{
    [XmlRoot("World")]
    public class WorldConfiguration : ServerConfiguration
    {
        public override ServerType Type => ServerType.World;
        
        [XmlElement] public int Zone { get; set; }
    }
}