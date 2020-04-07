using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using InfectedRose.Lvl;
using Microsoft.EntityFrameworkCore;
using Uchu.Core;
using Uchu.Core.Client;

namespace Uchu.World
{
    public class InventoryManagerComponent : Component
    {
        private readonly Dictionary<InventoryType, Inventory> _inventories;

        public AsyncEvent<Lot, uint> OnLotAdded { get; }

        public AsyncEvent<Lot, uint> OnLotRemoved { get; }
        
        private SemaphoreSlim CalculationLock { get; }

        protected InventoryManagerComponent()
        {
            _inventories = new Dictionary<InventoryType, Inventory>();;
            
            OnLotAdded = new AsyncEvent<Lot, uint>();
            
            OnLotRemoved = new AsyncEvent<Lot, uint>();

            CalculationLock = new SemaphoreSlim(1, 1);
            
            Listen(OnStart, () =>
            {
                foreach (var value in Enum.GetValues(typeof(InventoryType)))
                {
                    var id = (InventoryType) value;

                    Logger.Information($"[{id}]");

                    _inventories.Add(id, new Inventory(id, this));
                }
                
                return Task.CompletedTask;
            });

            Listen(OnDestroyed,  async () =>
            {
                OnLotAdded.Clear();
                
                OnLotRemoved.Clear();

                foreach (var item in _inventories.Values.SelectMany(inventory => inventory.Items))
                {
                    await DestroyAsync(item);
                }
            });
        }

        public Inventory this[InventoryType inventoryType] => _inventories[inventoryType];

        public Inventory[] Inventories => _inventories.Values.ToArray();

        public Item[] Items => _inventories.Values.SelectMany(i => i.Items).ToArray();

        #region Find Item

        public Item FindItem(long id)
        {
            using var ctx = new UchuContext();
            var item = ctx.InventoryItems.FirstOrDefault(
                i => i.Id == id && i.CharacterId == GameObject.Id
            );

            if (item == default)
            {
                Logger.Error($"{id} is not an item on {GameObject}");
                return null;
            }

            var managedItem = _inventories[(InventoryType) item.InventoryType][id];

            if (managedItem == null) Logger.Error($"{item.Id} is not managed on {GameObject}");

            return managedItem;
        }
        
        public Item FindItem(Lot lot)
        {
            return _inventories.Values.Select(
                inventory => inventory.Items.FirstOrDefault(i => i.Lot == lot)
            ).FirstOrDefault(item => item != default);
        }

        public Item FindItem(Lot lot, InventoryType inventoryType)
        {
            return _inventories[inventoryType].Items.FirstOrDefault(i => i.Lot == lot);
        }
        
        public bool TryFindItem(Lot lot, out Item item)
        {
            item = FindItem(lot);

            return item != default;
        }

        public bool TryFindItem(Lot lot, InventoryType inventoryType, out Item item)
        {
            item = FindItem(lot, inventoryType);

            return item != default;
        }
        
        public Item[] FindItems(Lot lot)
        {
            return _inventories.Values.SelectMany(
                inventory => inventory.Items.Where(i => i.Lot == lot)
            ).ToArray();
        }

        public Item[] FindItems(Lot lot, InventoryType inventoryType)
        {
            return _inventories[inventoryType].Items.Where(i => i.Lot == lot).ToArray();
        }

        #endregion
        
        public async Task AddItemAsync(Lot lot, uint count, LegoDataDictionary extraInfo = default)
        {
            await using var cdClient = new CdClientContext();
            
            var componentId = await cdClient.ComponentsRegistryTable.FirstOrDefaultAsync(
                r => r.Id == lot && r.Componenttype == (int) ComponentId.ItemComponent
            );

            if (componentId == default)
            {
                Logger.Error($"{lot} does not have a Item component");
                return;
            }

            var component = await cdClient.ItemComponentTable.FirstOrDefaultAsync(
                i => i.Id == componentId.Componentid
            );

            if (component == default)
            {
                Logger.Error(
                    $"{lot} has a corrupted component registry. There is no Item component of Id: {componentId.Componentid}"
                );
                return;
            }

            Debug.Assert(component.ItemType != null, "component.ItemType != null");

            await AddItemAsync(lot, count, ((ItemType) component.ItemType).GetInventoryType(), extraInfo);
        }

