using System.Threading.Tasks;

namespace Uchu.World.Systems.Behaviors
{
    public class ChargeUpBehavior : BehaviorBase
    {
        public override BehaviorTemplateId Id => BehaviorTemplateId.ChargeUp;
        
        public BehaviorBase Action { get; set; }
        
        public float MaxDuration { get; set; }
        
        public override async Task BuildAsync()
        {
            Action = await GetBehavior("action");

            MaxDuration = await GetParameter<float>("max_duration");
        }
        
        public override async Task ExecuteAsync(ExecutionContext context, ExecutionBranchContext branch)
        {
            await base.ExecuteAsync(context, branch);
            
            var handle = branch.Reader.Read<uint>();
            
            RegisterHandle(handle, context, branch);
        }

        public override async Task SyncAsync(ExecutionContext context, ExecutionBranchContext branch)
        {
            await base.ExecuteAsync(context, branch);
            
            await Action.ExecuteAsync(context, branch);
        }
    }
}