using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using InfectedRose.Lvl;
using Microsoft.EntityFrameworkCore;
using RakDotNet.IO;
using Uchu.Core;
using Uchu.Core.Client;

namespace Uchu.World
{
    public class GameObject : Object
    {
        private List<Component> Components { get; }
        
        private Mask _layer = new Mask(StandardLayer.Default);

        private ObjectWorldState _worldState;
        
        private SemaphoreSlim ComponentLock { get; }

        protected string ObjectName { get; set; }
        
        public ObjectId Id { get; private set; }

        public Lot Lot { get; private set; }
        
        /// <summary>
        ///     Also known as ExtraInfo
        /// </summary>
        public LegoDataDictionary Settings { get; protected set; }

        public string ClientName { get; private set; }

        public SpawnerComponent SpawnerObject { get; private set; }

        protected GameObject()
        {
            ComponentLock = new SemaphoreSlim(1, 1);
            Components = new List<Component>();
            Settings = new LegoDataDictionary();

            OnInteract = new AsyncEvent<Player>();
            OnEmoteReceived = new AsyncEvent<int, Player>();

            Listen(OnStart, async () =>
            {
                await ComponentLock.WaitAsync();

                var components = Components.ToArray();
                
                ComponentLock.Release();
                
                foreach (var component in components)
                {
                    await StartAsync(component);
                }
            });

            Listen(OnDestroyed, async () =>
            {
                OnInteract.Clear();
                
                OnEmoteReceived.Clear();
                
                await Zone.UnregisterObjectAsync(this);

                await ComponentLock.WaitAsync();

                var components = Components.ToArray();
                
                ComponentLock.Release();

                foreach (var component in components)
                {
                    await DestroyAsync(component);
                }

                Destruct(this);
            });
        }
        
        public Mask Layer
        {
            get => _layer;
            set
            {
                _layer = value;

                foreach (var player in Zone.Players)
                {
                    player.UpdateView(this);
                }
            }
        }

        public virtual string Name
        {
            get => ObjectName;
            set
            {
                ObjectName = value;

                Reload();
            }
        }

        public ObjectWorldState WorldState
        {
            get => _worldState;
            set
            {
                _worldState = value;
                
                Zone.BroadcastMessage(new ChangeObjectWorldStateMessage
                {
                    Associate = this,
                    State = value
                });
            }
        }

        public int GameMasterLevel
        {
            get
            {
                if (Settings == default) return 0;
                
                if (!Settings.TryGetValue("gmlevel", out var level)) return default;

                return (int) level;
            }
            set
            {
                Settings["gmlevel"] = value;

                Reload();
            }
        }

        #region Events

        public AsyncEvent<Player> OnInteract { get; }

        public AsyncEvent<int, Player> OnEmoteReceived { get; }

        #endregion
        
        #region Macro

        public Transform Transform => GetComponent<Transform>();

        public bool Alive => Zone?.TryGetGameObject(Id, out _) ?? false;

        public IEnumerable<ReplicaComponent> ReplicaComponents => Components.OfType<ReplicaComponent>();

        public Player[] Viewers => Zone.Players.Where(p => p.Perspective.TryGetNetworkId(this, out _)).ToArray();

        #endregion
        
        #region Operators

        public static implicit operator long(GameObject gameObject)
        {
            if (gameObject == null) return -1;
            return gameObject.Id;
        }

        #endregion

        #region Utilities

        public override string ToString()
        {
            return $"[{Id}] \"{(string.IsNullOrWhiteSpace(ObjectName) ? ClientName : Name)}\"";
        }

        #endregion

        #region Component Management

