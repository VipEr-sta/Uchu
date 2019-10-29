using RakDotNet.IO;

namespace Uchu.World
{
    public class PossesableComponent : ReplicaComponent
    {
        public GameObject Driver { get; set; }

        public override ComponentId Id => ComponentId.Possesable;

        public override void Construct(BitWriter writer)
        {
            Serialize(writer);
        }

        public override void Serialize(BitWriter writer)
        {
            writer.Write(true);

            var hasDriver = Driver != null;
            writer.WriteBit(hasDriver);
            if (hasDriver) writer.Write(Driver);

            writer.WriteBit(false);
            writer.WriteBit(false);
        }
    }
}