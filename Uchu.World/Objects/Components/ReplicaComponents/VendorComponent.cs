using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using RakDotNet.IO;
using Uchu.Core;
using Uchu.Core.Client;

namespace Uchu.World
{
    public class VendorComponent : ReplicaComponent
    {
        public override ComponentId Id => ComponentId.VendorComponent;
        
        public ShopEntry[] Entries { get; set; }
        
        public AsyncEvent<Lot, uint, Player> OnBuy { get; } = new AsyncEvent<Lot, uint, Player>();
        
        public AsyncEvent<Item, uint, Player> OnSell { get; } = new AsyncEvent<Item, uint, Player>();
        
        public AsyncEvent<Item, uint, Player> OnBuyback { get; } = new AsyncEvent<Item, uint, Player>();

        protected VendorComponent()
        {
            Listen(OnStart, async () =>
            {
                await SetupEntriesAsync();
                
                Listen(GameObject.OnInteract, player =>
                {
                    OnInteract(player);
                    
                    return Task.CompletedTask;
                });
            });
        }

        public override void Construct(BitWriter writer)
        {
            Serialize(writer);
        }

        public override void Serialize(BitWriter writer)
        {
            writer.WriteBit(false);
        }

        private void OnInteract(Player player)
        {
            player.Message(new OpenVendorWindowMessage
            {
                Associate = GameObject
            });
            
            player.Message(new VendorStatusUpdateMessage
            {
                Associate = GameObject,
                Entries = Entries
            });
        }

        private async Task SetupEntriesAsync()
        {
            await using var ctx = new CdClientContext();
            
            var componentId = await GameObject.Lot.GetComponentIdAsync(ComponentId.VendorComponent);

            var vendorComponent = await ctx.VendorComponentTable.FirstAsync(
                c => c.Id == componentId
            );

            var matrices = ctx.LootMatrixTable.Where(
                l => l.LootMatrixIndex == vendorComponent.LootMatrixIndex
            );

            var shopItems = new List<ShopEntry>();

            foreach (var matrix in matrices)
            {
                shopItems.AddRange(ctx.LootTableTable.Where(
                    l => l.LootTableIndex == matrix.LootTableIndex
                ).ToArray().Select(lootTable =>
                {
                    Debug.Assert(lootTable.Itemid != null, "lootTable.Itemid != null");
                    Debug.Assert(lootTable.SortPriority != null, "lootTable.SortPriority != null");
                    
                    return new ShopEntry
                    {
                        Lot = new Lot(lootTable.Itemid.Value),
                        SortPriority = lootTable.SortPriority.Value
                    };
                }));
            }

            Entries = shopItems.ToArray();
        }

        public async Task BuyAsync(Lot lot, uint count, Player player)
        {
            await using var ctx = new CdClientContext();

            var componentId = await lot.GetComponentIdAsync(ComponentId.ItemComponent);

            var itemComponent = await ctx.ItemComponentTable.FirstAsync(
                i => i.Id == componentId
            );
            
            if (count == default || itemComponent.BaseValue <= 0) return;

            var cost = (uint) ((itemComponent.BaseValue ?? 0) * count);

            var currency = await player.GetCurrencyAsync();
            
            if (cost > currency) return;

            await player.SetCurrencyAsync(currency - cost);
            
            await player.GetComponent<InventoryManagerComponent>().AddItemAsync(lot, count);
            
            player.Message(new VendorTransactionResultMessage
            {
                Associate = GameObject,
                Result = TransactionResult.Success
            });

            await OnBuy.InvokeAsync(lot, count, player);
        }

        public async Task SellAsync(Item item, uint count, Player player)
        {
            var itemComponent = item.ItemComponent;
            
            if (count == default || itemComponent.BaseValue <= 0) return;
            
            await player.GetComponent<InventoryManagerComponent>().MoveItemsBetweenInventoriesAsync(
                default,
                item.Lot,
                count,
                item.Inventory.InventoryType,
                InventoryType.VendorBuyback
            );

            var returnCurrency =
                Math.Floor(
                    (itemComponent.BaseValue ?? 0) *
                    (itemComponent.SellMultiplier ?? 0.1f)
                ) * count;

            var currency = await player.GetCurrencyAsync();

            await player.SetCurrencyAsync(currency + (uint) returnCurrency);
            
            player.Message(new VendorTransactionResultMessage
            {
                Associate = GameObject,
                Result = TransactionResult.Success
            });

            await OnSell.InvokeAsync(item, count, player);
        }

        public async Task BuybackAsync(Item item, uint count, Player player)
        {
            var itemComponent = item.ItemComponent;
            
            if (count == default || itemComponent.BaseValue <= 0) return;

            var cost =
                (uint) Math.Floor(
                    (itemComponent.BaseValue ?? 0) *
                    (itemComponent.SellMultiplier ?? 0.1f)
                ) * count;

            var currency = await player.GetCurrencyAsync();
            
            if (cost > currency) return;

            await player.SetCurrencyAsync(currency - cost);
            
            var manager = player.GetComponent<InventoryManagerComponent>();
            
            await manager.RemoveItemAsync(item.Lot, count, InventoryType.VendorBuyback);
            
            await manager.AddItemAsync(item.Lot, count);
            
            player.Message(new VendorTransactionResultMessage
            {
                Associate = GameObject,
                Result = TransactionResult.Success
            });

            await OnBuyback.InvokeAsync(item, count, player);
        }
    }
}