        public async Task<Component> AddComponentAsync(Type type)
        {
            if (TryGetComponent(type, out var addedComponent)) return addedComponent;
            
            if (Instantiate(type, Zone) is Component component)
            {
                component.GameObject = this;

                await ComponentLock.WaitAsync();
                
                Components.Add(component);

                ComponentLock.Release();

                var requiredComponents = type.GetCustomAttributes<RequireComponentAttribute>().ToArray();

                foreach (var attribute in requiredComponents.Where(r => r.Priority))
                {
                    await AddComponentAsync(attribute.Type);
                }

                foreach (var attribute in requiredComponents.Where(r => !r.Priority))
                {
                    await AddComponentAsync(attribute.Type);
                }

                await StartAsync(component);

                return component;
            }

            Logger.Error($"{type.FullName} does not inherit form Components but is being Created as one.");
            return null;
        }

        public async Task<T> AddComponentAsync<T>() where T : Component
        {
            return await AddComponentAsync(typeof(T)) as T;
        }

        public Component GetComponent(Type type)
        {
            ComponentLock.Wait();
            
            var result = Components.FirstOrDefault(c => c.GetType() == type);

            ComponentLock.Release();

            return result;
        }

        public T GetComponent<T>() where T : Component
        {
            ComponentLock.Wait();

            var result = Components.FirstOrDefault(c => c is T) as T;
            
            ComponentLock.Release();

            return result;
        }

        public Component[] GetAllComponents()
        {
            ComponentLock.Wait();

            var result = Components.ToArray();
            
            ComponentLock.Release();

            return result;
        }

        public bool TryGetComponent(Type type, out Component result)
        {
            result = GetComponent(type);
            return result != default;
        }

        public bool TryGetComponent<T>(out T result) where T : Component
        {
            result = GetComponent<T>();
            return result != default;
        }

        public async Task RemoveComponentAsync(Type type, bool destroy = true)
        {
            var comp = GetComponent(type);

            await ComponentLock.WaitAsync();
            
            Components.Remove(comp);

            ComponentLock.Release();

            if (destroy)
            {
                await DestroyAsync(comp);
            }
        }

        public async Task RemoveComponentAsync<T>(bool destroy = true) where T : Component
        {
            await RemoveComponentAsync(typeof(T), destroy);
        }

        public async Task RemoveComponentAsync(Component component, bool destroy = true)
        {
            await ComponentLock.WaitAsync();

            var contains = Components.Contains(component);

            ComponentLock.Release();
            
            if (contains)
            {
                await RemoveComponentAsync(component.GetType(), destroy);
            }
        }

        #endregion

        #region Networking

        public static void Construct(GameObject gameObject)
        {
            Zone.SendConstruction(gameObject, gameObject.Zone.Players);
        }

        public static void Serialize(GameObject gameObject)
        {
            Zone.SendSerialization(gameObject, gameObject.Zone.Players);
        }

        public static void Destruct(GameObject gameObject)
        {
            Zone.SendDestruction(gameObject, gameObject.Zone.Players);
        }

        /// <summary>
        ///     Causes this GameObject to be deconstructed and than reconstructed on all Viewers.
        /// </summary>
        public void Reload()
        {
            var viewers = Viewers;
            
            Zone.SendDestruction(this, viewers);

            Zone.SendConstruction(this, viewers);
        }

        #endregion

        #region Instaniate

        #region From Raw

        public static async Task<GameObject> InstantiateAsync(Type type, Object parent, string name = "", Vector3 position = default,
            Quaternion rotation = default, float scale = 1, ObjectId objectId = default, Lot lot = default,
            SpawnerComponent spawner = default)
        {
            if (type.IsSubclassOf(typeof(GameObject)) || type == typeof(GameObject))
            {
                var instance = (GameObject) Instantiate(type, parent.Zone);
                
                instance.Id = objectId == 0L ? ObjectId.Standalone : objectId;

                instance.Lot = lot;

                instance.Name = name;

                await using (var cdClient = new CdClientContext())
                {
                    var obj = await cdClient.ObjectsTable.FirstOrDefaultAsync(
                        o => o.Id == lot
                    );
                    
                    instance.ClientName = obj?.Name;
                }

                instance.SpawnerObject = spawner;

                var transform = await instance.AddComponentAsync<Transform>();
                
                transform.Position = position;
                transform.Rotation = rotation;
                transform.Scale = scale;

                switch (parent)
                {
                    case Zone _:
                        transform.Parent = null;
                        break;
                    case GameObject parentObject:
                        transform.Parent = parentObject.Transform;
                        break;
                    case Transform parentTransform:
                        transform.Parent = parentTransform;
                        break;
                }

                return instance;
            }

            Logger.Error($"{type.FullName} does not inherit from GameObject but is being Instantiated as one.");
            return null;
        }

