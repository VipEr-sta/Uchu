using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using InfectedRose.Lvl;
using Microsoft.EntityFrameworkCore;
using Uchu.Core;
using Uchu.Core.Client;

namespace Uchu.World
{
    [Unconstructed]
    public class Item : GameObject
    {
        protected Item()
        {
            OnConsumed = new AsyncEvent();
            
            Listen(OnStart, async () =>
            {
                await using var ctx = new CdClientContext();

                var id = await Lot.GetComponentIdAsync(ComponentId.ItemComponent);

                ItemComponent = await ctx.ItemComponentTable.FirstOrDefaultAsync(
                    c => c.Id == id
                );
            });
            
            Listen(OnDestroyed, () =>
            {
                Inventory.UnManageItem(this);
                
                return Task.CompletedTask;
            });
        }

        public AsyncEvent OnConsumed { get; }

        public ItemComponent ItemComponent { get; private set; }

        public Inventory Inventory { get; private set; }

        public Player Player { get; private set; }

        public ItemType ItemType => (ItemType) (ItemComponent.ItemType ?? (int) ItemType.Invalid);

        public async Task<uint> GetCountAsync()
        {
            await using var ctx = new UchuContext();

            var info = await ctx.InventoryItems.FirstAsync(
                i => i.Id == Id
            );

            return (uint) info.Count;
        }

        public async Task SetCountAsync(uint value)
        {
            await UpdateCountAsync(value);
            
            await using var ctx = new UchuContext();

            var info = await ctx.InventoryItems.FirstAsync(
                i => i.Id == Id
            );

            info.Count = value;

            await ctx.SaveChangesAsync();
        }

        public async Task<uint> GetSlotAsync()
        {
            await using var ctx = new UchuContext();

            var info = await ctx.InventoryItems.FirstAsync(
                i => i.Id == Id
            );

            return (uint) info.Slot;
        }

        public async Task SetSlotAsync(uint value)
        {
            await using var ctx = new UchuContext();

            var info = await ctx.InventoryItems.FirstAsync(
                i => i.Id == Id
            );

            info.Slot = (int) value;

            ctx.SaveChanges();
        }
        
        public async Task EquipAsync(bool skipAllChecks = false)
        {
            if (ItemComponent.IsBOE ?? false) await BindAsync();

            var inventory = Player.GetComponent<InventoryComponent>();

            await inventory.EquipItemAsync(this, skipAllChecks);
        }

        public async Task UnEquipAsync()
        {
            var inventory = Player.GetComponent<InventoryComponent>();

            await inventory.UnEquipItemAsync(this);
        }

        public async Task<bool> IsEquippedAsync()
        {
            var item = await Id.FindItemAsync();

            return item.IsEquipped;
        }

        public async Task<bool> IsBoundAsync()
        {
            var item = await Id.FindItemAsync();

            return item.IsBound;
        }

        public async Task BindAsync()
        {
            await using var ctx = new UchuContext();

            var item = await ctx.InventoryItems.FirstAsync(i => i.Id == Id);

            item.IsBound = true;

            await ctx.SaveChangesAsync();
        }

        public async Task ConsumeAsync()
        {
            if (!TryGetComponent<SkillComponent>(out var skillComponent))
            {
                var id = await Lot.GetComponentIdAsync(ComponentId.SkillComponent);
                
                if (id != default)
                {
                    skillComponent = await AddComponentAsync<SkillComponent>();
                }
            }

            bool consumable;

            if (skillComponent != default)
            {
                consumable = skillComponent.DefaultSkillSet.Any(entry => entry.Type == SkillCastType.OnConsumed);
            }
            else
            {
                consumable = TryGetComponent<ItemPackageComponent>(out _);
            }

            if (!consumable) return;
            
            await OnConsumed.InvokeAsync();
            
            await Player.GetComponent<MissionInventoryComponent>().UseConsumableAsync(Lot);

            await Inventory.ManagerComponent.RemoveItemAsync(Lot, 1);
        }

        public static async Task<Item> InstantiateAsync(long itemId, Inventory inventory)
        {
            await using var cdClient = new CdClientContext();
            await using var ctx = new UchuContext();

            var item = await ctx.InventoryItems.FirstOrDefaultAsync(
                i => i.Id == itemId && i.Character.Id == inventory.ManagerComponent.GameObject.Id
            );

            if (item == default)
            {
                Logger.Error($"{itemId} is not an item on {inventory.ManagerComponent.GameObject}");
                return null;
            }

            var cdClientObject = await cdClient.ObjectsTable.FirstOrDefaultAsync(
                o => o.Id == item.Lot
            );

            var itemRegistryEntry = await ((Lot) item.Lot).GetComponentIdAsync(ComponentId.ItemComponent);

            if (cdClientObject == default || itemRegistryEntry == default)
            {
                Logger.Error($"{itemId} [{item.Lot}] is not a valid item");
                return null;
            }

            var instance = await InstantiateAsync<Item>
            (
                inventory.ManagerComponent.Zone, cdClientObject.Name, objectId: itemId, lot: item.Lot
            );

            if (!string.IsNullOrWhiteSpace(item.ExtraInfo))
                instance.Settings = LegoDataDictionary.FromString(item.ExtraInfo);

            instance.Inventory = inventory;
            instance.Player = inventory.ManagerComponent.GameObject as Player;

            return instance;
        }

        public static async Task<Item> InstantiateAsync(Lot lot, Inventory inventory, uint count, LegoDataDictionary extraInfo = default)
        {
            uint slot = default;

            var slots = new List<uint>();

            foreach (var item in inventory.Items)
            {
                slots.Add(await item.GetSlotAsync());
            }
            
            for (var index = 0; index < inventory.Size; index++)
            {
                if (slots.All(s => s != index)) break;

                slot++;
            }

            return await InstantiateAsync(lot, inventory, count, slot, extraInfo);
        }

