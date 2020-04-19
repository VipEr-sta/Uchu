using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using RakDotNet.IO;
using Uchu.Core;
using Uchu.Core.Client;
using Uchu.World.Scripting.Native;

namespace Uchu.World
{
    public class Stats : Component
    {
        public int[] Factions { get; set; } = new int[0];
        
        public int[] Enemies { get; set; } = new int[0];
        
        public uint DamageAbsorptionPoints { get; set; }
        
        public bool Immune { get; set; }
        
        public bool GameMasterImmune { get; set; }
        
        public bool Shielded { get; set; }

        public GameObject LatestDamageSource { get; private set; }

        public uint Health { get; private set; }

        public uint MaxHealth { get; private set; }

        public uint Armor { get; private set; }

        public uint MaxArmor { get; private set; }

        public uint Imagination { get; private set; }

        public uint MaxImagination { get; private set; }

        public bool Smashable { get; set; }
        
        public bool HasStats { get; set; }

        /// <summary>
        /// New Health, Delta
        /// </summary>
        public AsyncEvent<uint, int> OnHealthChanged { get; }

        /// <summary>
        /// New Armor, Delta
        /// </summary>
        public AsyncEvent<uint, int> OnArmorChanged { get; }

        /// <summary>
        /// New Imagination, Delta
        /// </summary>
        public AsyncEvent<uint, int> OnImaginationChanged { get; }

        /// <summary>
        /// New MaxHealth, Delta
        /// </summary>
        public AsyncEvent<uint, int> OnMaxHealthChanged { get; }

        /// <summary>
        /// New MaxArmor, Delta
        /// </summary>
        public AsyncEvent<uint, int> OnMaxArmorChanged { get; }

        /// <summary>
        /// New MaxImagination, Delta
        /// </summary>
        public AsyncEvent<uint, int> OnMaxImaginationChanged { get; }

        public AsyncEvent OnDeath { get; }

        protected Stats()
        {
            OnHealthChanged = new AsyncEvent<uint, int>();
            
            OnArmorChanged = new AsyncEvent<uint, int>();
            
            OnImaginationChanged = new AsyncEvent<uint, int>();
            
            OnMaxHealthChanged = new AsyncEvent<uint, int>();
            
            OnMaxArmorChanged = new AsyncEvent<uint, int>();
            
            OnMaxImaginationChanged = new AsyncEvent<uint, int>();
            
            OnDeath = new AsyncEvent();

            Listen(OnStart, async () =>
            {
                if (GameObject is Player) CollectPlayerStats();
                else await CollectObjectStatsAsync();
                
                await using var cdClient = new CdClientContext();

                var componentId = await GameObject.Lot.GetComponentIdAsync(ComponentId.DestructibleComponent);

                var destroyable = await cdClient.DestructibleComponentTable.FirstOrDefaultAsync(
                    c => c.Id == componentId
                );
                
                if (destroyable == default) return;

                Factions = new[] {destroyable.Faction ?? 1};

                var faction = await cdClient.FactionsTable.FirstOrDefaultAsync(
                    f => f.Faction == Factions[0]
                );
                
                if (faction?.EnemyList == default) return;
                
                if (string.IsNullOrWhiteSpace(faction.EnemyList)) return;

                Enemies = faction.EnemyList
                    .Replace(" ", "")
                    .Split(',')
                    .Select(int.Parse)
                    .ToArray();
            });

            Listen(OnDestroyed, () =>
            {
                OnHealthChanged.Clear();
                OnArmorChanged.Clear();
                OnImaginationChanged.Clear();
                OnHealthChanged.Clear();
                OnMaxArmorChanged.Clear();
                OnMaxImaginationChanged.Clear();
                OnDeath.Clear();
                
                return Task.CompletedTask;
            });
            
            if (!(GameObject is Player)) return;

            Listen(OnHealthChanged, async (total, delta) =>
            {
                await using var ctx = new UchuContext();

                var character = await ctx.Characters.FirstAsync(c => c.Id == GameObject.Id);

                character.CurrentHealth = (int) total;

                await ctx.SaveChangesAsync();
            });

            Listen(OnArmorChanged, async (total, delta) =>
            {
                await using var ctx = new UchuContext();

                var character = await ctx.Characters.FirstAsync(c => c.Id == GameObject.Id);

                character.CurrentArmor = (int) total;

                await ctx.SaveChangesAsync();
            });

            Listen(OnImaginationChanged, async (total, delta) =>
            {
                await using var ctx = new UchuContext();

                var character = await ctx.Characters.FirstAsync(c => c.Id == GameObject.Id);

                character.CurrentImagination = (int) total;

                await ctx.SaveChangesAsync();
            });

            Listen(OnMaxHealthChanged, async (total, delta) =>
            {
                await using var ctx = new UchuContext();

                var character = await ctx.Characters.FirstAsync(c => c.Id == GameObject.Id);

                character.MaximumHealth = (int) total;

                await ctx.SaveChangesAsync();
            });

            Listen(OnMaxArmorChanged, async (total, delta) =>
            {
                await using var ctx = new UchuContext();

                var character = await ctx.Characters.FirstAsync(c => c.Id == GameObject.Id);

                character.MaximumArmor = (int) total;

                await ctx.SaveChangesAsync();
            });

            Listen(OnMaxImaginationChanged, async (total, delta) =>
            {
                await using var ctx = new UchuContext();

                var character = await ctx.Characters.FirstAsync(c => c.Id == GameObject.Id);

                character.MaximumImagination = (int) total;

                await ctx.SaveChangesAsync();
            });
        }

