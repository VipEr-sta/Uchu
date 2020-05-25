using System;
using System.Threading.Tasks;
using Uchu.Core;

namespace Uchu.World.Systems.Behaviors
{
    public class BasicAttackBehavior : BehaviorBase
    {
        public override BehaviorTemplateId Id => BehaviorTemplateId.BasicAttack;
        
        public BehaviorBase OnSuccess { get; set; }
        
        public int MinDamage { get; set; }
        
        public int MaxDamage { get; set; }
        
        public override async Task BuildAsync()
        {
            OnSuccess = await GetBehavior("on_success");

            MinDamage = await GetParameter<int>("min damage");

            MaxDamage = await GetParameter<int>("max damage");

            if (MinDamage == 0)
            {
                MinDamage = 1;
            }

            if (MaxDamage == 0)
            {
                MaxDamage = 1;
            }
        }

        public override async Task ExecuteAsync(ExecutionContext context, ExecutionBranchContext branch)
        {
            await base.ExecuteAsync(context, branch);
            
            branch.Reader.Align();

            branch.Reader.Read<ushort>();

            var failImmune = branch.Reader.ReadBit();

            if (!failImmune)
            {
                var unknownBit = branch.Reader.ReadBit();

                if (!unknownBit)
                {
                    var unknownFlag = branch.Reader.ReadBit();

                    if (unknownFlag)
                    {
                        branch.Reader.Read<uint>();
                    }
                }
            }

            var damage = branch.Reader.Read<uint>();

            if (branch.Target != default && branch.Target.TryGetComponent<Stats>(out var stats))
            {
                var _ = Task.Run(() =>
                {
                    stats.Damage(damage, context.Associate);
                });
            }
            
            var success = branch.Reader.ReadBit();
            
            context.DebugMessage($"[{BehaviorId}] Basic: {damage} ; {success}");
            
            if (success)
            {
                await OnSuccess.ExecuteAsync(context, branch);
            }
        }

        public override async Task CalculateAsync(NpcExecutionContext context, ExecutionBranchContext branchContext)
        {
            if (context.IsValidTarget(branchContext.Target))
            {
                context.Associate.Transform.LookAt(branchContext.Target.Transform.Position);

                await branchContext.Target.NetFavorAsync();
            }

            context.Writer.Align();

            context.Writer.Write<ushort>(0);
            
            context.Writer.WriteBit(false);
            context.Writer.WriteBit(false);
            context.Writer.WriteBit(true);
            
            context.Writer.Write(0);

            var success = context.IsValidTarget(branchContext.Target) && context.Alive;

            var damage = (uint) (success ? MinDamage : 0);

            context.Writer.Write(damage);
            
            context.Writer.WriteBit(success);

            if (success)
            {
                await PlayFxAsync("onhit", branchContext.Target, 1000);

                var stats = branchContext.Target.GetComponent<Stats>();

                stats.Damage(damage, context.Associate, EffectHandler);
                
                await OnSuccess.CalculateAsync(context, branchContext);
            }
        }
    }
}