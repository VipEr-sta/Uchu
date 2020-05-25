using System.Threading.Tasks;

namespace Uchu.World.Systems.Behaviors
{
    public class AlterCooldownBehavior : BehaviorBase
    {
        public override BehaviorTemplateId Id => BehaviorTemplateId.AlterCooldown;
        
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