        public async Task SetHealthAsync(uint value)
        {
            value = Math.Min(value, MaxHealth);

            if (value == Health) return;

            await OnHealthChanged.InvokeAsync(value, (int) ((int) value - Health));

            Health = value;

            GameObject.Serialize(GameObject);

            if (Health == default)
            {
                await OnDeath.InvokeAsync();
            }
        }

        public async Task SetMaxHealthAsync(uint value)
        {
            var delta = (int) ((int) value - MaxHealth);

            if (delta < 0 && Health > value)
            {
                await OnHealthChanged.InvokeAsync(value, (int) ((int) value - Health));

                Health = value;
            }
            
            await OnMaxHealthChanged.InvokeAsync(value, delta);

            MaxHealth = value;

            GameObject.Serialize(GameObject);
        }

        public async Task SetArmorAsync(uint value)
        {
            value = Math.Min(value, MaxArmor);

            if (value == Armor) return;

            await OnArmorChanged.InvokeAsync(value, (int) ((int) value - Armor));

            Armor = value;

            GameObject.Serialize(GameObject);
        }

        public async Task SetMaxArmorAsync(uint value)
        {
            var delta = (int) ((int) value - MaxArmor);

            if (delta < 0 && Armor > value)
            {
                await OnArmorChanged.InvokeAsync(value, (int) ((int) value - Armor));

                Armor = value;
            }
            
            await OnMaxArmorChanged.InvokeAsync(value, delta);

            MaxArmor = value;

            GameObject.Serialize(GameObject);
        }

        public async Task SetImaginationAsync(uint value)
        {
            value = Math.Min(value, MaxImagination);

            if (value == Imagination) return;

            await OnImaginationChanged.InvokeAsync(value, (int) ((int) value - Imagination));

            Imagination = value;

            GameObject.Serialize(GameObject);
        }

        public async Task SetMaxImaginationAsync(uint value)
        {
            var delta = (int) ((int) value - MaxImagination);

            if (delta < 0 && Imagination > value)
            {
                await OnImaginationChanged.InvokeAsync(value, (int) ((int) value - Imagination));

                Imagination = value;
            }
            
            await OnMaxImaginationChanged.InvokeAsync(value, delta);

            MaxImagination = value;

            GameObject.Serialize(GameObject);
        }

        public void Damage(uint value, GameObject source)
        {
            LatestDamageSource = source;
            
            var armorDamage = Math.Min(value, Armor);

            value -= armorDamage;
            Armor -= armorDamage;

            Health -= Math.Min(value, Health);

            if (source != default && GameObject is Player)
            {
                GameObject.Animate("onhit", true);
            }
        }

