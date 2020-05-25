using RakDotNet.IO;

namespace Uchu.World.Systems.Behaviors
{
    public struct ExecutionBranchContext
    {
        public bool FallowUp { get; set; }
        
        public BitReader Reader { get; set; }
        
        public GameObject Target { get; set; }

        public int Duration { get; set; }
    }
}