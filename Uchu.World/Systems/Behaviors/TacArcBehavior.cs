using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Uchu.Core;

namespace Uchu.World.Systems.Behaviors
{
    public class TacArcBehavior : BehaviorBase
    {
        public override BehaviorTemplateId Id => BehaviorTemplateId.TacArc;
        
        public bool CheckEnvironment { get; set; }

        public bool Blocked { get; set; }
        
        public BehaviorBase ActionBehavior { get; set; }
        
        public BehaviorBase BlockedBehavior { get; set; }
        
        public BehaviorBase MissBehavior { get; set; }
        
        public int MaxTargets { get; set; }

        public float MaxRange { get; set; }
        
        public bool UsePickedTarget { get; set; }
        
        public override async Task BuildAsync()
        {
            CheckEnvironment = (await GetParameter("check_env"))?.Value > 0;
            Blocked = await GetParameter("blocked action") != default;

            ActionBehavior = await GetBehavior("action");
            BlockedBehavior = await GetBehavior("blocked action");
            MissBehavior = await GetBehavior("miss action");

            MaxTargets = await GetParameter<int>("max targets");

            MaxRange = await GetParameter<float>("max range");

            UsePickedTarget = await GetParameter<int>("use_picked_target") > 0;
        }

        public override async Task ExecuteAsync(ExecutionContext context, ExecutionBranchContext branch)
        {
            await base.ExecuteAsync(context, branch);

            if (branch.Target != null && UsePickedTarget)
            {
                {
                    var distance = Vector3.Distance(
                        context.Associate.Transform.Position,
                        branch.Target.Transform.Position
                    );

                    if (distance > MaxRange)
                    {
                        branch.Target = default;
                    }
                }

                if (branch.Target != null)
                {
                    await ActionBehavior.ExecuteAsync(context, branch);

                    return;
                }
            }

            var hit = branch.Reader.ReadBit();
            
            if (hit) // Hit
            {
                var targets = new List<GameObject>();

                if (CheckEnvironment)
                {
                    branch.Reader.ReadBit();
                }

                var specifiedTargets = branch.Reader.Read<uint>();

                for (var i = 0; i < specifiedTargets; i++)
                {
                    var targetId = branch.Reader.Read<long>();

                    if (!context.Associate.Zone.TryGetGameObject(targetId, out var target))
                    {
                        Logger.Error($"{context.Associate} sent invalid TacArc target: {targetId}");

                        continue;
                    }

                    targets.Add(target);
                }

                foreach (var target in targets)
                {
                    branch.Target = target;

                    await ActionBehavior.ExecuteAsync(context, branch);
                }
            }
            else
            {
                if (Blocked)
                {
                    var isBlocked = branch.Reader.ReadBit();

                    if (isBlocked) // Is blocked
                    {
                        await BlockedBehavior.ExecuteAsync(context, branch);
                    }
                    else
                    {
                        await MissBehavior.ExecuteAsync(context, branch);
                    }
                }
                else
                {
                    await MissBehavior.ExecuteAsync(context, branch);
                }
            }
        }

        public override async Task CalculateAsync(NpcExecutionContext context, ExecutionBranchContext branchContext)
        {
            if (!context.Associate.TryGetComponent<BaseCombatAiComponent>(out var baseCombatAiComponent)) return;

            var validTarget = baseCombatAiComponent.SeekValidTargets();

            var sourcePosition = context.CalculatingPosition; // Change back to author position?

            var targets = validTarget.Where(target =>
            {
                var transform = target.Transform;

                var distance = Vector3.Distance(transform.Position, sourcePosition);

                return distance <= context.MaxRange && context.MinRange <= distance;
            }).ToList();

            targets.ToList().Sort((g1, g2) =>
            {
                var distance1 = Vector3.Distance(g1.Transform.Position, sourcePosition);
                var distance2 = Vector3.Distance(g2.Transform.Position, sourcePosition);

                return (int) (distance1 - distance2);
            });

            var selectedTargets = new List<GameObject>();

            foreach (var target in targets)
            {
                if (selectedTargets.Count < MaxTargets)
                {
                    selectedTargets.Add(target);
                }
            }

            if (!context.Alive)
            {
                selectedTargets.Clear(); // No targeting if dead
            }

            var any = selectedTargets.Any();

            context.Writer.WriteBit(any); // Hit

            if (any)
            {
                baseCombatAiComponent.Target = selectedTargets.First();
                
                context.FoundTarget = true;

                if (CheckEnvironment)
                {
                    // TODO
                    context.Writer.WriteBit(false);
                }

                context.Writer.Write((uint) selectedTargets.Count);

                foreach (var target in selectedTargets)
                {
                    context.Writer.Write(target.Id);
                }

                foreach (var target in selectedTargets)
                {
                    await ActionBehavior.CalculateAsync(context, new ExecutionBranchContext
                    {
                        Target = target,
                        Duration = branchContext.Duration
                    });
                }
            }
            else
            {
                if (Blocked)
                {
                    // TODO
                    context.Writer.WriteBit(false);
                }
                else
                {
                    await MissBehavior.CalculateAsync(context, branchContext);
                }
            }
        }
    }
}