        public static async Task<Item> InstantiateAsync(Lot lot, Inventory inventory, uint count, uint slot,
            LegoDataDictionary extraInfo = default)
        {
            await using var cdClient = new CdClientContext();
            await using var ctx = new UchuContext();

            var cdClientObject = cdClient.ObjectsTable.FirstOrDefault(
                o => o.Id == lot
            );

            var itemRegistryEntry = cdClient.ComponentsRegistryTable.FirstOrDefault(
                r => r.Id == lot && r.Componenttype == 11
            );

            if (cdClientObject == default || itemRegistryEntry == default)
            {
                Logger.Error($"<new item> [{lot}] is not a valid item");
                return null;
            }

            var instance = await InstantiateAsync<Item>
            (
                inventory.ManagerComponent.Zone, cdClientObject.Name, objectId: ObjectId.Standalone, lot: lot
            );

            instance.Settings = extraInfo ?? new LegoDataDictionary();

            var itemComponent = await cdClient.ItemComponentTable.FirstAsync(
                i => i.Id == itemRegistryEntry.Componentid
            );

            instance.Inventory = inventory;
            instance.Player = inventory.ManagerComponent.GameObject as Player;

            var playerCharacter = await ctx.Characters.Include(c => c.Items).FirstAsync(
                c => c.Id == inventory.ManagerComponent.GameObject.Id
            );

            var inventoryItem = new InventoryItem
            {
                Count = count,
                InventoryType = (int) inventory.InventoryType,
                Id = instance.Id,
                IsBound = itemComponent.IsBOP ?? false,
                Slot = (int) slot,
                Lot = lot,
                ExtraInfo = extraInfo?.ToString()
            };

            playerCharacter.Items.Add(inventoryItem);

            ctx.SaveChanges();

            var message = new AddItemToInventoryMessage
            {
                Associate = inventory.ManagerComponent.GameObject,
                InventoryType = (int) inventory.InventoryType,
                Delta = count,
                TotalItems = count,
                Slot = (int) slot,
                ItemLot = lot,
                IsBoundOnEquip = itemComponent.IsBOE ?? false,
                IsBoundOnPickup = itemComponent.IsBOP ?? false,
                IsBound = inventoryItem.IsBound,
                Item = instance,
                ExtraInfo = extraInfo
            };

            (inventory.ManagerComponent.GameObject as Player)?.Message(message);

            inventory.ManageItem(instance);

            return instance;
        }

        public async Task UpdateCountSilentAsync(uint count)
        {
            await using var ctx = new UchuContext();

            if (count > ItemComponent.StackSize && ItemComponent.StackSize > 0)
            {
                Logger.Error(
                    $"Trying to set {Lot} count to {count}, this is beyond the item's stack-size; Setting it to stack-size"
                );

                count = (uint) ItemComponent.StackSize;
            }

            var item = await ctx.InventoryItems.FirstAsync(i => i.Id == Id);

            item.Count = count;
            
            await ctx.SaveChangesAsync();

            if (count <= 0)
            {
                await DisassembleAsync();

                await RemoveFromInventoryAsync();
            }
        }

        private async Task UpdateCountAsync(uint count)
        {
            if (count >= await GetCountAsync())
            {
                await AddCountAsync(count);
            }
            else
            {
                await RemoveCountAsync(count);
            }

            if (count > 0) return;

            await DisassembleAsync();

            await RemoveFromInventoryAsync();
        }

        private async Task DisassembleAsync()
        {
            await using var ctx = new UchuContext();

            var item = await ctx.InventoryItems.FirstAsync(i => i.Id == Id);

            if (LegoDataDictionary.FromString(item.ExtraInfo).TryGetValue("assemblyPartLOTs", out var list))
                foreach (var part in (LegoDataList) list)
                    await Inventory.ManagerComponent.AddItemAsync((int) part, 1);
        }

        private async Task AddCountAsync(uint count)
        {
            await using var ctx = new UchuContext();

            var item = await ctx.InventoryItems.FirstAsync(i => i.Id == Id);

            var message = new AddItemToInventoryMessage
            {
                Associate = Player,
                Item = this,
                ItemLot = Lot,
                Delta = (uint) (count - item.Count),
                Slot = (int) await GetSlotAsync(),
                InventoryType = (int) Inventory.InventoryType,
                ShowFlyingLoot = count != default,
                TotalItems = count,
                ExtraInfo = null // TODO
            };

            Player.Message(message);
        }

        private async Task RemoveCountAsync(uint count)
        {
            await using var ctx = new UchuContext();

            var item = await ctx.InventoryItems.FirstAsync(i => i.Id == Id);

            var message = new RemoveItemToInventoryMessage
            {
                Associate = Player,
                Confirmed = true,
                DeleteItem = true,
                OutSuccess = false,
                ItemType = (ItemType) (ItemComponent.ItemType ?? -1),
                InventoryType = Inventory.InventoryType,
                ExtraInfo = null, // TODO
                ForceDeletion = true,
                Item = this,
                Delta = (uint) Math.Abs(count - item.Count),
                TotalItems = count
            };

            Player.Message(message);
        }

        private async Task RemoveFromInventoryAsync()
        {
            await using var ctx = new UchuContext();

            var character = await ctx.Characters.Include(c => c.Items).FirstAsync(
                c => c.Id == Player.Id
            );
            
            var item = character.Items.First(i => i.Id == Id);

            character.Items.Remove(item);

            await ctx.SaveChangesAsync();
                
            await DestroyAsync(this);
        }
    }
}