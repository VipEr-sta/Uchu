using System.Threading.Tasks;

namespace Uchu.World.Systems.Behaviors
{
    public class RepairArmorBehavior : BehaviorBase
    {
        public override BehaviorTemplateId Id => BehaviorTemplateId.RepairArmor;
        
        public int Armor { get; set; }
        
        public override async Task BuildAsync()
        {
            Armor = await GetParameter<int>("armor");
        }

        public override async Task ExecuteAsync(ExecutionContext context, ExecutionBranchContext branch)
        {
            await base.ExecuteAsync(context, branch);

            if (!branch.Target.TryGetComponent<Stats>(out var stats)) return;

            stats.Armor = (uint) ((int) stats.Armor + Armor);
        }
    }
}