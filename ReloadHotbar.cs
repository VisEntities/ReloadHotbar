/*
 * Copyright (C) 2024 Game4Freak.io
 * This mod is provided under the Game4Freak EULA.
 * Full legal terms can be found at https://game4freak.io/eula/
 */

using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Reload Hotbar", "VisEntities", "1.0.0")]
    [Description(" ")]
    public class ReloadHotbar : RustPlugin
    {
        #region Fields

        private static ReloadHotbar _plugin;
        private static Configuration _config;

        #endregion Fields

        #region Configuration

        private class Configuration
        {
            [JsonProperty("Version")]
            public string Version { get; set; }

            [JsonProperty("Reload Chat Command")]
            public string ReloadChatCommand { get; set; }

            [JsonProperty("Unload Chat Command")]
            public string UnloadChatCommand { get; set; }
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

            PrintWarning("Config update complete! Updated from version " + _config.Version + " to " + Version.ToString());
            _config.Version = Version.ToString();
        }

        private Configuration GetDefaultConfig()
        {
            return new Configuration
            {
                Version = Version.ToString(),
                ReloadChatCommand = "reload",
                UnloadChatCommand = "unload"
            };
        }

        #endregion Configuration

        #region Oxide Hooks

        private void Init()
        {
            _plugin = this;
            PermissionUtil.RegisterPermissions();
            cmd.AddChatCommand(_config.ReloadChatCommand, this, nameof(cmdReload));
            cmd.AddChatCommand(_config.UnloadChatCommand, this, nameof(cmdUnload));
        }

        private void Unload()
        {
            _config = null;
            _plugin = null;
        }

        #endregion Oxide Hooks

        #region Permissions

        private static class PermissionUtil
        {
            public const string USE = "reloadhotbar.use";
            private static readonly List<string> _permissions = new List<string>
            {
                USE,
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

        #region Commands

        private void cmdReload(BasePlayer player, string cmd, string[] args)
        {
            if (player == null)
                return;

            if (!PermissionUtil.HasPermission(player, PermissionUtil.USE))
            {
                MessagePlayer(player, Lang.NoPermission);
                return;
            }

            int weaponsReloaded = 0;
            int totalAmmoUsed = 0;
            List<string> details = new List<string>();

            MessagePlayer(player, Lang.Reloading);

            ItemContainer belt = player.inventory.containerBelt;
            if (belt == null || belt.itemList == null)
            {
                MessagePlayer(player, Lang.NoWeapon);
                return;
            }

            foreach (Item item in belt.itemList)
            {
                if (item == null)
                    continue;

                BaseProjectile weapon = item.GetHeldEntity() as BaseProjectile;
                if (weapon == null || weapon.primaryMagazine == null)
                    continue;

                int currentAmmo = weapon.primaryMagazine.contents;
                int capacity = weapon.primaryMagazine.capacity;
                if (currentAmmo >= capacity)
                    continue;

                ItemDefinition ammoDef = weapon.primaryMagazine.ammoType;
                if (ammoDef == null)
                    continue;

                ItemContainer mainInv = player.inventory.containerMain;
                if (mainInv == null || mainInv.itemList == null)
                    continue;

                int ammoAvailable = 0;
                foreach (Item ammoItem in mainInv.itemList)
                {
                    if (ammoItem == null)
                        continue;

                    if (ammoItem.info.shortname.Equals(ammoDef.shortname, StringComparison.OrdinalIgnoreCase))
                        ammoAvailable += ammoItem.amount;
                }

                int needed = capacity - currentAmmo;
                if (ammoAvailable <= 0 || needed <= 0)
                    continue;

                int ammoToAdd = Mathf.Min(needed, ammoAvailable);
                int remainingToAdd = ammoToAdd;

                foreach (Item ammoItem in mainInv.itemList.ToArray())
                {
                    if (ammoItem == null)
                        continue;

                    if (ammoItem.info.shortname.Equals(ammoDef.shortname, StringComparison.OrdinalIgnoreCase))
                    {
                        int take = Mathf.Min(remainingToAdd, ammoItem.amount);
                        remainingToAdd -= take;
                        ammoItem.UseItem(take);
                        if (ammoItem.amount <= 0)
                        {
                            ammoItem.RemoveFromContainer();
                            ammoItem.Remove();
                        }
                        if (remainingToAdd <= 0)
                            break;
                    }
                }

                weapon.primaryMagazine.contents += ammoToAdd;
                weapon.SendNetworkUpdateImmediate();

                weaponsReloaded++;
                totalAmmoUsed += ammoToAdd;

                string weaponName = item.info.displayName.translated;
                details.Add($"- {weaponName} ({ammoDef.shortname}) +{ammoToAdd}");
            }

            player.inventory.SendUpdatedInventory(PlayerInventory.Type.Belt, player.inventory.containerBelt);

            if (weaponsReloaded > 0)
            {
                string detailText = string.Join("\n", details);
                MessagePlayer(player, Lang.SuccessReload, weaponsReloaded, totalAmmoUsed, detailText);
            }
            else
            {
                MessagePlayer(player, Lang.NoWeapon);
            }
        }

        private void cmdUnload(BasePlayer player, string cmd, string[] args)
        {
            if (player == null)
                return;

            if (!PermissionUtil.HasPermission(player, PermissionUtil.USE))
            {
                MessagePlayer(player, Lang.NoPermission);
                return;
            }

            int weaponsUnloaded = 0;
            int totalAmmoUnloaded = 0;
            List<string> details = new List<string>();

            MessagePlayer(player, Lang.Unloading);

            ItemContainer belt = player.inventory.containerBelt;
            if (belt == null || belt.itemList == null)
            {
                MessagePlayer(player, Lang.NoWeapon);
                return;
            }

            foreach (Item item in belt.itemList)
            {
                if (item == null)
                    continue;

                BaseProjectile weapon = item.GetHeldEntity() as BaseProjectile;
                if (weapon == null || weapon.primaryMagazine == null)
                    continue;

                int currentAmmo = weapon.primaryMagazine.contents;
                if (currentAmmo <= 0)
                    continue;

                int ammoRemoved = currentAmmo;
                weapon.UnloadAmmo(item, player);

                weaponsUnloaded++;
                totalAmmoUnloaded += ammoRemoved;
                details.Add($"- {item.info.displayName.translated}: -{ammoRemoved}");
            }

            player.inventory.SendUpdatedInventory(PlayerInventory.Type.Belt, player.inventory.containerBelt);

            if (weaponsUnloaded > 0)
            {
                string detailText = string.Join("\n", details);
                MessagePlayer(player, Lang.SuccessUnload, weaponsUnloaded, totalAmmoUnloaded, detailText);
            }
            else
            {
                MessagePlayer(player, Lang.NoWeapon);
            }
        }

        #endregion Commands

        #region Localization

        private class Lang
        {
            public const string NoPermission = "NoPermission";
            public const string Reloading = "Reloading";
            public const string NoWeapon = "NoWeapon";
            public const string SuccessReload = "Success";
            public const string NoAmmo = "NoAmmo";
            public const string Unloading = "Unloading";
            public const string SuccessUnload = "SuccessUnload";
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                [Lang.NoPermission] = "You do not have permission to use this command.",
                [Lang.Reloading] = "Reloading your hotbar weapons...",
                [Lang.NoWeapon] = "No weapons in your hotbar need reloading.",
                [Lang.SuccessReload] = "Reloaded {0} weapons using a total of {1} ammo:\n{2}",
                [Lang.NoAmmo] = "You have no matching ammo in your inventory to reload your weapons.",
                [Lang.Unloading] = "Unloading your hotbar weapons...",
                [Lang.SuccessUnload] = "Unloaded {0} weapons, removing a total of {1} ammo:\n{2}",
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