using System;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using InfectedRose.Luz;
using InfectedRose.Lvl;
using Uchu.Core;

namespace Uchu.World
{
    public static class InstancingUtilities
    {
        private static readonly Random Random = new Random();
        
        public static async Task<GameObject> InstantiateSpawnerAsync(LevelObjectTemplate levelObject, Object parent)
        {
            if (!levelObject.LegoInfo.TryGetValue("spawntemplate", out var spawnTemplate))
            {
                Logger.Error("Instantiating a spawner without a \"spawntemplete\" is now allowed.");
                return null;
            }

            if (spawnTemplate is string s)
            {
                spawnTemplate = int.Parse(s);
            }

            var instance = await GameObject.InstantiateAsync(
                parent,
                position: levelObject.Position,
                rotation: levelObject.Rotation,
                scale: -1,
                objectId: (long) levelObject.ObjectId,
                lot: levelObject.Lot
            );

            if (levelObject.LegoInfo.TryGetValue("trigger_id", out var trigger))
            {
                Logger.Debug($"SPAWN TRIGGER: {trigger}");
            }

            var spawnerComponent = await instance.AddComponentAsync<SpawnerComponent>();

            spawnerComponent.Settings = levelObject.LegoInfo;
            spawnerComponent.SpawnTemplate = new Lot((int) spawnTemplate);
            spawnerComponent.LevelObject = levelObject;

            levelObject.LegoInfo.Remove("spawntemplate");

            return instance;
        }

        public static async Task<GameObject> InstantiateSpawnerAsync(LuzSpawnerPath spawnerPath, Object parent)
        {
            if (spawnerPath.Waypoints.Length == default) return default;

            var wayPoint = (LuzSpawnerWaypoint) spawnerPath.Waypoints[default];

            var spawner = await GameObject.InstantiateAsync(
                parent,
                spawnerPath.PathName,
                wayPoint.Position,
                wayPoint.Rotation,
                -1,
                spawnerPath.SpawnerObjectId,
                Lot.Spawner
            );

            spawner.Settings.Add("respawn", spawnerPath.RespawnTime);

            var spawnerComponent = await spawner.AddComponentAsync<SpawnerComponent>();

            //spawnerComponent.SpawnsToMaintain = (int) spawnerPath.NumberToMaintain;
            spawnerComponent.RespawnTime = (int) spawnerPath.RespawnTime * 1000;
            spawnerComponent.Settings = spawner.Settings;
            spawnerComponent.SpawnTemplate = (int) spawnerPath.SpawnedLot;
            spawnerComponent.LevelObject = new LevelObjectTemplate
            {
                Scale = 1
            };

            return spawner;
        }

        public static async Task<GameObject> InstantiateLootAsync(Lot lot, Player owner, GameObject source, Vector3 spawn)
        {
            if (owner is null) return default;

            var drop = await GameObject.InstantiateAsync(
                owner.Zone,
                lot,
                spawn
            );

            drop.Layer = StandardLayer.Hidden;

            var finalPosition = new Vector3
            {
                X = spawn.X + ((float) Random.NextDouble() % 1f - 0.5f) * 20f,
                Y = spawn.Y,
                Z = spawn.Z + ((float) Random.NextDouble() % 1f - 0.5f) * 20f
            };

            owner.Message(new DropClientLootMessage
            {
                Associate = owner,
                Currency = 0,
                Lot = drop.Lot,
                Loot = drop,
                Owner = owner,
                Source = source,
                SpawnPosition = drop.Transform.Position + Vector3.UnitY,
                FinalPosition = finalPosition
            });

            return drop;
        }
        
        public static void InstantiateCurrency(int currency, Player owner, GameObject source, Vector3 spawn)
        {
            if (owner is null) return;
            
            var finalPosition = new Vector3
            {
                X = spawn.X + ((float) Random.NextDouble() % 1f - 0.5f) * 20f,
                Y = spawn.Y,
                Z = spawn.Z + ((float) Random.NextDouble() % 1f - 0.5f) * 20f
            };

            owner.Message(new DropClientLootMessage
            {
                Associate = owner,
                Currency = currency,
                Owner = owner,
                Source = source,
                SpawnPosition = spawn + Vector3.UnitY,
                FinalPosition = finalPosition
            });

            owner.EntitledCurrency += currency;
        }
    }
}