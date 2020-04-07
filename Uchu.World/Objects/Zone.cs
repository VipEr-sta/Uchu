using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using InfectedRose.Luz;
using InfectedRose.Lvl;
using InfectedRose.Utilities;
using RakDotNet;
using RakDotNet.IO;
using Uchu.Core;
using Uchu.Python;
using Uchu.World.AI;
using Uchu.World.Client;
using Uchu.World.Scripting;
using Timer = System.Timers.Timer;

namespace Uchu.World
{
    public class Zone : Object
    {
        //
        // Consts
        //

        private const int TicksPerSecondLimit = 20;

        private SemaphoreSlim ObjectsLock { get; }
        
        private long PassedTickTime { get; set; }
        private bool Running { get; set; }
        private int Ticks { get; set; }

        public Zone(ZoneInfo zoneInfo, Server server, ushort instanceId, uint cloneId)
        {
            ObjectsLock = new SemaphoreSlim(1, 1);
            
            OnPlayerLoad = new AsyncEvent<Player>();
            OnObject = new AsyncEvent<Object>();
            OnTick = new AsyncEvent();
            OnChatMessage = new AsyncEvent<Player, string>();
            
            Zone = this;
            ZoneInfo = zoneInfo;
            Server = server;
            InstanceId = instanceId;
            CloneId = cloneId;

            ScriptManager = new ScriptManager(this);
            ManagedScriptEngine = new ManagedScriptEngine();
            UpdatedObjects = new List<UpdatedObject>();
            ManagedObjects = new List<Object>();
            SpawnedObjects = new List<GameObject>();

            Listen(OnDestroyed, () =>
            {
                Running = false;
                
                return Task.CompletedTask;
            });
        }

        //
        // Zone info
        //

        public uint CloneId { get; }

        public ushort InstanceId { get; }

        public ZoneInfo ZoneInfo { get; }

        public new Server Server { get; }

        public uint Checksum { get; private set; }

        public bool Loaded { get; private set; }

        public NavMeshManager NavMeshManager { get; private set; }

        //
        // Managed objects
        //

        private List<Object> ManagedObjects { get; }

        private List<GameObject> SpawnedObjects { get; }

        //
        // Macro properties
        //

        public Object[] Objects => ManagedObjects.ToArray();

        public GameObject[] GameObjects => Objects.OfType<GameObject>().ToArray();
        
        public Player[] Players => Objects.OfType<Player>().ToArray();

        public GameObject[] Spawned => SpawnedObjects.ToArray();

        public ZoneId ZoneId { get; private set; }

        public Vector3 SpawnPosition { get; private set; }

        public Quaternion SpawnRotation { get; private set; }

        //
        // Runtime
        //

        public float DeltaTime { get; private set; }
        
        public ScriptManager ScriptManager { get; }
        
        public ManagedScriptEngine ManagedScriptEngine { get; }

        private List<UpdatedObject> UpdatedObjects { get; }

        //
        // Events
        //

        public AsyncEvent<Player> OnPlayerLoad { get; }

        public AsyncEvent<Object> OnObject { get; }

        public AsyncEvent OnTick { get; }

        public AsyncEvent<Player, string> OnChatMessage { get; }

        #region Initializing

        public async Task<Task> InitializeAsync()
        {
            Checksum = ZoneInfo.LuzFile.GenerateChecksum(ZoneInfo.LvlFiles);

            ZoneId = (ZoneId) ZoneInfo.LuzFile.WorldId;

            SpawnPosition = ZoneInfo.LuzFile.SpawnPoint;

            SpawnRotation = ZoneInfo.LuzFile.SpawnRotation;

            Logger.Information($"Checksum: 0x{Checksum:X}");

            Logger.Information($"Collecting objects for {ZoneId}");

            await SetupNavigationAsync();

            await SpawnObjectsAsync();

            await RunManagedScripts();
            
            //
            // Clean up useless variables
            //
            
            ZoneInfo.LvlFiles.Clear();

            ZoneInfo.TerrainFile = default;
            
            Loaded = true;

            return ExecuteUpdateAsync();
        }

