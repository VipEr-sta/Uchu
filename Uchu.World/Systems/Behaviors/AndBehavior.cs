using System.Collections.Generic;
using System.Threading.Tasks;

namespace Uchu.World.Systems.Behaviors
{
    public class AndBehavior : BehaviorBase
    {
        public override BehaviorTemplateId Id => BehaviorTemplateId.And;
        
        public BehaviorBase[] Behaviors { get; set; }
        
        public override async Task BuildAsync()
        {
            var actions = GetParameters();

            var behaviors = new List<BehaviorBase>();

            foreach (var action in actions)
            {
                if (action.ParameterID.StartsWith("behavior"))
                {
                    behaviors.Add(await GetBehavior(action.ParameterID));
                }
            }
            
            Behaviors = behaviors.ToArray();
        }

        public override async Task ExecuteAsync(ExecutionContext context, ExecutionBranchContext branch)
        {
            await base.ExecuteAsync(context, branch);
            
            foreach (var behavior in Behaviors)
            {
                await behavior.ExecuteAsync(context, branch);
            }
        }

        public override async Task CalculateAsync(NpcExecutionContext context, ExecutionBranchContext branchContext)
        {
            foreach (var behavior in Behaviors)
            {
                await behavior.CalculateAsync(context, branchContext);
            }
        }
    }
}