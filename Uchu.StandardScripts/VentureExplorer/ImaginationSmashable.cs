using System;
using System.Threading.Tasks;
using Uchu.Core;
using Uchu.World;
using Uchu.World.Scripting.Native;

namespace Uchu.StandardScripts.VentureExplorer
{
    /// <summary>
    ///     LUA Reference: l_ag_imag_smashable.lua
    /// </summary>
    [ZoneSpecific(1000)]
    public class ImaginationSmashable : NativeScript
    {
        private const string ScriptName = "l_ag_imag_smashable.lua";
        
        private Random _random;
        
        public override Task LoadAsync()
        {
            _random = new Random();
            
            foreach (var gameObject in HasLuaScript(ScriptName))
            {
                if (!gameObject.TryGetComponent<DestructibleComponent>(out var destructibleComponent)) continue;
                
                Listen(destructibleComponent.OnSmashed, async (killer, owner) =>
                {
                    if (owner.GetComponent<Stats>().MaxImagination == default) return;

                    //
                    // Spawn imagination drops
                    //
                    
                    for (var i = 0; i < _random.Next(1, 3); i++)
                    {
                        var loot = await InstancingUtilities.InstantiateLootAsync(
                            Lot.Imagination,
                            owner,
                            gameObject,
                            gameObject.Transform.Position
                        );

                        await StartAsync(loot);
                    }

                    var random = _random.Next(0, 26);
                    
                    if (random != 1) return;
                    
                    //
                    // Spawn crate chicken
                    //
                    
                    var chicken = await GameObject.InstantiateAsync(Zone, 8114, gameObject.Transform.Position);

                    await StartAsync(chicken);

                    Construct(chicken);

                    var _ = Task.Run(async () =>
                    {
                        await Task.Delay(4000);

                        await DestroyAsync(chicken);
                    });
                });
            }
            
            return Task.CompletedTask;
        }
    }
}