        public void Heal(uint value)
        {
            var armorHeal = Math.Min(value, MaxArmor - Armor);

            value -= armorHeal;
            Armor += armorHeal;

            Health += Math.Min(value, MaxHealth - Health);
        }

        public async Task BoostBaseHealth(uint delta)
        {
            if (!(GameObject is Player)) return;

            await using var ctx = new UchuContext();

            var character = await ctx.Characters.FirstAsync(c => c.Id == GameObject.Id);

            character.BaseHealth += (int) delta;

            MaxHealth += delta;

            Health += delta;

            await ctx.SaveChangesAsync();
        }

        public async Task BoostBaseImagination(uint delta)
        {
            if (!(GameObject is Player)) return;

            await using var ctx = new UchuContext();

            var character = await ctx.Characters.FirstAsync(c => c.Id == GameObject.Id);

            character.BaseImagination += (int) delta;

            MaxImagination += delta;

            Imagination += delta;

            await ctx.SaveChangesAsync();
        }

        private async Task CollectObjectStatsAsync()
        {
            await using var ctx = new CdClientContext();

            var componentId = await GameObject.Lot.GetComponentIdAsync(ComponentId.DestructibleComponent);
            
            var stats = await ctx.DestructibleComponentTable.FirstOrDefaultAsync(
                o => o.Id == componentId
            );

            if (stats == default) return;

            var rawHealth = stats.Life ?? 0;
            var rawArmor = (int) (stats.Armor ?? 0);
            var rawImagination = stats.Imagination ?? 0;

            Health = (uint) (rawHealth != -1 ? rawHealth : 0);
            Armor = (uint) (rawArmor != -1 ? rawArmor : 0);
            Imagination = (uint) (rawImagination != -1 ? rawImagination : 0);

            MaxHealth = Health;
            MaxArmor = Armor;
            MaxImagination = Imagination;
        }

        private void CollectPlayerStats()
        {
            if (!(GameObject is Player)) return;
            
            using var ctx = new UchuContext();

            var character = ctx.Characters.First(c => c.Id == GameObject.Id);

            /*
             * Any additional stats gets added on by skills.
             */
            
            Health = (uint) character.CurrentHealth;
            MaxHealth = (uint) character.BaseHealth;

            Armor = (uint) character.CurrentArmor;
            MaxArmor = default;

            Imagination = (uint) character.CurrentImagination;
            MaxImagination = (uint) character.BaseImagination;
        }
        
        public void Construct(BitWriter writer)
        {
            writer.WriteBit(true);

            for (var i = 0; i < 9; i++) writer.Write<uint>(0);

            WriteStats(writer);

            if (HasStats)
            {
                writer.WriteBit(false);
                writer.WriteBit(false);

                if (Smashable)
                {
                    writer.WriteBit(false);
                    writer.WriteBit(false);
                }
            }

            writer.WriteBit(true);
            writer.WriteBit(false);
        }

        public void Serialize(BitWriter writer)
        {
            WriteStats(writer);

            writer.WriteBit(true);
            writer.WriteBit(false);
        }

        private void WriteStats(BitWriter writer)
        {
            writer.WriteBit(HasStats);
            
            if (!HasStats) return;

            writer.Write(Health);
            writer.Write<float>(MaxHealth);

            writer.Write(Armor);
            writer.Write<float>(MaxArmor);

            writer.Write(Imagination);
            writer.Write<float>(MaxImagination);

            writer.Write(DamageAbsorptionPoints);
            writer.WriteBit(Immune);
            writer.WriteBit(GameMasterImmune);
            writer.WriteBit(Shielded);

            writer.Write<float>(MaxHealth);
            writer.Write<float>(MaxArmor);
            writer.Write<float>(MaxImagination);

            writer.Write((uint) Factions.Length);

            foreach (var faction in Factions) writer.Write(faction);

            writer.WriteBit(Smashable && !GameObject.TryGetComponent<QuickBuildComponent>(out _));
        }
    }
}