﻿using System.IO;
using System.Runtime.Versioning;
using System.Text;
using System.Threading.Tasks;
using Masb.NuGet.Multiple.Targeting.Tool.Json;
using Masb.NuGet.Multiple.Targeting.Tool.Storage;
using Newtonsoft.Json;

namespace Masb.NuGet.Multiple.Targeting.Tool.InfoModel
{
    public class StorageFrameworkInfoCache : IFrameworkInfoCache
    {
        private readonly IBlobStorageManager storage;
        private readonly InMemoryFrameworkInfoCache innerCache = new InMemoryFrameworkInfoCache();

        public StorageFrameworkInfoCache(IBlobStorageManager storage)
        {
            this.storage = storage;
        }

        public async Task<FrameworkInfo> GetValueAsync(FrameworkName frameworkName)
        {
            var value = await this.innerCache.GetValueAsync(frameworkName);
            if (value != null)
                return value;

            using (var stream = await this.storage.DownloadFileFromStorageAsync(
                "FrameworkInfoCache",
                frameworkName.ToString()))
            {
                if (stream != null)
                {
                    using (var reader = new StreamReader(stream, Encoding.UTF8))
                    {
                        var code = await reader.ReadToEndAsync();

                        var settings = new JsonSerializerSettings
                        {
                            Formatting = Formatting.Indented,
                            Converters = new[]
                            {
                                new FrameworkNameConverter()
                            }
                        };

                        var frmkInfo = await FrameworkInfoJson.ValueAsync(JsonConvert.DeserializeObject<FrameworkInfoJson>(code, settings));
                        value = frmkInfo;
                    }

                    await this.innerCache.SetItemAsync(frameworkName, value);
                }
            }

            return value;
        }

        public async Task SetItemAsync(FrameworkName frameworkName, FrameworkInfo frmkInfo)
        {
            if (frmkInfo != null)
            {
                var settings = new JsonSerializerSettings
                    {
                        Formatting = Formatting.Indented,
                        Converters = new[]
                            {
                                new FrameworkNameConverter()
                            }
                    };

                var str = JsonConvert.SerializeObject(FrameworkInfoJson.From(frmkInfo), settings);

                using (var stream = new MemoryStream())
                using (var writer = new StreamWriter(stream, Encoding.UTF8))
                {
                    await writer.WriteAsync(str);
                    await writer.FlushAsync();

                    stream.Position = 0;

                    await this.storage.UploadFileToStorageAsync(
                        stream,
                        "FrameworkInfoCache",
                        frameworkName.ToString());
                }
            }

            await this.innerCache.SetItemAsync(frameworkName, frmkInfo);
        }
    }
}