        private async Task RunManagedScripts()
        {
            //
            // Load zone scripts
            //

            await ScriptManager.LoadDefaultScriptsAsync();

            foreach (var scriptPack in ScriptManager.ScriptPacks)
            {
                try
                {
                    Logger.Information($"Running: {scriptPack.Name}");

                    await scriptPack.LoadAsync();
                }
                catch (Exception e)
                {
                    Logger.Error(e);
                }
            }
        }

        private async Task SetupNavigationAsync()
        {
            NavMeshManager = new NavMeshManager(this, Server.Configuration.GamePlayConfiguration.PathFinding);

            if (NavMeshManager.Enabled)
            {
                await NavMeshManager.GeneratePointsAsync();
            }
        }

        private async Task SpawnObjectsAsync()
        {
            var objects = new List<LevelObjectTemplate>();

            foreach (var lvlFile in ZoneInfo.LvlFiles.Where(lvlFile => lvlFile.LevelObjects?.Templates != default))
                objects.AddRange(lvlFile.LevelObjects.Templates);
            
            Logger.Information($"Loading {objects.Count} objects");
            
            foreach (var levelObject in objects)
            {
                try
                {
                    await SpawnLevelObjectAsync(levelObject);
                }
                catch (Exception e)
                {
                    Logger.Error(e);
                }
            }

            if (ZoneInfo.LuzFile.PathData != default)
            {
                foreach (var path in ZoneInfo.LuzFile.PathData.OfType<LuzSpawnerPath>())
                {
                    try
                    {
                        await SpawnPathAsync(path);
                    }
                    catch (Exception e)
                    {
                        Logger.Error(e);
                    }
                }
            }
            
            Logger.Information($"Loaded {Objects.Length} objects");
        }

        private async Task SpawnLevelObjectAsync(LevelObjectTemplate levelObject)
        {
            var obj = await GameObject.InstantiateAsync(levelObject, this);

            SpawnerComponent spawner = default;

            if (levelObject.LegoInfo.TryGetValue("loadSrvrOnly", out var serverOnly) && (bool) serverOnly ||
                levelObject.LegoInfo.TryGetValue("carver_only", out var carverOnly) && (bool) carverOnly ||
                levelObject.LegoInfo.TryGetValue("renderDisabled", out var disabled) && (bool) disabled)
                obj.Layer = StandardLayer.Hidden;
            else if (!obj.TryGetComponent(out spawner)) obj.Layer = StandardLayer.Hidden;

            await StartAsync(obj);

            //
            // Only spawns should get constructed on the client.
            //

            spawner?.SpawnClusterAsync();
        }

        private async Task SpawnPathAsync(LuzSpawnerPath spawnerPath)
        {
            var obj = await InstancingUtilities.InstantiateSpawnerAsync(spawnerPath, this);

            if (obj == null) return;

            obj.Layer = StandardLayer.Hidden;

            var spawner = obj.GetComponent<SpawnerComponent>();

            spawner.SpawnLocations = spawnerPath.Waypoints.Select(w => new SpawnLocation
            {
                Position = w.Position,
                Rotation = Quaternion.Identity
            }).ToList();

            await StartAsync(obj);

            await spawner.SpawnClusterAsync();
        }

        #endregion

        #region Messages

        public void SelectiveMessage(IGameMessage message, IEnumerable<Player> players)
        {
            foreach (var player in players) player.Message(message);
        }

        public void ExcludingMessage(IGameMessage message, Player excluded)
        {
            foreach (var player in Players.Where(p => p != excluded)) player.Message(message);
        }

        public void BroadcastMessage(IGameMessage message)
        {
            foreach (var player in Players) player.Message(message);
        }

        #endregion

        #region Object Finder

