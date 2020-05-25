using System.Threading.Tasks;

namespace Uchu.World.Systems.Behaviors
{
    public class DurationBehavior : BehaviorBase
    {
        public override BehaviorTemplateId Id => BehaviorTemplateId.Duration;
        
        public BehaviorBase Action { get; set; }
        
        public int ActionDuration { get; set; }
        
        public override async Task BuildAsync()
        {
            Action = await GetBehavior("action");

            var duration = await GetParameter("duration");
            
            if (duration.Value == null) return;

            ActionDuration = (int) duration.Value;
        }

        public override async Task ExecuteAsync(ExecutionContext context, ExecutionBranchContext branch)
        {
            await base.ExecuteAsync(context, branch);

            branch.Duration = ActionDuration * 1000;
            
            await Action.ExecuteAsync(context, branch);
        }

        public override async Task CalculateAsync(NpcExecutionContext context, ExecutionBranchContext branchContext)
        {
            branchContext.Duration = ActionDuration * 1000;

            await Action.CalculateAsync(context, branchContext);
        }
    }
}