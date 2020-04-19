using System;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using InfectedRose.Lvl;
using Microsoft.EntityFrameworkCore;
using RakDotNet;
using RakDotNet.IO;
using Uchu.Api.Models;
using Uchu.Core;
using Uchu.Core.Client;
using Uchu.World.Filters;
using Uchu.World.Social;

namespace Uchu.World
{
    public sealed class Player : GameObject
    {
        private Player()
        {
            OnFireServerEvent = new AsyncEventDictionary<string, FireServerEventMessage>();

            OnLootPickup = new AsyncEvent<Lot>();

            OnPositionUpdate = new AsyncEvent<Vector3, Quaternion>();

            Listen(OnStart, async () =>
            {
                Connection.Disconnected += async reason =>
                {
                    Connection = default;

                    await DestroyAsync(this);
                };

                if (TryGetComponent<DestructibleComponent>(out var destructibleComponent))
                {
                    destructibleComponent.OnResurrect.AddListener(async () =>
                    {
                        await GetComponent<Stats>().SetImaginationAsync(6);
                    });
                }

                await using var ctx = new UchuContext();

                var character = await ctx.Characters
                    .Include(c => c.UnlockedEmotes)
                    .FirstAsync(c => c.Id == Id);

                foreach (var unlockedEmote in character.UnlockedEmotes)
                {
                    await UnlockEmoteAsync(unlockedEmote.EmoteId);
                }

                Zone.Update(this, async () =>
                {
                    await Perspective.TickAsync();

                    await CheckBannedStatusAsync();
                }, 20);
            });

            Listen(OnDestroyed, () =>
            {
                OnFireServerEvent.Clear();
                OnLootPickup.Clear();
                OnPositionUpdate.Clear();

                return Task.CompletedTask;
            });
        }

        public AsyncEventDictionary<string, FireServerEventMessage> OnFireServerEvent { get; }

        public AsyncEvent<Lot> OnLootPickup { get; }

        public AsyncEvent<Vector3, Quaternion> OnPositionUpdate { get; }

        public IRakConnection Connection { get; private set; }

        public Perspective Perspective { get; private set; }

        public long EntitledCurrency { get; set; }

        public PlayerChatChannel ChatChannel { get; set; }

        public GuildGuiState GuildGuiState { get; set; }

        public string GuildInviteName { get; set; }

        public int Ping => Connection.AveragePing;

        public override string Name
        {
            get => ObjectName;
            set
            {
                ObjectName = value;

                Zone.BroadcastMessage(new SetNameMessage
                {
                    Associate = this,
                    Name = value
                });
            }
        }

        /// <summary>
        ///    Negative offset for the SetCurrency message.
        /// </summary>
        /// <remarks>
        ///    Used when the client adds currency by itself. E.g, achievements.
        /// </remarks>
        public long HiddenCurrency { get; set; }

        public async Task<long> GetCurrencyAsync()
        {
            await using var ctx = new UchuContext();

            var character = await ctx.Characters.FirstAsync(c => c.Id == Id);

            return character.Currency;
        }

        public async Task<long> GetUniverseScoreAsync()
        {
            await using var ctx = new UchuContext();

            var character = await ctx.Characters.FirstAsync(c => c.Id == Id);

            return character.UniverseScore;
        }

        public async Task<long> GetLevelAsync()
        {
            await using var ctx = new UchuContext();

            var character = await ctx.Characters.FirstAsync(c => c.Id == Id);

            return character.Level;
        }

        public async Task<Character> GetCharacterAsync()
        {
            await using var ctx = new UchuContext();

            return await ctx.Characters.FirstAsync(c => c.Id == Id);
        }

        private async Task CheckBannedStatusAsync()
        {
            await using var ctx = new UchuContext();

            var character = await ctx.Characters.FirstAsync(c => c.Id == Id);

            var user = await ctx.Users.FirstAsync(u => u.Id == character.UserId);

            if (!user.Banned) return;

            try
            {
                await Connection.CloseAsync();
            }
            catch (Exception e)
            {
                Logger.Error(e);
            }
        }

