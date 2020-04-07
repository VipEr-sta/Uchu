using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Uchu.Core;
using Uchu.Core.Client;
using Uchu.Python;
using Uchu.World.Social;

namespace Uchu.World.Scripting.Managed
{
    public class PythonScript : Script
    {
        public string Source { get; }
        
        public Zone Zone { get; }
        
        public ManagedScript Script { get; set; }
        
        public Object Proxy { get; set; }
        
        public PythonScript(string source, Zone zone)
        {
            Source = source;
            
            Zone = zone;
        }

        public override async Task LoadAsync()
        {
            Proxy = Object.Instantiate(Zone);

            await Object.StartAsync(Proxy);
            
            Script = new ManagedScript(
                Source,
                Zone.ManagedScriptEngine
            );

            dynamic layers = new ExpandoObject();
            layers.None = (Mask) StandardLayer.None;
            layers.Default = (Mask) StandardLayer.Default;
            layers.Environment = (Mask) StandardLayer.Environment;
            layers.Npc = (Mask) StandardLayer.Npc;
            layers.Smashable = (Mask) StandardLayer.Smashable;
            layers.Player = (Mask) StandardLayer.Player;
            layers.Enemy = (Mask) StandardLayer.Enemy;
            layers.Spawner = (Mask) StandardLayer.Spawner;
            layers.Hidden = (Mask) StandardLayer.Hidden;
            layers.All = (Mask) StandardLayer.All;

            var variables = new Dictionary<string, dynamic>
            {
                ["Zone"] = Zone,
                ["Start"] = new Func<Object, Task>(Object.StartAsync),
                ["Destroy"] = new Func<Object, Task>(Object.DestroyAsync),
                ["Construct"] = new Action<GameObject>(GameObject.Construct),
                ["Serialize"] = new Action<GameObject>(GameObject.Serialize),
                ["Destruct"] = new Action<GameObject>(GameObject.Destruct),
                ["Create"] = new Func<int, Vector3, Quaternion, Task<GameObject>>
                    ((lot, position, rotation) => GameObject.InstantiateAsync(Zone, lot, position, rotation)),
                ["Broadcast"] = new Action<dynamic>(obj =>
                {
                    foreach (var player in Zone.Players)
                    {
                        player.SendChatMessage(obj, PlayerChatChannel.Normal);
                    }
                }),
                ["OnStart"] = new Func<GameObject, Func<Task>, Delegate>((gameObject, action) => Listen(gameObject.OnStart, action)),
                ["OnDestroy"] = new Func<GameObject, Func<Task>, Delegate>((gameObject, action) => Listen(gameObject.OnDestroyed, action)),
                ["OnInteract"] = new Func<GameObject, Func<Player, Task>, Delegate>((gameObject, action) => Listen(gameObject.OnInteract, action)),
                ["OnHealth"] = new Func<GameObject, Action<int, int, GameObject>, Delegate>((gameObject, action) =>
                {
                    if (!gameObject.TryGetComponent<Stats>(out var stats)) return null;

                    return Listen(stats.OnHealthChanged, (newHealth, delta) =>
                    {
                        action((int) newHealth, delta, stats.LatestDamageSource);
                        
                        return Task.CompletedTask;
                    });
                }),
                ["OnArmor"] = new Func<GameObject, Action<int, int, GameObject>, Delegate>((gameObject, action) =>
                {
                    if (!gameObject.TryGetComponent<Stats>(out var stats)) return null;

                    return Listen(stats.OnArmorChanged, (newArmor, delta) =>
                    {
                        action((int) newArmor, delta, stats.LatestDamageSource);
                        
                        return Task.CompletedTask;
                    });
                }),
                ["OnDeath"] = new Func<GameObject, Action<GameObject>, Delegate>((gameObject, action) =>
                {
                    if (!gameObject.TryGetComponent<Stats>(out var stats)) return null;
                    
                    return Listen(stats.OnDeath, () =>
                    {
                        action(stats.LatestDamageSource); 
                        
                        return Task.CompletedTask;
                    });
                }),
                ["OnChat"] = new Func<Action<Player, string>, Delegate>(action =>
                {
                    return Listen(Zone.OnChatMessage, (player, message) =>
                    {
                        action(player, message);
                        
                        return Task.CompletedTask;
                    });
                }),
                ["Release"] = new Action<Delegate>(ReleaseListener),
                ["Drop"] = new Func<int, Vector3, GameObject, Player, Task>(async (lot, position, source, player) =>
                {
                    var loot = await InstancingUtilities.InstantiateLootAsync(lot, player, source, position);

                    await Object.StartAsync(loot);
                }),
                ["Currency"] = new Action<int, Vector3, GameObject, Player>((count, position, source, player) =>
                {
                    InstancingUtilities.InstantiateCurrency(count, player, source, position);
                }),
                ["GetComponent"] = new Func<GameObject, string, Component>((gameObject, name) =>
                {
                    var type = Type.GetType($"Uchu.World.{name}");
                    
                    return type != default ? gameObject.GetComponent(type) : null;
                }),
                ["AddComponent"] = new Func<GameObject, string, Task<Component>>(async (gameObject, name) =>
                {
                    var type = Type.GetType($"Uchu.World.{name}");

                    if (type == default) return default;
                    
                    var result = await gameObject.AddComponentAsync(type);

                    return result;

                }),
                ["RemoveComponent"] = new Func<GameObject, string, Task>(async (gameObject, name) =>
                {
                    var type = Type.GetType($"Uchu.World.{name}");

                    if (type == null) return;
                    
                    await gameObject.RemoveComponentAsync(type);
                }),
                ["Vector3"] = new Func<float, float, float, Vector3>((x, y, z) => new Vector3(x, y, z)),
                ["Distance"] = new Func<Vector3, Vector3, float>(Vector3.Distance),
                ["Quaternion"] = new Func<float, float, float, float, Quaternion>((x, y, z, w) => new Quaternion(x, y, z, w)),
                ["Layer"] = layers,
                ["Chat"] = new Action<Player, string>((player, message) =>
                {
                    player.SendChatMessage(message, PlayerChatChannel.Normal);
                }),
                ["ClientContext"] = new Func<CdClientContext>(() => new CdClientContext()),
                ["UchuContext"] = new Func<UchuContext>(() => new UchuContext()),
                ["CentralNotice"] = new Action<Player, string>((player, text) => player.CentralNoticeGui(text)),
                ["StoryBox"] = new Action<Player, string>(async (player, text) => await player.StoryBoxGuiAsync(text))
            };

            Script.Run(variables.ToArray());

            var _ = TaskHelper.TryTask(() =>
            {
                Script.Execute("load", out var exception);

                if (exception != default)
                    Logger.Error(exception);
                
                return Task.CompletedTask;
            });
            
            Zone.Update(Proxy, () => { Task.Run(() => {
                    Script.Execute("tick", out var exception);
                    
                    if (exception != default)
                        Logger.Error(exception);
                });
            
                return Task.CompletedTask;
            }, 1);
        }

        public override async Task UnloadAsync()
        {
            await Object.DestroyAsync(Proxy);
            
            ClearListeners();

            var _ = TaskHelper.TryTask(() =>
            {
                Script.Execute("unload", out var exception);

                if (exception != default)
                    Logger.Error(exception);
                
                return Task.CompletedTask;
            });
        }
    }
}