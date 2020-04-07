using System.Linq;
using System.Threading.Tasks;
using Uchu.World;
using Uchu.World.Scripting.Native;

namespace Uchu.StandardScripts.AvantGardens
{
    public class TurretMech : NativeScript
    {
        public override Task LoadAsync()
        {
            foreach (var gameObject in Zone.GameObjects.Where(g => g.Lot == 6253))
            {
                if (!gameObject.TryGetComponent<DestructibleComponent>(out var destructibleComponent)) continue;

                Listen(destructibleComponent.OnSmashed, async (smasher, lootOwner) =>
                {
                    var quickBuild = await GameObject.InstantiateAsync(
                        Zone,
                        6254,
                        gameObject.Transform.Position,
                        gameObject.Transform.Rotation
                    );

                    await StartAsync(quickBuild);
                    Construct(quickBuild);

                    var _ = Task.Run(async () =>
                    {
                        await Task.Delay(20000);
                        
                        await quickBuild.GetComponent<DestructibleComponent>().SmashAsync(quickBuild, lootOwner);

                        await DestroyAsync(quickBuild);
                    });
                });
            }
            
            return Task.CompletedTask;
        }
    }
}