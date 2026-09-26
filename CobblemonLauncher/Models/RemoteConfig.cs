using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace CobblemonLauncher.Models
{
    public class RemoteConfig
    {
        [JsonPropertyName("launcher_version")]
        public string LauncherVersion { get; set; } = "2.0.2";

        [JsonPropertyName("launcher_download_url")]
        public string LauncherDownloadUrl { get; set; } = "";

        [JsonPropertyName("launcher_sha256")]
        public string LauncherSha256 { get; set; } = "";

        [JsonPropertyName("launcher_changelog")]
        public List<string> LauncherChangelog { get; set; } = new();

        [JsonPropertyName("server_ip")]
        public string ServerIp { get; set; } = "";

        [JsonPropertyName("additional_mods")]
        public List<RemoteModEntry> AdditionalMods { get; set; } = new();

        [JsonPropertyName("skins")]
        public List<SkinItem> Skins { get; set; } = new();
    }

    public class SkinItem
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";

        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("description")]
        public string Description { get; set; } = "";

        [JsonPropertyName("price")]
        public int Price { get; set; } = 1000;

        [JsonPropertyName("fileName")]
        public string FileName { get; set; } = "";

        [JsonPropertyName("downloadUrl")]
        public string DownloadUrl { get; set; } = "";

        [JsonPropertyName("previewUrl")]
        public string PreviewUrl { get; set; } = "";
    }

    public class RemoteModEntry
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("slug")]
        public string Slug { get; set; } = "";

        [JsonPropertyName("fileName")]
        public string FileName { get; set; } = "";

        [JsonPropertyName("downloadUrl")]
        public string DownloadUrl { get; set; } = "";

        [JsonPropertyName("expectedSize")]
        public long ExpectedSize { get; set; }

        [JsonPropertyName("isDatapack")]
        public bool IsDatapack { get; set; }

        [JsonPropertyName("isResourcepack")]
        public bool IsResourcepack { get; set; }

        [JsonPropertyName("type")]
        public string Type { get; set; } = "";

        [JsonIgnore]
        public bool IsEffectiveResourcepack =>
            IsResourcepack ||
            string.Equals(Type, "resourcepack", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(Type, "resource_pack", StringComparison.OrdinalIgnoreCase);

        [JsonIgnore]
        public bool IsEffectiveDatapack =>
            !IsEffectiveResourcepack &&
            (IsDatapack ||
             string.Equals(Type, "datapack", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(Type, "data_pack", StringComparison.OrdinalIgnoreCase));
    }

    public class InstalledModRecord
    {
        [JsonPropertyName("fileName")]
        public string FileName { get; set; } = "";

        [JsonPropertyName("isDatapack")]
        public bool IsDatapack { get; set; }

        [JsonPropertyName("isResourcepack")]
        public bool IsResourcepack { get; set; }
    }
}
