using System.Xml.Serialization;

namespace Uchu.Core
{
    [XmlRoot("Character")]
    public class CharacterConfiguration : ServerConfiguration
    {
        public override ServerType Type => ServerType.Character;
    }
}