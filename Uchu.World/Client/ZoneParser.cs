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
        private readonly IFileResources _resources;

        public Dictionary<ZoneId, ZoneInfo> Zones { get; }

        public ZoneParser(IFileResources resources)
        {
            _resources = resources;

            Zones = new Dictionary<ZoneId, ZoneInfo>();
        }

        public async Task<ZoneInfo> LoadZoneDataAsync(ZoneId seek)
        {
            if (Zones.TryGetValue(seek, out var info))
                return info;

            await using var cdClient = new CdClientContext();

            var entry = await cdClient.ZoneTableTable.FirstOrDefaultAsync(z => z.ZoneID == seek);

            if (entry == default)
            {
                throw new KeyNotFoundException($"ZoneId {seek} not found.");
            }

            await using var stream = _resources.GetStream(Path.Combine("maps", entry.ZoneName));

            var luz = new LuzFile();

            var reader = new BitReader(stream);

            luz.Deserialize(reader);

            var path = Path.Combine("maps", Path.GetDirectoryName(entry.ZoneName)).ToLower();

            var lvlFiles = new List<LvlFile>();

            foreach (var scene in luz.Scenes)
            {
                await using var sceneStream = _resources.GetStream(Path.Combine(path, scene.FileName));

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

            var terrainStream = _resources.GetStream(Path.Combine(path, luz.TerrainFileName));

            using var terrainReader = new BitReader(terrainStream);

            var terrain = new TerrainFile();

            terrain.Deserialize(terrainReader);

            var triggers = await TriggerDictionary.FromDirectoryAsync(Path.Combine(_resources.RootPath, path));

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