        public GameObject GetGameObject(long objectId)
        {
            return objectId == -1 ? null : GameObjects.First(o => o.Id == objectId);
        }

        public bool TryGetGameObject(long objectId, out GameObject result)
        {
            result = GameObjects.FirstOrDefault(o => o.Id == objectId);
            return result != default;
        }

        public T GetGameObject<T>(long objectId) where T : GameObject
        {
            return GameObjects.OfType<T>().First(o => o.Id == objectId);
        }

        public bool TryGetGameObject<T>(long objectId, out T result) where T : GameObject
        {
            result = GameObjects.FirstOrDefault(o => o.Id == objectId) as T;
            return result != null;
        }

        #endregion

        #region Object Mangement

        #region Register

        internal async Task RegisterPlayerAsync(Player player)
        {
            await OnPlayerLoad.InvokeAsync(player);

            foreach (var gameObject in GameObjects)
            {
                if (gameObject.GetType().GetCustomAttribute<UnconstructedAttribute>() != null) continue;

                SendConstruction(gameObject, new[] {player});
            }
        }

        internal async Task RegisterObjectAsync(Object obj)
        {
            await ObjectsLock.WaitAsync();

            try
            {
                if (ManagedObjects.Contains(obj)) return;

                await OnObject.InvokeAsync(obj);

                ManagedObjects.Add(obj);

                if (!(obj is GameObject gameObject)) return;

                if ((gameObject.Id.Flags & ObjectIdFlags.Spawned) != 0) SpawnedObjects.Add(gameObject);
            }
            finally
            {
                ObjectsLock.Release();
            }
        }

        internal async Task UnregisterObjectAsync(Object obj)
        {
            await ObjectsLock.WaitAsync();

            try
            {
                if (!ManagedObjects.Contains(obj)) return;

                ManagedObjects.Remove(obj);

                if (obj is GameObject gameObject)
                    if ((gameObject.Id.Flags & ObjectIdFlags.Spawned) != 0)
                        SpawnedObjects.Remove(gameObject);

                var updated = UpdatedObjects.FirstOrDefault(u => u.Associate == obj);

                if (updated == default) return;

                UpdatedObjects.Remove(updated);
            }
            finally
            {
                ObjectsLock.Release();
            }
        }

        #endregion

        #region Networking

        internal static void SendConstruction(GameObject gameObject, Player recipient)
        {
            SendConstruction(gameObject, new[] {recipient});
        }

        internal static void SendConstruction(GameObject gameObject, IEnumerable<Player> recipients)
        {
            foreach (var recipient in recipients)
            {
                if (!recipient.Perspective.View(gameObject)) continue;

                if (!recipient.Perspective.Reveal(gameObject, out var id)) continue;

                if (id == 0) return;

                using var stream = new MemoryStream();
                using var writer = new BitWriter(stream);

                writer.Write((byte) MessageIdentifier.ReplicaManagerConstruction);

                writer.WriteBit(true);
                writer.Write(id);

                gameObject.WriteConstruct(writer);

                recipient.Connection.Send(stream);
            }
        }

        internal static void SendSerialization(GameObject gameObject, IEnumerable<Player> recipients)
        {
            foreach (var recipient in recipients)
            {
                if (!recipient.Perspective.TryGetNetworkId(gameObject, out var id)) continue;
                
                if (id == 0) return;

                using var stream = new MemoryStream();
                using var writer = new BitWriter(stream);

                writer.Write((byte) MessageIdentifier.ReplicaManagerSerialize);

                writer.Write(id);

                gameObject.WriteSerialize(writer);

                recipient.Connection.Send(stream);
            }
        }

        internal static void SendDestruction(GameObject gameObject, Player player)
        {
            SendDestruction(gameObject, new[] {player});
        }