        public static async Task<T> InstantiateAsync<T>(Object parent, string name = "", Vector3 position = default,
            Quaternion rotation = default, float scale = 1, ObjectId objectId = default, Lot lot = default,
            SpawnerComponent spawner = default)
            where T : GameObject
        {
            return await InstantiateAsync(typeof(T), parent, name, position, rotation, scale, objectId, lot, spawner) as T;
        }
        
        public static async Task<GameObject> InstantiateAsync(Object parent, string name = "", Vector3 position = default,
            Quaternion rotation = default, float scale = 1, ObjectId objectId = default, Lot lot = default,
            SpawnerComponent spawner = default)
        {
            return await InstantiateAsync(typeof(GameObject), parent, name, position, rotation, scale, objectId, lot, spawner);
        }
        
        #endregion

        #region From Template

        public static async Task<GameObject> InstantiateAsync(Type type, Object parent, Lot lot, Vector3 position = default,
            Quaternion rotation = default)
        {
            return await InstantiateAsync(type, new LevelObjectTemplate
            {
                Lot = lot,
                Position = position,
                Rotation = rotation,
                Scale = 1,
                LegoInfo = new LegoDataDictionary()
            }, parent);
        }

        public static async Task<T> InstantiateAsync<T>(Object parent, Lot lot, Vector3 position = default,
            Quaternion rotation = default) where T : GameObject
        {
            return await InstantiateAsync(typeof(T), parent, lot, position, rotation) as T;
        }

        public static async Task<GameObject> InstantiateAsync(Object parent, Lot lot, Vector3 position = default,
            Quaternion rotation = default)
        {
            return await InstantiateAsync(typeof(GameObject), parent, lot, position, rotation);
        }

        #endregion

        #region From LevelObject

        public static async Task<GameObject> InstantiateAsync(Type type, LevelObjectTemplate levelObject, Object parent,
            SpawnerComponent spawner = default)
        {
            // ReSharper disable PossibleInvalidOperationException

            //
            // Check if spawner
            //

            if (levelObject.LegoInfo.TryGetValue("spawntemplate", out _))
                return await InstancingUtilities.InstantiateSpawnerAsync(levelObject, parent);

            await using var ctx = new CdClientContext();
            
            var name = levelObject.LegoInfo.TryGetValue("npcName", out var npcName) ? (string) npcName : "";

            //
            // Create GameObject
            //

            var id = levelObject.ObjectId == 0 ? (long) ObjectId.FromFlags(ObjectIdFlags.Spawned | ObjectIdFlags.Client) : (long) levelObject.ObjectId;

            var instance = await InstantiateAsync(
                type,
                parent,
                name,
                levelObject.Position,
                levelObject.Rotation,
                levelObject.Scale,
                id,
                levelObject.Lot
            );

            instance.SpawnerObject = spawner;
            instance.Settings = levelObject.LegoInfo;

            //
            // Collect all the components on this object
            //

            var registryComponents = ctx.ComponentsRegistryTable.Where(
                r => r.Id == levelObject.Lot
            ).ToArray();

            //
            // Select all the none networked components on this object
            //

            var componentEntries = registryComponents.Where(o =>
                o.Componenttype != null && !ReplicaComponent.ComponentOrder.Contains(o.Componenttype.Value)
            ).ToArray();

            foreach (var component in componentEntries)
            {
                //
                // Add components from the entries
                //
                
                var componentType = ReplicaComponent.GetReplica((ComponentId) (int) component.Componenttype);

                if (componentType != default)
                    await instance.AddComponentAsync(componentType);
            }

            //
            // Select all the networked components on this object
            //

            registryComponents = registryComponents.Where(
                c => ReplicaComponent.ComponentOrder.Contains(c.Componenttype.Value)
            ).ToArray();

            // Sort components

            Array.Sort(registryComponents, (c1, c2) =>
                ReplicaComponent.ComponentOrder.IndexOf((int) c1.Componenttype)
                    .CompareTo(ReplicaComponent.ComponentOrder.IndexOf((int) c2.Componenttype))
            );

            foreach (var component in registryComponents)
            {
                var componentType = ReplicaComponent.GetReplica((ComponentId) component.Componenttype);

                if (componentType == null) Logger.Warning($"No component of ID {(ComponentId) component.Componenttype}");
                else await instance.AddComponentAsync(componentType);
            }

            //
            // Check if this object is a trigger
            //

            if (levelObject.LegoInfo.ContainsKey("trigger_id"))
            {
                await instance.AddComponentAsync<TriggerComponent>();
            }

            return instance;
        }

