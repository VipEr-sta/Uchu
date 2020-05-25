using System;
using System.Threading.Tasks;

namespace Uchu.World.Systems.Behaviors
{
    public class MovementSwitchBehavior : BehaviorBase
    {
        public override BehaviorTemplateId Id => BehaviorTemplateId.MovementSwitch;
        
        public BehaviorBase GroundBehavior { get; set; }
        
        public BehaviorBase JumpBehavior { get; set; }
        
        public BehaviorBase FallingBehavior { get; set; }
        
        public BehaviorBase DoubleJumpBehavior { get; set; }
        
        public BehaviorBase JetpackBehavior { get; set; }

        public override async Task BuildAsync()
        {
            GroundBehavior = await GetBehavior("ground_action");
            JumpBehavior = await GetBehavior("jump_action");
            FallingBehavior = await GetBehavior("falling_action");
            DoubleJumpBehavior = await GetBehavior("double_jump_action");
            JetpackBehavior = await GetBehavior("ground_action");
        }

        public override async Task ExecuteAsync(ExecutionContext context, ExecutionBranchContext branch)
        {
            await base.ExecuteAsync(context, branch);
            
            var movementType = (MovementType) branch.Reader.Read<uint>();

            switch (movementType)
            {
                case MovementType.Ground:
                    await GroundBehavior.ExecuteAsync(context, branch);
                    return;
                case MovementType.Jump:
                    await JumpBehavior.ExecuteAsync(context, branch);
                    return;
                case MovementType.Falling:
                    await FallingBehavior.ExecuteAsync(context, branch);
                    return;
                case MovementType.DoubleJump:
                    await DoubleJumpBehavior.ExecuteAsync(context, branch);
                    return;
                case MovementType.Jetpack:
                    await JetpackBehavior.ExecuteAsync(context, branch);
                    return;
                case MovementType.Stunned:
                    return;
                default:
                    throw new Exception($"Invalid {nameof(movementType)}! Got {movementType}!");
            }
        }
    }
}