using System.Threading.Tasks;

namespace Uchu.World.Systems.Behaviors
{
    public class EmptyBehavior : BehaviorBase
    {
        public override BehaviorTemplateId Id => BehaviorTemplateId.Empty;

        public override Task BuildAsync()
        {
            return Task.CompletedTask;
        }

        public override BehaviorExecutionParameters DeserializeStart(ExecutionContext context, ExecutionBranchContext branchContext)
        {
            return new BehaviorExecutionParameters(context, branchContext);
        }

        public override BehaviorExecutionParameters DeserializeSync(ExecutionContext context, ExecutionBranchContext branchContext)
        {
            return new BehaviorExecutionParameters(context, branchContext);
        }
    }
}