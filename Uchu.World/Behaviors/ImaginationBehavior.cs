using System.Threading.Tasks;

namespace Uchu.World.Behaviors
{
    public class ImaginationBehavior : BehaviorBase
    {
        public override BehaviorTemplateId Id => BehaviorTemplateId.Imagination;
        
        public int Imagination { get; set; }
        
        public override async Task BuildAsync()
        {
            Imagination = await GetParameter<int>("imagination");
        }

        public override async Task ExecuteAsync(ExecutionContext context, ExecutionBranchContext branchContext)
        {
            await base.ExecuteAsync(context, branchContext);

            if (!branchContext.Target.TryGetComponent<Stats>(out var stats)) return;

            await stats.SetImaginationAsync((uint) ((int) stats.Imagination + Imagination));
        }
    }
}