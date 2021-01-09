using System.Collections.Generic;
using System.Threading.Tasks;

namespace Uchu.World.Systems.Behaviors
{
    public class AndBehaviorExecutionParameters : BehaviorExecutionParameters
    {
        public List<BehaviorExecutionParameters> BehaviorExecutionParameters { get; } 
            = new List<BehaviorExecutionParameters>();
    }
    public class AndBehavior : BehaviorBase<AndBehaviorExecutionParameters>
    {
        public override BehaviorTemplateId Id => BehaviorTemplateId.And;
        
        private BehaviorBase[] Behaviors { get; set; }
        
        public override async Task BuildAsync()
        {
            var actions = GetParameters();

            Behaviors = new BehaviorBase[actions.Length];
            for (var i = 0; i < actions.Length; i++)
            {
                Behaviors[i] = await GetBehavior($"behavior {i + 1}");
            }
        }

        protected override void DeserializeStart(AndBehaviorExecutionParameters parameters)
        {
            foreach (var behaviorBase in Behaviors)
            {
                parameters.BehaviorExecutionParameters.Add(
                    behaviorBase.DeserializeStart(parameters.Context, parameters.BranchContext));
            }
        }

        protected override Task ExecuteStart(AndBehaviorExecutionParameters parameters)
        {
            for (var i = 0; i < Behaviors.Length; i++)
            {
                var index = i;
                Task.Run(async () =>
                {
                    await Behaviors[index].ExecuteStart(parameters.BehaviorExecutionParameters[index]);
                });
            }

            return Task.CompletedTask;
        }

        protected override void SerializeStart(AndBehaviorExecutionParameters parameters)
        {
            foreach (var behaviorBase in Behaviors)
            {
                parameters.BehaviorExecutionParameters.Add(
                    behaviorBase.SerializeStart(parameters.NpcContext, parameters.BranchContext));
            }
        }

        protected override void SerializeSync(AndBehaviorExecutionParameters parameters)
        {
            for (var i = 0; i < Behaviors.Length; i++)
            {
                Behaviors[i].SerializeSync(parameters.BehaviorExecutionParameters[i]);
            }
        }

        protected override Task ExecuteSync(AndBehaviorExecutionParameters parameters)
        {
            for (var i = 0; i < Behaviors.Length; i++)
            {
                var index = i;
                Task.Run(async () =>
                {
                    await Behaviors[index].ExecuteSync(parameters.BehaviorExecutionParameters[index]);
                });
            }
            
            return Task.CompletedTask;
        }
    }
}