using System.Threading.Tasks;

namespace Uchu.World.Systems.Behaviors
{
    public class StunBehavior : BehaviorBase
    {
        public override BehaviorTemplateId Id => BehaviorTemplateId.Stun;
        
        public int StunCaster { get; set; }
        
        public override async Task BuildAsync()
        {
            StunCaster = await GetParameter<int>("stun_caster");
        }

        public override async Task ExecuteAsync(ExecutionContext context, ExecutionBranchContext branch)
        {
            await base.ExecuteAsync(context, branch);
            
            if (StunCaster == 1 || branch.Target == context.Associate) return;

            branch.Reader.ReadBit();
        }

        public override async Task CalculateAsync(NpcExecutionContext context, ExecutionBranchContext branchContext)
        {
            if (StunCaster == 1 || branchContext.Target == context.Associate) return;

            context.Writer.WriteBit(false);
        }
    }
}