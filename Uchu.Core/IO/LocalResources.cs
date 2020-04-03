using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Uchu.Core.IO
{
    public class LocalResources : IFileResources
    {
        private Configuration Configuration { get; }

        public string Root => Configuration.ResourceConfiguration.Root;

        public LocalResources(Configuration configuration)
        {
            Configuration = configuration;
        }

        public async Task<string> ReadTextAsync(string path)
        {
            await using var stream = GetStream(path);
            using var reader = new StreamReader(stream);
            return await reader.ReadToEndAsync().ConfigureAwait(false);
        }

        public async Task<byte[]> ReadBytesAsync(string path)
        {
            await using var stream = GetStream(path);
            var bytes = new byte[stream.Length];

            await stream.ReadAsync(bytes, 0, (int) stream.Length).ConfigureAwait(false);

            return bytes;
        }

        public byte[] ReadBytes(string path)
        {
            using var stream = GetStream(path);
            var bytes = new byte[stream.Length];

            stream.Read(bytes, 0, (int) stream.Length);

            return bytes;
        }

        public IEnumerable<string> GetAllFilesWithExtension(string extension)
        {
            var files = Directory.GetFiles(
                Root,
                $"*.{extension}",
                SearchOption.AllDirectories
            );
            
            var folder = new Uri(Root);

            for (var i = 0; i < files.Length; i++)
            {
                var file = new Uri(files[i]);

                var final = Uri.UnescapeDataString(
                    folder.MakeRelativeUri(file)
                        .ToString()
                        .Replace('\\', '/')
                );

                files[i] = $"../{final}";
            }

            return files;
        }
        
        public IEnumerable<string> GetAllFilesWithExtension(string location, string extension)
        {
            var files = Directory.GetFiles(
                Path.Combine(Root, location),
                $"*.{extension}",
                SearchOption.TopDirectoryOnly
            );

            return files;
        }
        
        public Stream GetStream(string path)
        {
            path = path.Replace('\\', '/').ToLowerInvariant();

            return File.OpenRead(Path.Combine(Root, path));
        }
    }
}