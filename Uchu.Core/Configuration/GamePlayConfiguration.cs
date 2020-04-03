using System.Xml.Serialization;

namespace Uchu.Core
{
    [XmlRoot("Gameplay")]
    public class GamePlayConfiguration
    {
        [XmlElement] public bool PathFinding { get; set; }
        
        [XmlElement] public bool AiWander { get; set; }
    }
}