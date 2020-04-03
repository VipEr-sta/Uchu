using System.Xml.Serialization;

namespace Uchu.Core
{
    [XmlRoot("Python")]
    public class PythonConfiguration
    {
        [XmlElement("Script")] public string[] Scripts { get; set; }
        
        [XmlElement("Library")] public string[] Paths { get; set; }
    }
}