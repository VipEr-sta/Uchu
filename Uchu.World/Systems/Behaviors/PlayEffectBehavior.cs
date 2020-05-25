using System.Threading.Tasks;

namespace Uchu.World.Systems.Behaviors
{
    public class PlayEffectBehavior : BehaviorBase
    {
        public override BehaviorTemplateId Id => BehaviorTemplateId.PlayEffect;
        
        public override Task BuildAsync()
        {
            return Task.CompletedTask;
        }

        public override async Task ExecuteAsync(ExecutionContext context, ExecutionBranchContext branch)
        {
            await PlayFxAsync("", branch.Target, 1000);
        }
    }
}