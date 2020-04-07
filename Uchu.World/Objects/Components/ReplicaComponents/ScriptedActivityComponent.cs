using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using RakDotNet.IO;
using Uchu.Core;
using Uchu.Core.Client;

namespace Uchu.World
{
    public class ScriptedActivityComponent : ReplicaComponent
    {
        private readonly Random _random;
        
        public readonly List<GameObject> Participants = new List<GameObject>();

        public float[] Parameters { get; set; } = new float[10];

        public override ComponentId Id => ComponentId.ScriptedActivityComponent;

        public Activities ActivityInfo { get; private set; }
        
        public ActivityRewards[] Rewards { get; private set; }

        protected ScriptedActivityComponent()
        {
            _random = new Random();
            
            Listen(OnStart, async () =>
            {
                if (!GameObject.Settings.TryGetValue("activityID", out var id))
                {
                    return;
                }

                var activityId = (int) id;
                await using var cdClient = new CdClientContext();

                ActivityInfo = await cdClient.ActivitiesTable.FirstOrDefaultAsync(
                    a => a.ActivityID == activityId
                );

                if (ActivityInfo == default) return;
                
                ActivityInfo = await cdClient.ActivitiesTable.FirstOrDefaultAsync(
                    a => a.ActivityID == activityId
                );

                if (ActivityInfo == default)
                {
                    Logger.Error($"{GameObject} has an invalid activityID: {activityId}");
                    return;
                }

                Rewards = cdClient.ActivityRewardsTable.Where(
                    a => a.ObjectTemplate == activityId
                ).ToArray();
            });
        }

        public async Task DropLootAsync(Player owner)
        {
            var container = await GameObject.AddComponentAsync<LootContainerComponent>();

            await container.CollectDetailsAsync();

            foreach (var lot in container.GenerateLootYields())
            {
                var drop = await InstancingUtilities.InstantiateLootAsync(lot, owner, GameObject, Transform.Position);

                await StartAsync(drop);
            }

            var currency = container.GenerateCurrencyYields();

            if (currency > 0)
            {
                InstancingUtilities.InstantiateCurrency(currency, owner, GameObject, Transform.Position);
            }
        }

        public override void Construct(BitWriter writer)
        {
            Serialize(writer);
        }

        public override void Serialize(BitWriter writer)
        {
            writer.WriteBit(true);
            writer.Write((uint) Participants.Count);

            foreach (var contributor in Participants)
            {
                writer.Write(contributor);

                foreach (var parameter in Parameters) writer.Write(parameter);
            }
        }
    }
}