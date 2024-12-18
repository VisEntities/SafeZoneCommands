﻿/*
 * Copyright (C) 2024 Game4Freak.io
 * This mod is provided under the Game4Freak EULA.
 * Full legal terms can be found at https://game4freak.io/eula/
 */

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System.Collections.Generic;
using System.Linq;

namespace Oxide.Plugins
{
    [Info("Safe Zone Commands", "VisEntities", "1.2.1")]
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

            [JsonProperty("Run Random Command ")]
            public bool RunRandomCommand { get; set; }

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

            if (string.Compare(_config.Version, "1.2.0") < 0)
            {
                foreach (SafeZoneConfig safeZone in _config.SafeZones)
                {
                    safeZone.RunRandomCommand = false;
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
                                Command = "Hello! {PlayerName} here, currently at {MonumentName} in grid {Grid} to recycle some items.",
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
                                Command = "inventory.giveto {PlayerId} scrap 50",
                            }
                        },
                        EnterMessage = "Welcome to {MonumentName}, {PlayerName}!",
                        LeaveMessage = "Goodbye, {PlayerName}. Hope you had a great time at {MonumentName}!"
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
                    if (matchingSafeZone.RunRandomCommand && matchingSafeZone.CommandsToRun.Any())
                    {
                        var randomCommandConfig = matchingSafeZone.CommandsToRun[UnityEngine.Random.Range(0, matchingSafeZone.CommandsToRun.Count)];
                        if (randomCommandConfig.Trigger == CommandTrigger.Enter)
                        {
                            RunCommand(player, randomCommandConfig.Type, randomCommandConfig.Command, matchingSafeZone.MonumentName);
                        }
                    }
                    else
                    {
                        foreach (var commandConfig in matchingSafeZone.CommandsToRun)
                        {
                            if (commandConfig.Trigger == CommandTrigger.Enter)
                            {
                                RunCommand(player, commandConfig.Type, commandConfig.Command, matchingSafeZone.MonumentName);
                            }
                        }
                    }

                    if (!string.IsNullOrEmpty(matchingSafeZone.EnterMessage))
                    {
                        string withPlaceholdersReplaced = ReplacePlaceholders(matchingSafeZone.EnterMessage, player, matchingSafeZone.MonumentName);
                        SendReply(player, withPlaceholdersReplaced);
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
                    if (matchingSafeZone.RunRandomCommand && matchingSafeZone.CommandsToRun.Any())
                    {
                        var randomCommandConfig = matchingSafeZone.CommandsToRun[UnityEngine.Random.Range(0, matchingSafeZone.CommandsToRun.Count)];
                        if (randomCommandConfig.Trigger == CommandTrigger.Leave)
                        {
                            RunCommand(player, randomCommandConfig.Type, randomCommandConfig.Command, matchingSafeZone.MonumentName);
                        }
                    }
                    else
                    {
                        foreach (var commandConfig in matchingSafeZone.CommandsToRun)
                        {
                            if (commandConfig.Trigger == CommandTrigger.Leave)
                            {
                                RunCommand(player, commandConfig.Type, commandConfig.Command, matchingSafeZone.MonumentName);
                            }
                        }
                    }

                    if (!string.IsNullOrEmpty(matchingSafeZone.LeaveMessage))
                    {
                        string withPlaceholdersReplaced = ReplacePlaceholders(matchingSafeZone.LeaveMessage, player, matchingSafeZone.MonumentName);
                        SendReply(player, withPlaceholdersReplaced);
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
                            MessagePlayer(player, Lang.CommandBlocked);
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
            string withPlaceholdersReplaced = ReplacePlaceholders(command, player, monumentName);

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

        #region Placeholder Replacement

        private string ReplacePlaceholders(string input, BasePlayer player, string monumentName)
        {
            string formattedMonumentName = GetMonumentNiceName(monumentName);

            return input
                .Replace("{PlayerId}", player.UserIDString)
                .Replace("{PlayerName}", player.displayName)
                .Replace("{PositionX}", player.transform.position.x.ToString())
                .Replace("{PositionY}", player.transform.position.y.ToString())
                .Replace("{PositionZ}", player.transform.position.z.ToString())
                .Replace("{Grid}", MapHelper.PositionToString(player.transform.position))
                .Replace("{MonumentName}", formattedMonumentName);
        }

        #endregion Placeholder Replacement

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

        private static string GetMessage(BasePlayer player, string messageKey, params object[] args)
        {
            string message = _plugin.lang.GetMessage(messageKey, _plugin, player.UserIDString);

            if (args.Length > 0)
                message = string.Format(message, args);

            return message;
        }

        public static void MessagePlayer(BasePlayer player, string messageKey, params object[] args)
        {
            string message = GetMessage(player, messageKey, args);
            _plugin.SendReply(player, message);
        }

        #endregion Localization
    }
}