using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Uchu.World.Systems.Behaviors
{
    public class SwitchMultipleBehavior : BehaviorBase
    {
        public override BehaviorTemplateId Id => BehaviorTemplateId.SwitchMultiple;
        
        public float ChargeTime { get; set; }
        
        public float DistanceToTarget { get; set; }
        
        public float DefaultValue { get; set; }
        
        public Dictionary<BehaviorBase, float> Behaviors { get; set; }

        public override async Task BuildAsync()
        {
            ChargeTime = await GetParameter<float>("charge_up");

            DistanceToTarget = await GetParameter<float>("distance_to_target");

            Behaviors = new Dictionary<BehaviorBase, float>();

            var behaviors = new Dictionary<BehaviorBase, float>();

            var index = 1;
            
            while (true)
            {
                var behavior = await GetBehavior($"behavior {index}");
                
                if (behavior is EmptyBehavior) break;

                var value = await GetParameter<float>($"value {index}");

                behaviors[behavior] = value;

                index++;
            }

            DefaultValue = await GetParameter<float>("value 1");
            
            Behaviors = behaviors;
        }

        public override async Task ExecuteAsync(ExecutionContext context, ExecutionBranchContext branch)
        {
            await base.ExecuteAsync(context, branch);

            var value = branch.Reader.Read<float>();
            
            context.DebugMessage($"[{BehaviorId}] Value: {value}");
            
            if (value <= DefaultValue) value = DefaultValue;
            
            foreach (var (behavior, mark) in Behaviors)
            {
                if (value < mark) continue;

                await behavior.ExecuteAsync(context, branch);
            }
        }
    }
}