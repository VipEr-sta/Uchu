using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using RakDotNet.IO;
using Uchu.Core;

namespace Uchu.World.Systems.Behaviors
{
    using SyncDelegate = Func<BitReader, bool, Task>;

    public class ExecutionContext
    {
        public GameObject Associate { get; }

        public BehaviorBase Root { get; set; }

        public BitWriter Writer { get; set; }

        public GameObject ExplicitTarget { get; set; }

        public List<BehaviorSyncEntry> BehaviorHandles { get; set; } = new List<BehaviorSyncEntry>();

        public ExecutionContext(GameObject associate, BitWriter writer)
        {
            Associate = associate;
            Writer = writer;
        }

        public async Task SyncAsync(uint handle, BitReader reader, bool fallowUp)
        {
            BehaviorSyncEntry entry;
            
            DebugMessage($"Sync: [{fallowUp}] {handle}");
            
            lock (BehaviorHandles)
            {
                entry = BehaviorHandles.FirstOrDefault(e => e.Handle == handle);

                if (entry == default)
                {
                    Logger.Error($"Invalid behavior sync id: {handle}!");
                    
                    return;
                }
                
                BehaviorHandles.Remove(entry);
            }

            await entry.Delegate(reader, fallowUp);
        }

        public void RegisterHandle(uint handle, SyncDelegate @delegate)
        {
            lock (BehaviorHandles)
            {
                BehaviorHandles.Add(new BehaviorSyncEntry
                {
                    Handle = handle,
                    Delegate = @delegate
                });
            }
        }

        public void DebugMessage(string message)
        {
            if (!(Associate is Player player)) return;

            player.SendChatMessage(message);
        }

        public class BehaviorSyncEntry
        {
            public uint Handle { get; set; }

            public SyncDelegate Delegate { get; set; }
        }
    }
}