        public async Task<float[]> GetCollectedAsync()
        {
            await using var ctx = new UchuContext();
            await using var cdContext = new CdClientContext();

            var character = await ctx.Characters
                .Include(c => c.Missions)
                .ThenInclude(m => m.Tasks)
                .ThenInclude(t => t.Values)
                .SingleOrDefaultAsync(c => c.Id == Id);

            var flagTaskIds = cdContext.MissionTasksTable
                .Where(t => t.TaskType == (int) MissionTaskType.Collect)
                .Select(t => t.Uid);

            // Get all the mission task values that correspond to flag values
            var flagValues = character.Missions
                .SelectMany(m => m.Tasks
                    .Where(t => flagTaskIds.Contains(t.TaskId))
                    .SelectMany(t => t.ValueArray())).ToArray();

            return flagValues;
        }

        internal static async Task<Player> ConstructAsync(Character character, IRakConnection connection, Zone zone)
        {
            //
            // Create base gameobject
            //

            var instance = await InstantiateAsync<Player>(
                zone,
                character.Name,
                zone.SpawnPosition,
                zone.SpawnRotation,
                1,
                character.Id,
                1
            );

            //
            // Setup layers
            //

            instance.Layer = StandardLayer.Player;

            var layer = StandardLayer.All;
            layer -= StandardLayer.Hidden;
            layer -= StandardLayer.Spawner;

            instance.Perspective = new Perspective(instance);

            var maskFilter = instance.Perspective.AddFilter<MaskFilter>();
            maskFilter.ViewMask = layer;

            instance.Perspective.AddFilter<RenderDistanceFilter>();
            instance.Perspective.AddFilter<FlagFilter>();
            instance.Perspective.AddFilter<ExcludeFilter>();

            //
            // Set connection
            //

            instance.Connection = connection;

            //
            // Add serialized components
            //

            var controllablePhysics = await instance.AddComponentAsync<ControllablePhysicsComponent>();

            await instance.AddComponentAsync<DestructibleComponent>();

            var stats = instance.GetComponent<Stats>();
            var characterComponent = await instance.AddComponentAsync<CharacterComponent>();
            var inventory = await instance.AddComponentAsync<InventoryComponent>();

            await instance.AddComponentAsync<LuaScriptComponent>();
            await instance.AddComponentAsync<SkillComponent>();
            await instance.AddComponentAsync<RendererComponent>();
            await instance.AddComponentAsync<PossessableOccupantComponent>();

            controllablePhysics.HasPosition = true;
            stats.HasStats = true;
            characterComponent.Character = character;

            //
            // Equip items
            //

            await using (var ctx = new UchuContext())
            {
                var items = await ctx.InventoryItems.Where(
                    i => i.CharacterId == character.Id && i.IsEquipped
                ).ToArrayAsync();

                foreach (var item in items)
                {
                    if (item.ParentId != ObjectId.Invalid) continue;

                    await inventory.EquipAsync(new EquippedItem
                    {
                        Id = item.Id,
                        Lot = item.Lot
                    });
                }
            }

            //
            // Register player gameobject in zone
            //

            Logger.Information($"Starting player");
            
            await StartAsync(instance);
            
            Logger.Information($"Construing player");
            
            Construct(instance);

            //
            // Server Components
            //

            Logger.Information("Adding server components");
            
            await instance.AddComponentAsync<MissionInventoryComponent>();
            await instance.AddComponentAsync<InventoryManagerComponent>();
            await instance.AddComponentAsync<TeamPlayerComponent>();
            await instance.AddComponentAsync<ModularBuilderComponent>();

            //
            // Register player as an active in zone
            //

            Logger.Information($"Regestring player");
            
            await zone.RegisterPlayerAsync(instance);

            return instance;
        }

        public async Task UnlockEmoteAsync(int emoteId)
        {
            await using var ctx = new UchuContext();

            var character = await ctx.Characters
                .Include(c => c.UnlockedEmotes)
                .FirstAsync(c => c.Id == Id);

            if (character.UnlockedEmotes.All(u => u.EmoteId != emoteId))
            {
                character.UnlockedEmotes.Add(new UnlockedEmote
                {
                    EmoteId = emoteId
                });

                await ctx.SaveChangesAsync();
            }

            Message(new SetEmoteLockStateMessage
            {
                Associate = this,
                EmoteId = emoteId,
                Lock = false
            });
        }

