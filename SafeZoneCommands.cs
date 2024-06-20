using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("Safe Zone Commands", "VisEntities", "1.1.0")]
    [Description("Execute and block commands in safe zones.")]
    public class SafeZoneCommands : RustPlugin
    {
        #region Fields

        private static SafeZoneCommands _plugin;
        private static Configuration _config;

        #endregion Fields

        #region Configuration

        private class Configuration
        {
            [JsonProperty("Version")]
            public string Version { get; set; }

            [JsonProperty("Safe Zones")]
            public List<SafeZoneConfig> SafeZones { get; set; }
        }

        private class SafeZoneConfig
        {
            [JsonProperty("Monument Name")]
            public string MonumentName { get; set; }

            [JsonProperty("Blacklisted Commands")]
            public List<string> BlacklistedCommands { get; set; }

            [JsonProperty("Commands To Run")]
            public List<CommandConfig> CommandsToRun { get; set; }

            [JsonProperty("Enter Message")]
            public string EnterMessage { get; set; }

            [JsonProperty("Leave Message")]
            public string LeaveMessage { get; set; }
        }

        private class CommandConfig
        {
            [JsonProperty("Type")]
            [JsonConverter(typeof(StringEnumConverter))]
            public CommandType Type { get; set; }

            [JsonProperty("Trigger")]
            [JsonConverter(typeof(StringEnumConverter))]
            public CommandTrigger Trigger { get; set; }

            [JsonProperty("Command")]
            public string Command { get; set; }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            _config = Config.ReadObject<Configuration>();

            if (string.Compare(_config.Version, Version.ToString()) < 0)
                UpdateConfig();

            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            _config = GetDefaultConfig();
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(_config, true);
        }

        private void UpdateConfig()
        {
            PrintWarning("Config changes detected! Updating...");

            Configuration defaultConfig = GetDefaultConfig();

            if (string.Compare(_config.Version, "1.0.0") < 0)
                _config = defaultConfig;

            if (string.Compare(_config.Version, "1.1.0") < 0)
            {
                foreach (SafeZoneConfig safeZone in _config.SafeZones)
                {
                    safeZone.EnterMessage = "Welcome to {monumentName}, {playerName}!";
                    safeZone.LeaveMessage = "Goodbye, {playerName}. Hope you had a great time at {monumentName}!";
                }
            }

            PrintWarning("Config update complete! Updated from version " + _config.Version + " to " + Version.ToString());
            _config.Version = Version.ToString();
        }

        private Configuration GetDefaultConfig()
        {
            return new Configuration
            {
                Version = Version.ToString(),
                SafeZones = new List<SafeZoneConfig>
                {
                    new SafeZoneConfig
                    {
                        MonumentName = "compound",
                        BlacklistedCommands = new List<string>
                        {
                            "kit",
                            "tp"
                        },
                        CommandsToRun = new List<CommandConfig>
                        {
                            new CommandConfig
                            {
                                Type = CommandType.Chat,
                                Trigger = CommandTrigger.Enter,
                                Command = "Hello! {playerName} here, currently at {monumentName} in grid {grid} to recycle some items.",
                            },
                            new CommandConfig
                            {
                                Type = CommandType.Client,
                                Trigger = CommandTrigger.Leave,
                                Command = "heli.calltome"
                            },
                            new CommandConfig
                            {
                                Type = CommandType.Server,
                                Trigger = CommandTrigger.Enter,
                                Command = "inventory.giveto {playerId} scrap 50",
                            }
                        },
                        EnterMessage = "Welcome to {monumentName}, {playerName}!",
                        LeaveMessage = "Goodbye, {playerName}. Hope you had a great time at {monumentName}!"
                    }
                }
            };
        }

        #endregion Configuration

        #region Oxide Hooks

        private void Init()
        {
            _plugin = this;
            PermissionUtil.RegisterPermissions();
        }

        private void Unload()
        {
            _config = null;
            _plugin = null;
        }

        private void OnEntityEnter(TriggerSafeZone safeZoneTrigger, BasePlayer player)
        {
            if (player != null && !player.IsNpc && !player.IsHostile())
            {
                string monumentPrefabName = safeZoneTrigger.transform.parent.name;
                SafeZoneConfig matchingSafeZone = null;

                foreach (SafeZoneConfig safeZone in _config.SafeZones)
                {
                    if (!string.IsNullOrEmpty(safeZone.MonumentName) && monumentPrefabName.Contains(safeZone.MonumentName))
                    {
                        matchingSafeZone = safeZone;
                        break;
                    }
                }

                if (matchingSafeZone != null)
                {
                    foreach (var commandConfig in matchingSafeZone.CommandsToRun)
                    {
                        if (commandConfig.Trigger == CommandTrigger.Enter)
                        {
                            RunCommand(player, commandConfig.Type, commandConfig.Command, matchingSafeZone.MonumentName);
                        }
                    }

                    if (!string.IsNullOrEmpty(matchingSafeZone.EnterMessage))
                    {
                        SendFormattedMessage(player, matchingSafeZone.EnterMessage, matchingSafeZone.MonumentName);
                    }
                }
            }
        }

        private void OnEntityLeave(TriggerSafeZone safeZoneTrigger, BasePlayer player)
        {
            if (player != null && !player.IsNpc && !player.IsHostile())
            {
                string monumentPrefabName = safeZoneTrigger.transform.parent.name;
                SafeZoneConfig matchingSafeZone = null;

                foreach (SafeZoneConfig safeZone in _config.SafeZones)
                {
                    if (!string.IsNullOrEmpty(safeZone.MonumentName) && monumentPrefabName.Contains(safeZone.MonumentName))
                    {
                        matchingSafeZone = safeZone;
                        break;
                    }
                }

                if (matchingSafeZone != null)
                {
                    foreach (var commandConfig in matchingSafeZone.CommandsToRun)
                    {
                        if (commandConfig.Trigger == CommandTrigger.Leave)
                        {
                            RunCommand(player, commandConfig.Type, commandConfig.Command, matchingSafeZone.MonumentName);
                        }
                    }

                    if (!string.IsNullOrEmpty(matchingSafeZone.LeaveMessage))
                    {
                        SendFormattedMessage(player, matchingSafeZone.LeaveMessage, matchingSafeZone.MonumentName);
                    }
                }
            }
        }

        private object OnPlayerCommand(BasePlayer player, string command, string[] args)
        {
            if (player == null || player.IsAdmin || !player.InSafeZone() || PermissionUtil.HasPermission(player, PermissionUtil.IGNORE))
                return null;
            
            string commandLowercased = command.ToLower();

            foreach (MonumentInfo monument in TerrainMeta.Path.Monuments)
            {
                foreach (SafeZoneConfig safeZone in _config.SafeZones)
                {
                    if (monument.name.Contains(safeZone.MonumentName) && monument.IsInBounds(player.transform.position))
                    {
                        if (safeZone.BlacklistedCommands.Contains(commandLowercased))
                        {
                            SendMessage(player, Lang.CommandBlocked);
                            return true;
                        }
                    }
                }
            }

            return null;
        }

        #endregion Oxide Hooks

        #region Monument Name Formatting

        private string GetMonumentNiceName(string monumentName)
        {
            if (string.IsNullOrEmpty(monumentName))
                return string.Empty;

            string formattedName = System.Threading.Thread.CurrentThread.CurrentCulture.TextInfo.ToTitleCase(monumentName.Replace('_', ' '));
            return formattedName;
        }

        #endregion Monument Name Formatting

        #region Enter and Leave Message Formatting

        private void SendFormattedMessage(BasePlayer player, string message, string monumentName)
        {
            string formattedMonumentName = GetMonumentNiceName(monumentName);

            string withPlaceholdersReplaced = message
                .Replace("{playerId}", player.UserIDString)
                .Replace("{playerName}", player.displayName)
                .Replace("{positionX}", player.transform.position.x.ToString())
                .Replace("{positionY}", player.transform.position.y.ToString())
                .Replace("{positionZ}", player.transform.position.z.ToString())
                .Replace("{grid}", PhoneController.PositionToGridCoord(player.transform.position))
                .Replace("{monumentName}", formattedMonumentName);

            SendReply(player, withPlaceholdersReplaced);
        }

        #endregion Enter and Leave Message Formatting

        #region Command Execution

        private enum CommandTrigger
        {
            Enter,
            Leave
        }

        private enum CommandType
        {
            Chat,
            Server,
            Client
        }

        private void RunCommand(BasePlayer player, CommandType type, string command, string monumentName)
        {
            string formattedMonumentName = GetMonumentNiceName(monumentName);

            string withPlaceholdersReplaced = command
                .Replace("{playerId}", player.UserIDString)
                .Replace("{playerName}", player.displayName)
                .Replace("{positionX}", player.transform.position.x.ToString())
                .Replace("{positionY}", player.transform.position.y.ToString())
                .Replace("{positionZ}", player.transform.position.z.ToString())
                .Replace("{grid}", PhoneController.PositionToGridCoord(player.transform.position))
                .Replace("{monumentName}", formattedMonumentName);

            if (type == CommandType.Chat)
            {
                player.Command(string.Format("chat.say \"{0}\"", withPlaceholdersReplaced));
            }
            else if (type == CommandType.Client)
            {
                player.Command(withPlaceholdersReplaced);
            }
            else if (type == CommandType.Server)
            {
                Server.Command(withPlaceholdersReplaced);
            }
        }

        #endregion Command Execution

        #region Permissions

        private static class PermissionUtil
        {
            public const string IGNORE = "safezonecommands.ignore";
            private static readonly List<string> _permissions = new List<string>
            {
                IGNORE,
            };

            public static void RegisterPermissions()
            {
                foreach (var permission in _permissions)
                {
                    _plugin.permission.RegisterPermission(permission, _plugin);
                }
            }

            public static bool HasPermission(BasePlayer player, string permissionName)
            {
                return _plugin.permission.UserHasPermission(player.UserIDString, permissionName);
            }
        }

        #endregion Permissions

        #region Localization

        private class Lang
        {
            public const string CommandBlocked = "CommandBlocked";
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                [Lang.CommandBlocked] = "You cannot use this command in the safe zone.",
            }, this, "en");
        }

        private void SendMessage(BasePlayer player, string messageKey, params object[] args)
        {
            string message = lang.GetMessage(messageKey, this, player.UserIDString);
            if (args.Length > 0)
                message = string.Format(message, args);

            SendReply(player, message);
        }

        #endregion Localization
    }
}