        internal static void SendDestruction(GameObject gameObject, IEnumerable<Player> recipients)
        {
            foreach (var recipient in recipients)
            {
                if (recipient.Perspective.View(gameObject)) continue;

                if (!recipient.Perspective.TryGetNetworkId(gameObject, out var id)) continue;

                using (var stream = new MemoryStream())
                {
                    using var writer = new BitWriter(stream);

                    writer.Write((byte) MessageIdentifier.ReplicaManagerDestruction);

                    writer.Write(id);

                    recipient.Connection.Send(stream);
                }

                recipient.Perspective.Drop(gameObject);
            }
        }

        #endregion

        #endregion

        #region Debug

        internal static void SaveCreation(GameObject gameObject, IEnumerable<Player> recipients, string path)
        {
            foreach (var recipient in recipients)
            {
                if (!recipient.Perspective.TryGetNetworkId(gameObject, out var id)) continue;

                using var stream = new MemoryStream();
                using var writer = new BitWriter(stream);

                writer.Write((byte) MessageIdentifier.ReplicaManagerConstruction);

                writer.WriteBit(true);
                writer.Write(id);

                gameObject.WriteConstruct(writer);

                recipient.Connection.Send(stream);

                var content = stream.ToArray();

                File.WriteAllBytes(Path.Combine(gameObject.Server.MasterPath, path), content);
            }
        }

        internal static void SaveSerialization(GameObject gameObject, IEnumerable<Player> recipients, string path)
        {
            foreach (var recipient in recipients)
            {
                if (!recipient.Perspective.TryGetNetworkId(gameObject, out var id)) continue;

                using var stream = new MemoryStream();
                using var writer = new BitWriter(stream);

                writer.Write((byte) MessageIdentifier.ReplicaManagerSerialize);

                writer.Write(id);

                gameObject.WriteSerialize(writer);

                var content = stream.ToArray();

                File.WriteAllBytes(Path.Combine(gameObject.Server.MasterPath, path), content);
            }
        }

        #endregion

        #region Runtime

        private Task ExecuteUpdateAsync()
        {
            var timer = new Timer
            {
                Interval = 1000,
                AutoReset = true
            };

            timer.Elapsed += (sender, args) =>
            {
                if (Ticks > 0)
                    Logger.Debug($"TPS: {Ticks}/{TicksPerSecondLimit} TPT: {PassedTickTime / Ticks} ms");
                PassedTickTime = 0;
                Ticks = 0;
            };

            timer.Start();
            
            Logger.Information("Running gameloop");

            return Task.Run(async () =>
            {
                Running = true;

                while (Running)
                {
                    if (Players.Length == 0)
                    {
                        await Task.Delay(1000);

                        continue;
                    }

                    if (Ticks >= TicksPerSecondLimit) continue;

                    var start = DateTimeOffset.Now.ToUnixTimeMilliseconds();

                    await Task.Delay(1000 / TicksPerSecondLimit);

                    foreach (var updatedObject in UpdatedObjects.ToArray())
                    {
                        if (updatedObject.Associate is GameObject gameObject)
                            if (Players.All(p => !p.Perspective.LoadedObjects.Contains(gameObject)))
                                continue;

                        if (updatedObject.Frequency != ++updatedObject.Ticks) continue;

                        updatedObject.Ticks = 0;

                        try
                        {
                            await updatedObject.Delegate();
                        }
                        catch (Exception e)
                        {
                            Logger.Error(e);
                        }
                    }

                    Ticks++;

                    var passedMs = DateTimeOffset.Now.ToUnixTimeMilliseconds() - start;

                    DeltaTime = passedMs / 1000f;

                    PassedTickTime += passedMs;
                }
            });
        }

        public void Update(Object associate, Func<Task> @delegate, int frequency)
        {
            UpdatedObjects.Add(new UpdatedObject
            {
                Associate = associate,
                Delegate = @delegate,
                Frequency = frequency
            });
        }

        private class UpdatedObject
        {
            public Object Associate { get; set; }

            public Func<Task> Delegate { get; set; }

            public int Frequency { get; set; }

            public int Ticks { get; set; }
        }

        #endregion
    }
}