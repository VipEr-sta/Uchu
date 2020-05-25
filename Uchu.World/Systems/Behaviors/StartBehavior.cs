using System.Threading.Tasks;

namespace Uchu.World.Systems.Behaviors
{
    public class StartBehavior : BehaviorBase
    {
        public override BehaviorTemplateId Id => BehaviorTemplateId.Start;
        
        public BehaviorBase Action { get; set; }
        
        public int UseTarget { get; set; }
        
        public override async Task BuildAsync()
        {
            Action = await GetBehavior("action");

            UseTarget = await GetParameter<int>("use_target");
        }
        
        public override async Task ExecuteAsync(ExecutionContext context, ExecutionBranchContext branch)
        {
            await base.ExecuteAsync(context, branch);

            await Action.ExecuteAsync(context, branch);
        }
    }
}