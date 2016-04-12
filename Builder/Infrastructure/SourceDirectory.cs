using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Builder
    {
    [JsonObject(MemberSerialization= MemberSerialization.OptIn)]
    public class SourceDirectory
        {
        [JsonProperty("alias")]
        public string Alias { get; set; }

        [JsonProperty("src", Required= Required.DisallowNull)]
        public string SrcPath { get; set; }

        [JsonProperty("stream")]
        public string Stream { get; set; }

        [JsonProperty("configurations", NullValueHandling=NullValueHandling.Ignore)]
        public IList<Configuration> Configurations { get; set; } = new List<Configuration>();

        [JsonProperty("shellCommands")]
        public string ShellCommands { get; set; }

        [JsonProperty("expanded")]
        public bool IsExpanded { get; set; } = false;

        public SourceDirectory Copy ()
            {
            return new SourceDirectory()
                {
                Alias = Alias,
                SrcPath = SrcPath,
                Stream = Stream,
                ShellCommands = ShellCommands,
                IsExpanded = IsExpanded,
                Configurations = new List<Configuration>(Configurations.Select(c => c.Copy()))
                };
            }
        }

    [JsonObject(MemberSerialization = MemberSerialization.OptIn)]
    public class Configuration
        {
        [JsonProperty("alias")]
        public string Alias { get; set; }
        [JsonProperty("out")]
        public string OutPath { get; set; }
        [JsonProperty("strategy")]
        public string BuildStrategy { get; set; }
        [JsonProperty("release")]
        public bool Release { get; set; } = true;
        [JsonProperty("shellCommands")]
        public string ShellCommands { get; set; }

        [JsonProperty("pinned_parts", NullValueHandling = NullValueHandling.Ignore)]
        public IList<Part> PinnedParts { get; set; } = new List<Part>();

        public Configuration Copy ()
            {
            return new Configuration()
                {
                Alias = Alias,
                OutPath = OutPath,
                BuildStrategy = BuildStrategy,
                Release = Release,
                ShellCommands = ShellCommands,
                PinnedParts = new List<Part>(PinnedParts.Select(c => c.Copy()))
                };
            }
        }

    [JsonObject(MemberSerialization = MemberSerialization.OptIn)]
    public class Part
        {
        [JsonProperty("name")]
        public string Name { get; set; }

        public Part Copy ()
            {
            return new Part()
                {
                Name = Name
                };
            }
        }
    }
