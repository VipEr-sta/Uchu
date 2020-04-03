using System.Xml.Serialization;

namespace Uchu.Core
{
    [XmlRoot("Modules")]
    public class ModuleConfiguration
    {
        [XmlElement("Dotnet")] public string DotNetPath { get; set; } = "dotnet";

        [XmlElement("Instance")] public string Instance { get; set; } = "Uchu.Instance.dll";
        
        [XmlElement("Scripts")] public string[] ScriptDllSource { get; set; } = {
            "Uchu.StandardScripts.dll"
        };
    }
}