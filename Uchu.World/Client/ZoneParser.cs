using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using InfectedRose.Luz;
using InfectedRose.Lvl;
using InfectedRose.Terrain;
using InfectedRose.Triggers;
using Microsoft.EntityFrameworkCore;
using RakDotNet.IO;
using Uchu.Core;
using Uchu.Core.Client;
using Uchu.Core.IO;

namespace Uchu.World.Client
{
    public class ZoneParser
    {
        private IFileResources Resources { get; }

        private ResourceConfiguration Configuration { get; }

        private Dictionary<ZoneId, ZoneInfo> Zones { get; }

        public ZoneParser(IFileResources resources, ResourceConfiguration configuration)
        {
            Resources = resources;

            Configuration = configuration;

            Zones = new Dictionary<ZoneId, ZoneInfo>();
        }

        public async Task<ZoneInfo> LoadZoneDataAsync(ZoneId zoneId)
        {
            if (Zones.TryGetValue(zoneId, out var info))
                return info;

            await using var cdClient = new CdClientContext();

            var entry = await cdClient.ZoneTableTable.FirstOrDefaultAsync(z => z.ZoneID == zoneId);

            if (entry == default)
            {
                throw new KeyNotFoundException($"ZoneId {zoneId} not found.");
            }

            await using var stream = Resources.GetStream(Path.Combine(Configuration.Maps, entry.ZoneName));

            var luz = new LuzFile();

            var reader = new BitReader(stream);

            luz.Deserialize(reader);

            var path = Path.Combine(Configuration.Maps, Path.GetDirectoryName(entry.ZoneName)).ToLower();

            var lvlFiles = new List<LvlFile>();

            foreach (var scene in luz.Scenes)
            {
                await using var sceneStream = Resources.GetStream(Path.Combine(path, scene.FileName));

                using var sceneReader = new BitReader(sceneStream);

                Logger.Information($"Parsing: {scene.FileName}");

                var lvl = new LvlFile();

                lvl.Deserialize(sceneReader);

                lvlFiles.Add(lvl);

                if (lvl.LevelObjects?.Templates == default) continue;

                foreach (var template in lvl.LevelObjects.Templates)
                {
                    template.ObjectId |= 70368744177664;
                }
            }

            var terrainStream = Resources.GetStream(Path.Combine(path, luz.TerrainFileName));

            using var terrainReader = new BitReader(terrainStream);

            var terrain = new TerrainFile();

            terrain.Deserialize(terrainReader);

            var triggers = await TriggerDictionary.FromDirectoryAsync(Path.Combine(Resources.Root, path));

            Logger.Information($"Parsed: {(ZoneId) luz.WorldId}");

            info = new ZoneInfo
            {
                LuzFile = luz,
                LvlFiles = lvlFiles,
                TriggerDictionary = triggers,
                TerrainFile = terrain
            };

            Zones[(ZoneId) luz.WorldId] = info;

            return info;
        }
    }
}