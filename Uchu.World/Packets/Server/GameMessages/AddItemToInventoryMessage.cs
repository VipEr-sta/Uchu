using System.Collections.Generic;
using System.Numerics;
using RakDotNet;
using Uchu.Core;

namespace Uchu.World
{
    public class AddItemToInventoryMessage : ServerGameMessage
    {
        public override ushort GameMessageId => 0x00E3;

        public bool IsBound { get; set; } = false;
        public bool IsBOE { get; set; } = false;
        public bool IsBOP { get; set; } = false;
        public int Source { get; set; } = -1;
        public Dictionary<string, object> ExtraInfo { get; set; } = new Dictionary<string, object>();
        public int ItemLOT { get; set; }
        public long Subkey { get; set; } = -1;
        public int InventoryType { get; set; } = -1;
        public uint ItemCount { get; set; } = 1;
        public uint TotalItems { get; set; } = 0;
        public long ItemObjectId { get; set; }
        public Vector3 FlyingLootPosition { get; set; } = Vector3.Zero;
        public bool ShowFlyingLoot { get; set; } = true;
        public int Slot { get; set; }

        public override void SerializeMessage(BitStream stream)
        {
            stream.WriteBit(IsBound);
            stream.WriteBit(IsBOE);
            stream.WriteBit(IsBOP);

            var hasSource = Source != -1;

            stream.WriteBit(hasSource);

            if (hasSource)
                stream.WriteInt(Source);

            // TODO: implement
            stream.WriteUInt(0);

            stream.WriteInt(ItemLOT);

            var hasSubkey = Subkey != -1;

            stream.WriteBit(hasSubkey);

            if (hasSubkey)
                stream.WriteLong(Subkey);

            var hasInvType = InventoryType != -1;

            stream.WriteBit(hasInvType);

            if (hasInvType)
                stream.WriteInt(InventoryType);

            var hasCount = ItemCount > 1;

            stream.WriteBit(hasCount);

            if (hasCount)
                stream.WriteUInt(ItemCount);

            var hasTotal = TotalItems != 0;

            stream.WriteBit(hasTotal);

            if (hasTotal)
                stream.WriteUInt(TotalItems);

            stream.WriteLong(ItemObjectId);

            stream.WriteFloat(FlyingLootPosition.X);
            stream.WriteFloat(FlyingLootPosition.Y);
            stream.WriteFloat(FlyingLootPosition.Z);

            stream.WriteBit(ShowFlyingLoot);

            stream.WriteInt(Slot);
        }
    }
}