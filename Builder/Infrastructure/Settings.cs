using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Builder
    {
    [JsonObject(MemberSerialization.OptIn)]
    public class Settings
        {
        [JsonProperty("close_to_tray")]
        public bool CloseToTray { get; set; } = false;

        [JsonProperty("start_in_tray")]
        public bool StartInTray { get; set; } = false;

        [JsonProperty("theme")]
        [JsonConverter(typeof(StringEnumConverter))]
        public Theme Theme { get; set; } = Theme.Latest;

        [JsonProperty("tccUsage")]
        [JsonConverter(typeof(StringEnumConverter))]
        public TCCLeUsage TCCLeUsage { get; set; } = TCCLeUsage.Automatic;

        [JsonProperty("tccLePath")]
        public string TCCLePath { get; set; }

        [JsonProperty("shellCommands")]
        public string ShellCommands { get; set; }

        public Settings Copy()
            {
            return new Settings()
                {
                CloseToTray = CloseToTray,
                StartInTray = StartInTray,
                Theme = Theme,
                TCCLeUsage = TCCLeUsage,
                ShellCommands = ShellCommands,
                TCCLePath = TCCLePath
                };
            }
        }

    public enum TCCLeUsage
        {
        Disabled,
        Automatic,
        Enabled
        }
    }
