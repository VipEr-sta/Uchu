using System.Threading.Tasks;
using Uchu.Core;

namespace Uchu.World
{
    [ServerComponent(Id = ComponentId.PackageComponent)]
    public class ItemPackageComponent : Component
    {
        private ItemPackageComponent()
        {
            Listen(OnStart, async () =>
            {
                if (!(GameObject is Item item))
                {
                    Logger.Error("Component mismatch");

                    await DestroyAsync(this);
                    
                    return;
                }

                Listen(item.OnConsumed, ConsumeAsync);
            });
        }

        private async Task ConsumeAsync()
        {
            if (!(GameObject is Item item)) return;
            
            var container = await GameObject.AddComponentAsync<LootContainerComponent>();

            await container.CollectDetailsAsync();

            var manager = item.Inventory.ManagerComponent;
            
            foreach (var lot in container.GenerateLootYields())
            {
                await manager.AddItemAsync(lot, 1);
            }
        }
    }
}