        public static async Task<T> InstantiateAsync<T>(LevelObjectTemplate levelObject, Object parent, SpawnerComponent spawner = default)
            where T : GameObject
        {
            return await InstantiateAsync(typeof(T), levelObject, parent, spawner) as T;
        }

        public static async Task<GameObject> InstantiateAsync(LevelObjectTemplate levelObject, Object parent, SpawnerComponent spawner = default)
        {
            return await InstantiateAsync(typeof(GameObject), levelObject, parent, spawner);
        }

        #endregion

        #endregion

        #region Replica

        internal void WriteConstruct(BitWriter writer)
        {
            writer.Write(Id);

            writer.Write(Lot);

            writer.Write((byte) ObjectName.Length);
            writer.WriteString(ObjectName, ObjectName.Length, true);

            writer.Write<uint>(0); // TODO: Add creation time?

            writer.WriteBit(false); // TODO: figure this out

            var trigger = GetComponent<TriggerComponent>();

            var hasTriggerId = trigger?.Trigger != null;

            writer.WriteBit(hasTriggerId);

            var hasSpawner = SpawnerObject != null;

            writer.WriteBit(hasSpawner);

            if (hasSpawner)
                writer.Write(SpawnerObject.GameObject.Id);

            var hasSpawnerNode = SpawnerObject != null && SpawnerObject.SpawnTemplate != 0;

            writer.WriteBit(hasSpawnerNode);

            if (hasSpawnerNode)
                writer.Write(SpawnerObject.SpawnTemplate);

            var hasScale = !Transform.Scale.Equals(-1);

            writer.WriteBit(hasScale);

            if (hasScale)
                writer.Write(Transform.Scale);

            if (writer.Flag(GameMasterLevel != default))
            {
                writer.Write((byte) GameMasterLevel);
            }

            if (writer.Flag(WorldState != ObjectWorldState.World))
            {
                writer.Write((byte) WorldState);
            }

            WriteHierarchy(writer);

            //
            // Construct replica components.
            //

            foreach (var replicaComponent in ReplicaComponents)
                replicaComponent.Construct(writer);
        }

        internal void WriteSerialize(BitWriter writer)
        {
            WriteHierarchy(writer);

            //
            // Serialize replica components.
            //

            foreach (var replicaComponent in ReplicaComponents)
                replicaComponent.Serialize(writer);
        }

        private void WriteHierarchy(BitWriter writer)
        {
            writer.WriteBit(true);

            var hasParent = Transform.Parent != null;

            writer.WriteBit(hasParent);

            if (hasParent)
            {
                writer.Write(Transform.Parent.GameObject);
                writer.WriteBit(false);
            }

            var hasChildren = Transform.Children.Length != default;

            writer.WriteBit(hasChildren);

            if (!hasChildren) return;
            writer.Write((ushort) Transform.Children.Length);

            foreach (var child in Transform.Children) writer.Write(child.GameObject);
        }

        #endregion
    }
}