using System.Xml.Serialization;

namespace Uchu.Core
{
    [XmlRoot("Authentication")]
    public class AuthenticationConfiguration : ServerConfiguration
    {
        public override ServerType Type => ServerType.Authentication;
    }
}