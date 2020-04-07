using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using InfectedRose.Lvl;
using Uchu.Core;

namespace Uchu.World
{
    [ServerComponent(Id = ComponentId.SpawnerComponent)]
    public class SpawnerComponent : Component
    {
        private Random Random { get; }
        
        public List<GameObject> ActiveSpawns { get; }

        public LevelObjectTemplate LevelObject { get; set; }
        
        public List<SpawnLocation> SpawnLocations { get; set; }

        public int SpawnsToMaintain { get; set; } = 1;

        public int RespawnTime { get; set; } = 10000;

        public Lot SpawnTemplate { get; set; }

        public uint SpawnNodeId { get; set; }

        public LegoDataDictionary Settings { get; set; }

        protected SpawnerComponent()
        {
            Random = new Random();
            
            SpawnLocations = new List<SpawnLocation>();

            ActiveSpawns = new List<GameObject>();
            
            Listen(OnStart, () =>
            {
                if (SpawnLocations.Count == 0)
                {
                    SpawnLocations.Add(new SpawnLocation
                    {
                        Position = Transform.Position,
                        Rotation = Transform.Rotation
                    });
                }

                GameObject.Layer = StandardLayer.Spawner;
                
                return Task.CompletedTask;
            });
        }

        private async Task<GameObject> GenerateSpawnObjectAsync()
        {
            var location = FindLocation();

            location.InUse = true;

            var o = new LevelObjectTemplate
            {
                Lot = SpawnTemplate,
                Position = location.Position,
                Rotation = location.Rotation,
                Scale = LevelObject.Scale,
                LegoInfo = Settings,
                ObjectId = ObjectId.FromFlags(ObjectIdFlags.Spawned | ObjectIdFlags.Client)
            };
            
            var obj = await GameObject.InstantiateAsync(o, Zone, this);

            if (obj.TryGetComponent<DestructibleComponent>(out var destructibleComponent))
            {
                Listen(destructibleComponent.OnSmashed, (smasher, owner) =>
                {
                    location.InUse = false;
                    
                    return Task.CompletedTask;
                });
            }

            return obj;
        }

        private SpawnLocation FindLocation()
        {
            var locations = SpawnLocations.Where(s => !s.InUse).ToArray();

            if (locations.Length == 0)
            {
                return new SpawnLocation
                {
                    Position = Transform.Position,
                    Rotation = Transform.Rotation
                };
            }
            
            var location = locations[Random.Next(locations.Length)];

            return location;
        }

        public async Task<GameObject> SpawnAsync()
        {
            var obj = await GenerateSpawnObjectAsync();

            await StartAsync(obj);

            GameObject.Construct(obj);

            ActiveSpawns.Add(obj);

            Listen(obj.OnDestroyed, () =>
            {
                ActiveSpawns.Remove(obj);
                
                return Task.CompletedTask;
            });

            if (obj.TryGetComponent<DestructibleComponent>(out var destructibleComponent))
            {
                Listen(destructibleComponent.OnSmashed, async (smasher, lootOwner) =>
                {
                    await Task.Delay(1000);

                    var location = FindLocation();

                    obj.Transform.Position = location.Position;
                    obj.Transform.Rotation = location.Rotation;
                });
            }

            return obj;
        }

        public async Task SpawnClusterAsync()
        {
            for (var i = 0; i < SpawnsToMaintain; i++)
            {
                await SpawnAsync();
            }
        }
    }
}