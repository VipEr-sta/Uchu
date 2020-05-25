using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Uchu.Core;

namespace Uchu.World.Systems.Behaviors
{
    public class AreaOfEffect : BehaviorBase
    {
        public override BehaviorTemplateId Id => BehaviorTemplateId.AreaOfEffect;
        
        public BehaviorBase Action { get; set; }
        
        public int MaxTargets { get; set; }
        
        public float Radius { get; set; }
        
        public override async Task BuildAsync()
        {
            Action = await GetBehavior("action");

            MaxTargets = await GetParameter<int>("max targets");

            Radius = await GetParameter<float>("radius");
        }

        public override async Task ExecuteAsync(ExecutionContext context, ExecutionBranchContext branch)
        {
            await base.ExecuteAsync(context, branch);

            var length = branch.Reader.Read<uint>();
            
            context.DebugMessage($"[{BehaviorId}] Area: {length}");

            if (length > MaxTargets)
            {
                length = (uint) MaxTargets;
            }

            var targets = new List<GameObject>();

            for (var i = 0; i < length; i++)
            {
                var id = branch.Reader.Read<long>();

                if (!context.Associate.Zone.TryGetGameObject(id, out var target))
                {
                    context.DebugMessage($"Invalid target: {id}");
                    
                    throw new Exception($"Invalid area of effect target: {id}");
                }
                
                context.DebugMessage($"[{BehaviorId}] Area: {target}");

                targets.Add(target);
            }
            
            foreach (var target in targets)
            {
                branch.Target = target;
                
                await Action.ExecuteAsync(context, branch);
            }
        }

        public override async Task CalculateAsync(NpcExecutionContext context, ExecutionBranchContext branchContext)
        {    
            if (!context.Associate.TryGetComponent<BaseCombatAiComponent>(out var baseCombatAiComponent)) return;

            var validTarget = baseCombatAiComponent.SeekValidTargets();

            var sourcePosition = context.CalculatingPosition;

            var targets = validTarget.Where(target =>
            {
                var transform = target.Transform;

                var distance = Vector3.Distance(transform.Position, sourcePosition);

                var valid = distance <= Radius;

                return valid;
            }).ToArray();

            if (targets.Length > 0)
                context.FoundTarget = true;

            context.Writer.Write((uint) targets.Length);

            foreach (var target in targets)
            {
                context.Writer.Write(target);
            }

            foreach (var target in targets)
            {
                await Action.CalculateAsync(context, new ExecutionBranchContext
                {
                    Target = target,
                    Duration = branchContext.Duration
                });
            }
        }
    }
}