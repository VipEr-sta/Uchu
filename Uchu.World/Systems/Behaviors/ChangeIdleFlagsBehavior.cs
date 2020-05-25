using System.Threading.Tasks;

namespace Uchu.World.Systems.Behaviors
{
    public class ChangeIdleFlagsBehavior : BehaviorBase
    {
        public override BehaviorTemplateId Id => BehaviorTemplateId.ChangeIdleFlags;
        
        public override Task BuildAsync()
        {
            return Task.CompletedTask;
        }

        public override async Task ExecuteAsync(ExecutionContext context, ExecutionBranchContext branch)
        {
            await base.ExecuteAsync(context, branch);
        }
    }
}