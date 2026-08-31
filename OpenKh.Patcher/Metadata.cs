using OpenKh.Kh2;
using SharpYaml;
using SharpYaml.Serialization;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace OpenKh.Patcher
{
    [YamlSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, DefaultIgnoreCondition = YamlIgnoreCondition.WhenWritingDefault)]
    public class Metadata
    {
        public class Dependency
        {
            public string Name { get; set; }
        }

        public string Title { get; set; }
        public string OriginalAuthor { get; set; }
        public string Description { get; set; }
         public string Game { get; set; }
        public int Specifications { get; set; }
        public List<Dependency>? Dependencies { get; set; }
        public bool IsCollection { get; set; }
        public List<string> CollectionGames { get; set; }
        public List<AssetFile> Assets { get; set; }

        [YamlIgnore]
        public bool IsValid = true;

        public static Metadata Read(string fileName)
        {
            var _fetchYamlRaw = File.ReadAllText(fileName);

            // Replace all back slashes as Windows supports forward slashes but Linux doesn't support back slashes.
            _fetchYamlRaw = _fetchYamlRaw.Replace('\\', '/');

            try { return YamlSerializer.Deserialize<Metadata>(_fetchYamlRaw, new YamlSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }); }

            catch (SharpYaml.YamlException ex)
            {
                // Handle YAML parsing errors
                Debug.WriteLine($"Error deserializing YAML: {ex.Message}");

                var _fetchTitle = string.Empty;
                var _fetchMatch = Regex.Match(_fetchYamlRaw, @"(?<=title:).*");

                if (_fetchMatch.Success)
                    _fetchTitle = _fetchMatch.Value.Trim();

                var metadata = new Metadata
                {
                    Title = _fetchTitle,
                    IsValid = false
                };

                return metadata; // Return modified metadata indicating failure
            }

            catch (Exception ex)
            {
                // Handle other unexpected errors
                Debug.WriteLine($"Unexpected error: {ex.Message}");
                throw; // Rethrow other exceptions for further investigation
            }
        }

        public void Write(Stream stream)
        {
            using (var writer = new StreamWriter(stream))
                YamlSerializer.Serialize(writer, this, new YamlSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, DefaultIgnoreCondition = YamlIgnoreCondition.WhenWritingDefault });
        }
        public override string ToString() =>
        YamlSerializer.Serialize(this, new YamlSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, DefaultIgnoreCondition = YamlIgnoreCondition.WhenWritingDefault });
    }

    public class AssetFile
    {
        public string Name { get; set; }

        /// <summary>
        /// "areadatascript"
        /// "bdscript"
        /// "binarc"
        /// "copy"
        /// "imgd"
        /// "imgz"
        /// "kh1ardresource"
        /// "kh2msg"
        /// "listpatch"
        /// "spawnpoint"
        /// "synthpatch"
        /// </summary>
        public string Method { get; set; }

        /// <summary>
        /// null
        /// ""
        /// "both"
        /// "pc"
        /// "ps2"
        /// </summary>
        public string Platform { get; set; }

        /// <summary>
        /// (null)
        /// bbs_first
        /// bbs_fourth
        /// bbs_second
        /// bbs_third
        /// kh1_fifth
        /// kh1_first
        /// kh1_fourth
        /// kh1_second
        /// kh1_third
        /// kh2_fifth
        /// kh2_first
        /// kh2_fourth
        /// kh2_second
        /// kh2_sixth
        /// kh2_third
        /// kh3d_first
        /// kh3d_fourth
        /// kh3d_second
        /// kh3d_third
        /// Recom
        /// Theater
        /// </summary>
        public string Package { get; set; }
        public List<Multi> Multi { get; set; }
        public List<AssetFile> Source { get; set; }

        public bool Required { get; set; }

        /// <summary>
        /// "areadatascript"
        /// "areadataspawn"
        /// "atkp"
        /// "bdx"
        /// "Binary"
        /// "bons"
        /// "cmd"
        /// "condition"
        /// "enmp"
        /// "fmab"
        /// "fmlv"
        /// "imgd"
        /// "imgz"
        /// "internal"
        /// "item"
        /// "jigsaw"
        /// "level"
        /// "libretto"
        /// "list"
        /// "localset"
        /// "lvup"
        /// "magc"
        /// "memt"
        /// "objentry"
        /// "place"
        /// "plrp"
        /// "przt"
        /// "recipe"
        /// "sklt"
        /// "soundinfo"
        /// "Synthesis"
        /// "trsr"
        /// "vtbl"
        /// </summary>
        public string Type { get; set; }
        public Bar.MotionsetType MotionsetType { get; set; }
        public string Language { get; set; }
        public bool IsSwizzled { get; set; }
        public int Index { get; set; }
        public string Game { get; set; }
        public bool CollectionOptional { get; set; }
    }

    public class Multi
    {
        public string Name { get; set; }
    }
}
