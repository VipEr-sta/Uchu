using System.Threading.Tasks;

namespace Uchu.World.Systems.Behaviors
{
    public class SwitchBehavior : BehaviorBase
    {
        public override BehaviorTemplateId Id => BehaviorTemplateId.Switch;

        public BehaviorBase Action { get; set; }
        
        public int Imagination { get; set; }
        
        public bool IsEnemyFaction { get; set; }
        
        public BehaviorBase ActionFalse { get; set; }
        
        public BehaviorBase ActionTrue { get; set; }
        
        public override async Task BuildAsync()
        {
            Action = await GetBehavior("action");
            ActionFalse = await GetBehavior("action_false");
            ActionTrue = await GetBehavior("action_true");

            Imagination = await GetParameter<int>("imagination");

            IsEnemyFaction = (await GetParameter("isEnemyFaction"))?.Value > 0;
        }

        public override async Task ExecuteAsync(ExecutionContext context, ExecutionBranchContext branch)
        {
            await base.ExecuteAsync(context, branch);

            var state = true;

            if (Imagination > 0 || !IsEnemyFaction)
            {
                state = branch.Reader.ReadBit();
            }

            if (state)
            {
                await ActionTrue.ExecuteAsync(context, branch);
            }
            else
            {
                await ActionFalse.ExecuteAsync(context, branch);
            }
        }

        public override async Task CalculateAsync(NpcExecutionContext context, ExecutionBranchContext branchContext)
        {
            var state = true;
            
            if (Imagination > 0 || !IsEnemyFaction)
            {
                state = branchContext.Target != default && context.Alive;
                
                context.Writer.WriteBit(state);
            }

            if (state)
            {
                await ActionTrue.CalculateAsync(context, branchContext);
            }
            else
            {
                await ActionFalse.CalculateAsync(context, branchContext);
            }
        }
    }
}