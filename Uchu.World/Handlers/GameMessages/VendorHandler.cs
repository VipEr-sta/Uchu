using System.Threading.Tasks;
using Uchu.Core;

namespace Uchu.World.Handlers.GameMessages
{
    public class VendorHandler : HandlerGroup
    {
        [PacketHandler]
        public async Task BuyFromVendorHandler(BuyFromVendorMessage message, Player player)
        {
            await message.Associate.GetComponent<VendorComponent>().BuyAsync(message.Lot, (uint) message.Count, player);
        }
        
        [PacketHandler]
        public async Task SellToVendorHandler(SellToVendorMessage message, Player player)
        {
            await message.Associate.GetComponent<VendorComponent>().SellAsync(message.Item, (uint) message.Count, player);
        }
        
        [PacketHandler]
        public async Task BuybackFromVendorHandler(BuybackFromVendorMessage message, Player player)
        {
            await message.Associate.GetComponent<VendorComponent>().BuybackAsync(message.Item, (uint) message.Count, player);
        }
    }
}