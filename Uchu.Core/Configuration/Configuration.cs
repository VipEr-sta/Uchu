using System.Xml.Serialization;

namespace Uchu.Core
{
    [XmlRoot("Uchu")]
    public class Configuration
    {
        [XmlElement]
        public DatabaseConfiguration DatabaseConfiguration { get; set; } = new DatabaseConfiguration
        {
            Provider = "postgres",
            Database = "uchu",
            Host = "localhost",
            Username = "postgres",
            Password = "postgres"
        };

        [XmlElement]
        public LoggingConfiguration ConsoleLoggingConfiguration { get; set; } = new LoggingConfiguration
        {
            Level = LogLevel.Debug.ToString()
        };

        [XmlElement]
        public LoggingConfiguration FileLoggingConfiguration { get; set; } = new LoggingConfiguration
        {
            Level = LogLevel.None.ToString(),
            File = "uchu.log"
        };

        [XmlElement] public ModuleConfiguration ModuleConfiguration { get; set; } = new ModuleConfiguration();

        [XmlElement] public PythonConfiguration PythonConfiguration { get; set; } = new PythonConfiguration();

        [XmlElement] public ResourceConfiguration ResourceConfiguration { get; set; } = new ResourceConfiguration();
        
        [XmlElement] public ServerInfrastructureConfiguration InfrastructureConfiguration { get; set; } = new ServerInfrastructureConfiguration();

        [XmlElement] public SecurityConfiguration SecurityConfiguration { get; set; } = new SecurityConfiguration();

        [XmlElement] public GamePlayConfiguration GamePlayConfiguration { get; set; } = new GamePlayConfiguration();
        
        [XmlElement] public ApiConfiguration ApiConfiguration { get; set; } = new ApiConfiguration();
        
        [XmlElement] public SsoConfiguration SsoConfiguration { get; set; } = new SsoConfiguration();
    }
}