        public async Task AddItemAsync(int lot, uint count, InventoryType inventoryType, LegoDataDictionary extraInfo = default)
        {
            var itemCount = count;
            
            await OnLotAdded.InvokeAsync(lot, itemCount);

            if (!_inventories.TryGetValue(inventoryType, out var inventory))
            {
                inventory = new Inventory(inventoryType, this);

                _inventories[inventoryType] = inventory;
            }

            // The math here cannot be executed in parallel
            
            await using var cdClient = new CdClientContext();
            
            var componentId = cdClient.ComponentsRegistryTable.FirstOrDefault(
                r => r.Id == lot && r.Componenttype == (int) ComponentId.ItemComponent
            );

            if (componentId == default)
            {
                Logger.Error($"{lot} does not have a Item component");
                return;
            }

            var component = cdClient.ItemComponentTable.FirstOrDefault(
                i => i.Id == componentId.Componentid
            );

            if (component == default)
            {
                Logger.Error(
                    $"{lot} has a corrupted component registry. There is no Item component of Id: {componentId.Componentid}"
                );
                return;
            }

            var stackSize = component.StackSize ?? 1;
            
            // Bricks and alike does not have a stack limit.
            if (stackSize == default) stackSize = int.MaxValue;
            
            //
            // Update quest tasks
            //

            var questInventory = GameObject.GetComponent<MissionInventoryComponent>();

            for (var i = 0; i < count; i++)
            {
                await questInventory.ObtainItemAsync(lot);
            }
            
            //
            // Fill stacks
            //
            
            await CalculationLock.WaitAsync();

            try
            {
                foreach (var item in inventory.Items.Where(i => i.Lot == lot))
                {
                    if (item.Settings.Count != default) continue;

                    var size = await item.GetCountAsync();
                    
                    if (size == stackSize) continue;

                    var toAdd = (uint) Min(stackSize, (int) count, (int) (stackSize - size));

                    await item.SetCountAsync(size + toAdd);

                    count -= toAdd;

                    if (count <= 0) return;
                }

                //
                // Create new stacks
                //

                var toCreate = count;

                while (toCreate != default)
                {
                    var toAdd = (uint) Min(stackSize, (int) toCreate);

                    var item = await Item.InstantiateAsync(lot, inventory, toAdd, extraInfo);

                    await StartAsync(item);

                    toCreate -= toAdd;
                }
            }
            finally
            {
                CalculationLock.Release();
            }
        }

        public async Task RemoveItemAsync(Lot lot, uint count, bool silent = false)
        {
            await using var cdClient = new CdClientContext();
            
            var componentId = await cdClient.ComponentsRegistryTable.FirstOrDefaultAsync(
                r => r.Id == lot && r.Componenttype == (int) ComponentId.ItemComponent
            );

            if (componentId == default)
            {
                Logger.Error($"{lot} does not have a Item component");
                return;
            }

            var component = await cdClient.ItemComponentTable.FirstOrDefaultAsync(
                i => i.Id == componentId.Componentid
            );

            if (component == default)
            {
                Logger.Error(
                    $"{lot} has a corrupted component registry. There is no Item component of Id: {componentId.Componentid}"
                );
                
                return;
            }

            Debug.Assert(component.ItemType != null, "component.ItemType != null");

            await RemoveItemAsync(lot, count, ((ItemType) component.ItemType).GetInventoryType(), silent);
        }

        public async Task RemoveItemAsync(int lot, uint count, InventoryType inventoryType, bool silent = false)
        {
            await OnLotRemoved.InvokeAsync(lot, count);

            await using var cdClient = new CdClientContext();
            
            var componentId = cdClient.ComponentsRegistryTable.FirstOrDefault(
                r => r.Id == lot && r.Componenttype == (int) ComponentId.ItemComponent
            );

            if (componentId == default)
            {
                Logger.Error($"{lot} does not have a Item component");
                return;
            }

            var component = cdClient.ItemComponentTable.FirstOrDefault(
                i => i.Id == componentId.Componentid
            );

            if (component == default)
            {
                Logger.Error(
                    $"{lot} has a corrupted component registry. There is no Item component of Id: {componentId.Componentid}"
                );
                return;
            }

            var items = _inventories[inventoryType].Items.Where(i => i.Lot == lot).ToList();

            var sizes = new Dictionary<Item, uint>();

            foreach (var item in items)
            {
                sizes[item] = await item.GetCountAsync();
            }
            
            //
            // Sort to make sure we remove from the stacks with the lowest count first.
            //

            items.Sort((i1, i2) => (int) (sizes[i1] - sizes[i2]));

            foreach (var item in items)
            {
                var size = sizes[item];
                
                var toRemove = (uint) Min((int) count, (int) size);

                if (!silent)
                {
                    await item.SetCountAsync(size - toRemove);
                }
                else
                {
                    var storedCount = size - toRemove;
                    
                    var _ = Task.Run(async () =>
                    {
                        await item.UpdateCountSilentAsync(storedCount);
                    });
                }

                count -= toRemove;

                if (count != default) continue;

                return;
            }

            Logger.Error(
                $"Trying to remove {lot} x {count} when {GameObject} does not have that many of {lot} in their {inventoryType} inventory"
            );
        }

        public async Task MoveItemsBetweenInventoriesAsync(Item item, Lot lot, uint count, InventoryType source, InventoryType destination, bool silent = false)
        {
            if (item?.Settings != null)
            {
                var size = await item.GetCountAsync();

                if (count != 1 || size != 1)
                {
                    Logger.Error($"Invalid special item {item}");
                    return;
                }
                
                await DestroyAsync(item);

                await AddItemAsync(item.Lot, count, destination, item.Settings);
                
                return;
            }
            
            await RemoveItemAsync(item?.Lot ?? lot, count, source, silent);

            await AddItemAsync(item?.Lot ?? lot, count, destination);
        }
        
        private static int Min(params int[] nums)
        {
            return nums.Min();
        }
    }
}