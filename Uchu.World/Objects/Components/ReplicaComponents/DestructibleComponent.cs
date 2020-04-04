using System;
using System.Linq;
using System.Threading.Tasks;
using RakDotNet.IO;
using Uchu.Core;
using Uchu.Core.Client;

namespace Uchu.World
{
    [RequireComponent(typeof(Stats))]
    public class DestructibleComponent : ReplicaComponent
    {
        private Stats Stats { get; set; }

        public override ComponentId Id => ComponentId.DestructibleComponent;

        /// <summary>
        ///     Warning: Should not be used as a definitive client state. Could be unreliable.
        /// </summary>
        public bool Alive { get; private set; } = true;

        public float ResurrectTime { get; set; }

        /// <summary>
        ///     Killer, Loot Owner
        /// </summary>
        public Event<GameObject, Player> OnSmashed { get; } = new Event<GameObject, Player>();

        public Event OnResurrect { get; } = new Event();

        protected DestructibleComponent()
        {
            Listen(OnStart, () =>
            {
                if (GameObject.Settings.TryGetValue("respawn", out var resTimer))
                {
                    ResurrectTime = resTimer switch
                    {
                        uint timer => timer,
                        float timer => timer,
                        int timer => timer,
                        _ => ResurrectTime
                    };
                }

                GameObject.Layer = StandardLayer.Smashable;

                GameObject.AddComponent<LootContainerComponent>();

                Stats = GameObject.GetComponent<Stats>();

                Stats.HasStats = false;

                Listen(Stats.OnDeath, async () =>
                {
                    await SmashAsync(
                        Stats.LatestDamageSource,
                        Stats.LatestDamageSource is Player player ? player : default
                    );
                });
            });

            Listen(OnDestroyed, () =>
            {
                OnResurrect.Clear();

                OnSmashed.Clear();
            });
        }

        public override void Construct(BitWriter writer)
        {
            writer.WriteBit(false);
            writer.WriteBit(false);

            GameObject.GetComponent<Stats>().Construct(writer);
        }

        public override void Serialize(BitWriter writer)
        {
            GameObject.GetComponent<Stats>().Serialize(writer);
        }

        public async Task SmashAsync(GameObject smasher, Player owner = default, string animation = default)
        {
            if (!Alive) return;

            Alive = false;

            owner ??= smasher as Player;

            if (owner != null)
            {
                var missionInventoryComponent = owner.GetComponent<MissionInventoryComponent>();

                if (missionInventoryComponent == default) return;

                await missionInventoryComponent.SmashAsync(GameObject.Lot);
            }

            Zone.BroadcastMessage(new DieMessage
            {
                Associate = GameObject,
                DeathType = animation ?? "",
                Killer = smasher,
                SpawnLoot = false,
                LootOwner = owner ?? GameObject
            });

            if (GameObject is Player)
            {
                //
                // Player
                //

                var coinToDrop = Math.Min((long) Math.Round(As<Player>().Currency * 0.1), 10000);
                As<Player>().Currency -= coinToDrop;

                InstancingUtil.Currency((int) coinToDrop, owner, owner, Transform.Position);

                return;
            }

            //
            // Normal Smashable
            //

            if (owner == null)
            {
                OnSmashed.Invoke(smasher, default);

                return;
            }

            GameObject.Layer -= StandardLayer.Smashable;
            GameObject.Layer += StandardLayer.Hidden;

            InitializeRespawn();

            var container = GameObject.GetComponent<LootContainerComponent>();

            await container.CollectDetailsAsync();

            foreach (var lot in container.GenerateLootYields())
            {
                var drop = InstancingUtil.Loot(lot, owner, GameObject, Transform.Position);

                Start(drop);
            }

            var currency = container.GenerateCurrencyYields();

            if (currency > 0)
            {
                InstancingUtil.Currency(currency, owner, GameObject, Transform.Position);
            }

            OnSmashed.Invoke(smasher, owner);
        }

        private void InitializeRespawn()
        {
            Task.Run(async () =>
            {
                await Task.Delay((int) (ResurrectTime * 1000));

                Resurrect();
            });
        }

        public void Resurrect()
        {
            Alive = true;

            if (GameObject is Player)
            {
                Zone.BroadcastMessage(new ResurrectMessage
                {
                    Associate = As<Player>()
                });

                Stats.Health = Math.Min(Stats.MaxHealth, 4);
            }
            else
            {
                Stats.Health = Stats.MaxHealth;

                GameObject.Layer += StandardLayer.Smashable;
                GameObject.Layer -= StandardLayer.Hidden;
            }

            OnResurrect.Invoke();
        }
    }
}