        public void Teleport(Vector3 position)
        {
            Message(new TeleportMessage
            {
                Associate = this,
                Position = position
            });
        }

        internal void UpdateView()
        {
            foreach (var gameObject in Zone.Spawned)
            {
                var spawned = Perspective.LoadedObjects.ToArray().Contains(gameObject);

                var view = Perspective.View(gameObject);

                if (spawned && !view)
                {
                    Zone.SendDestruction(gameObject, this);

                    continue;
                }

                if (!spawned && view)
                {
                    Zone.SendConstruction(gameObject, this);
                }
            }
        }

        internal void UpdateView(GameObject gameObject)
        {
            var spawned = Perspective.LoadedObjects.ToArray().Contains(gameObject);

            var view = Perspective.View(gameObject);

            if (spawned && !view)
            {
                Zone.SendDestruction(gameObject, this);

                return;
            }

            if (!spawned && view)
            {
                Zone.SendConstruction(gameObject, this);
            }
        }

        public void SendChatMessage(string message, PlayerChatChannel channel = PlayerChatChannel.Debug,
            Player author = null, ChatChannel chatChannel = World.ChatChannel.Public)
        {
            if (channel > ChatChannel) return;

            Message(new ChatMessagePacket
            {
                Message = $"{message}\0",
                Sender = author,
                IsMythran = author?.GameMasterLevel > 0,
                Channel = chatChannel
            });
        }

        public void Message(ISerializable package)
        {
            Connection.Send(package);
        }

        public async Task<bool> SendToWorldAsync(InstanceInfo specification, ZoneId zoneId)
        {
            Message(new ServerRedirectionPacket
            {
                Port = (ushort) specification.Port,
                Address = Server.GetHost()
            });

            await using var ctx = new UchuContext();

            var character = await ctx.Characters.FirstAsync(c => c.Id == Id);

            character.LastZone = zoneId;

            await ctx.SaveChangesAsync();

            return true;
        }

        public async Task<bool> SendToWorldAsync(ZoneId zoneId)
        {
            var server = await ServerHelper.RequestWorldServerAsync(Server, zoneId);

            if (server == default)
            {
                return false;
            }

            if (Server.Port != server.Port) return await SendToWorldAsync(server, zoneId);

            Logger.Error("Could not send a player to the same port as it already has");

            return false;
        }

        public async Task SetCurrencyAsync(long currency)
        {
            await using (var ctx = new UchuContext())
            {
                var character = await ctx.Characters.FirstAsync(c => c.Id == Id);

                character.Currency = currency;
                character.TotalCurrencyCollected += currency;

                await ctx.SaveChangesAsync();
            }

            Message(new SetCurrencyMessage
            {
                Associate = this,
                Currency = currency - HiddenCurrency
            });
        }

        public async Task SetUniverseScoreAsync(long score)
        {
            await using var ctx = new UchuContext();
            await using var cdClient = new CdClientContext();

            var character = await ctx.Characters.FirstAsync(c => c.Id == Id);

            character.UniverseScore = score;

            foreach (var levelProgressionLookup in cdClient.LevelProgressionLookupTable)
            {
                if (levelProgressionLookup.RequiredUScore > score) break;

                if (levelProgressionLookup.Id != null) character.Level = levelProgressionLookup.Id.Value;
            }

            Message(new ModifyLegoScoreMessage
            {
                Associate = this,
                Score = character.UniverseScore - await GetUniverseScoreAsync()
            });

            await ctx.SaveChangesAsync();
        }

        public async Task SetLevelAsync(long level)
        {
            await using var ctx = new UchuContext();
            await using var cdClient = new CdClientContext();

            var character = await ctx.Characters.FirstAsync(c => c.Id == Id);

            var lookup = await cdClient.LevelProgressionLookupTable.FirstOrDefaultAsync(l => l.Id == level);

            if (lookup == default)
            {
                Logger.Error($"Trying to set {this} level to a level that does not exist.");
                return;
            }

            character.Level = level;

            if (lookup.RequiredUScore != null) character.UniverseScore = lookup.RequiredUScore.Value;

            Message(new ModifyLegoScoreMessage
            {
                Associate = this,
                Score = character.UniverseScore - await GetUniverseScoreAsync()
            });

            await ctx.SaveChangesAsync();
        }
    }
}