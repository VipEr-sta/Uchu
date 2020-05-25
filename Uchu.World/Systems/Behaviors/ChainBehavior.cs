using System.Collections.Generic;
using System.Threading.Tasks;

namespace Uchu.World.Systems.Behaviors
{
    public class ChainBehavior : BehaviorBase
    {
        public override BehaviorTemplateId Id => BehaviorTemplateId.Chain;
        
        public int Delay { get; set; }
        
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
            
            var delay = await GetParameter("chain_delay");
            
            if (delay.Value == null) return;

            Delay = (int) delay.Value;
        }

        public override async Task ExecuteAsync(ExecutionContext context, ExecutionBranchContext branch)
        {
            await base.ExecuteAsync(context, branch);
            
            var chainIndex = branch.Reader.Read<uint>();

            await Behaviors[chainIndex - 1].ExecuteAsync(context, branch);
        }

        public override async Task CalculateAsync(NpcExecutionContext context, ExecutionBranchContext branchContext)
        {
            // TODO
            
            context.Writer.Write(1);
            
            await Behaviors[1 - 1].CalculateAsync(context, branchContext);
        }
    }
}