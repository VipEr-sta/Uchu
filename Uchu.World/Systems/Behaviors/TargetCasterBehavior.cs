using System.Threading.Tasks;

namespace Uchu.World.Systems.Behaviors
{
    public class TargetCasterBehavior : BehaviorBase
    {
        public override BehaviorTemplateId Id => BehaviorTemplateId.TargetCaster;
        
        public BehaviorBase Action { get; set; }
        
        public override async Task BuildAsync()
        {
            Action = await GetBehavior("action");
        }

        public override async Task ExecuteAsync(ExecutionContext context, ExecutionBranchContext branch)
        {
            await base.ExecuteAsync(context, branch);

            branch.Target = context.Associate;

            await Action.ExecuteAsync(context, branch);
        }
    }
}