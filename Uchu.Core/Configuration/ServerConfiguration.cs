using System.Xml.Serialization;

namespace Uchu.Core
{
    public abstract class ServerConfiguration
    {
        [XmlIgnore] public abstract ServerType Type { get; }

        [XmlElement] public bool Active { get; set; } = true;
        
        [XmlElement] public int Port { get; set; }
    }
}