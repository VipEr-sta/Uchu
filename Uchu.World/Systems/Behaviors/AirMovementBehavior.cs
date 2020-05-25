using System.Threading.Tasks;
using Uchu.Core;

namespace Uchu.World.Systems.Behaviors
{
    public class AirMovementBehavior : BehaviorBase
    {
        public override BehaviorTemplateId Id => BehaviorTemplateId.AirMovement;
        
        public BehaviorBase HitAction { get; set; }
        
        public BehaviorBase HitEnemyAction { get; set; }
        
        public BehaviorBase GroundAction { get; set; }
        
        public BehaviorBase TimeoutAction { get; set; }
        
        public override async Task BuildAsync()
        {
            HitAction = await GetBehavior("hit_action");

            HitEnemyAction = await GetBehavior("hit_action_enemy");

            GroundAction = await GetBehavior("ground_action");

            TimeoutAction = await GetBehavior("timeout_action");
        }

        public override async Task ExecuteAsync(ExecutionContext context, ExecutionBranchContext branch)
        {
            await base.ExecuteAsync(context, branch);

            var handle = branch.Reader.Read<uint>();
            
            context.DebugMessage($"[{BehaviorId}] Air: Left: {(branch.Reader.BaseStream.Length - branch.Reader.BaseStream.Position)}");

            RegisterHandle(handle, context, branch);
        }

        public override async Task SyncAsync(ExecutionContext context, ExecutionBranchContext branch)
        {
            var actionId = branch.Reader.Read<uint>();

            var action = await GetBehavior(actionId);

            var id = branch.Reader.Read<ulong>();

            if (context.Associate.Zone.TryGetGameObject((long) id, out var target))
            {
                branch.Target = target;
            }

            Logger.Information($"[{BehaviorId}] Air: {action.BehaviorId} ; ({id}) {branch.Target}");
            
            context.DebugMessage($"[{BehaviorId}] Air: {action.BehaviorId} ; ({id}) {branch.Target}");

            await action.ExecuteAsync(context, branch);
        }
    }
}