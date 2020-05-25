using System.Linq;
using System.Threading.Tasks;

namespace Uchu.World.Systems.Behaviors
{
    public class ForceMovementBehavior : BehaviorBase
    {
        public override BehaviorTemplateId Id => BehaviorTemplateId.ForceMovement;
        
        public BehaviorBase HitAction { get; set; }
        
        public BehaviorBase HitActionEnemy { get; set; }
        
        public BehaviorBase HitActionFaction { get; set; }
        
        public override async Task BuildAsync()
        {
            HitAction = await GetBehavior("hit_action");
            HitActionEnemy = await GetBehavior("hit_action_enemy");
            HitActionFaction = await GetBehavior("hit_action_faction");
        }

        public override async Task ExecuteAsync(ExecutionContext context, ExecutionBranchContext branch)
        {
            await base.ExecuteAsync(context, branch);

            var array = new[] {HitAction, HitActionEnemy, HitActionFaction};
            
            if (array.All(b => b is EmptyBehavior)) return;

            var handle = branch.Reader.Read<uint>();

            RegisterHandle(handle, context, branch);
        }

        public override async Task SyncAsync(ExecutionContext context, ExecutionBranchContext branch)
        {
            var actionId = branch.Reader.Read<uint>();

            var action = await GetBehavior(actionId);

            var target = branch.Reader.ReadGameObject(context.Associate.Zone);
            
            context.DebugMessage($"[{BehaviorId}] Force: {action.BehaviorId} ; {target}");

            await action.ExecuteAsync(context, new ExecutionBranchContext
            {
                Target = target,
                Duration = branch.Duration
            });
        }
    }
}