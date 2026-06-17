using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Cute_Randomizer.Randomizers
{
    /// <summary>
    /// Randomizer for Hero Nail Damage
    /// </summary>
    internal class Hero_Damage_Rando
    {
        /// <summary>
        /// The nail damage if based on the game instance
        /// </summary>
        public static int gameNailDamageOffset = int.MinValue;
        /// <summary>
        /// The nail damage based on upgrade level
        /// </summary>
        public static Dictionary<int, int> nailUpgradeDamages = [];
        /// <summary>
        /// If the core of the randomizer is enabled
        /// </summary>
        private static bool coreEnableRandomization = false;

        #region Randomizer_Info
        /// <summary>
        /// Used for registering this randomizer in the core for when the game starts up
        /// </summary>
        private static readonly Randomizer_Info randomizerGameStartup = new(
            "Hero Damage Randomizer",
            RandomizerEventType.GameStartup,
            AccessTools.Method(
                typeof(Hero_Damage_Rando),
                nameof(GameStartup)));
        #endregion

        /// <summary>
        /// Initialize Hero Damage Randomizer
        /// </summary>
        internal static void InitRandomizer()
        {
            if (!Cute_Rando_Core.RegisterRandomizer(randomizerGameStartup)) return;

            InitSettings();

            coreEnableRandomization = Settings.Settings.EnableRandomizer;
        }

        /// <summary>
        /// Patch the nail damage getter for either the base game, or debug mod if that is loaded.
        /// <para>
        /// Debug mod's patch takes precidence over any patches applied to PlayerData.get_nailDamage. We need to patch that instead to apply the damage properly.
        /// </para>
        /// </summary>
        private static void GameStartup()
        {
            // Roundabout way of poking fingers into DebugMod without requiring it as a dependancy
            Chainloader.PluginInfos.TryGetValue("io.github.hk-speedrunning.debugmod", out PluginInfo DebugMod);

            if (Harmony.GetPatchInfo(AccessTools.Method(typeof(PlayerData), "get_nailDamage"))?.Postfixes?.FirstOrDefault(patch => patch.owner == "io.github.hk-speedrunning.debugmod") != null)
            {
                Cute_Rando_Core.harmony.Patch(AccessTools.Method(DebugMod.Instance.GetType(), "Get_NailDamage"), postfix: new HarmonyMethod(typeof(Hero_Damage_Rando), nameof(DebugModGetNailDamagePostfix)));
            }
            else
            {
                Cute_Rando_Core.harmony.Patch(AccessTools.Method(typeof(PlayerData), "get_nailDamage"), postfix: new HarmonyMethod(typeof(Hero_Damage_Rando), nameof(PlayerDataGetNailDamagePostfix)));
            }
        }

        /// <summary>
        /// Patch that hooks onto the DebugMod's Get_NailDamage postfix since for some reason Harmony isn't able to put our own version after DebugMod's
        /// </summary>
        /// <param name="__result">The to-be returned nail damage amount</param>
        private static void DebugModGetNailDamagePostfix(ref int __result)
        {
            PlayerDataGetNailDamagePostfix(ref __result);
        }

        /// <summary>
        /// Patch that hooks get_NailDamage to tweak the nail's damage
        /// </summary>
        /// <param name="__result">The to-be returned nail damage amount</param>
        private static void PlayerDataGetNailDamagePostfix(ref int __result)
        {
            if (!coreEnableRandomization || __result == 0 || !PlayerNailDamageRando) return;

            __result = NailDamage(__result);
            if (__result <= 0 && PlayerNailDamageMinimum) __result = 1;
        }

        /// <summary>
        /// Randomizes the nail's damage depending on the consistancy setting
        /// </summary>
        /// <param name="nailDamage">The initial nail damage value</param>
        /// <returns>The new nail damage</returns>
        /// <exception cref="NotImplementedException">Thrown if there is an unimplemented consistancy type.</exception>
        private static int NailDamage(int nailDamage)
        {
            switch (ConsistancySetting)
            {
                case PlayerNailDamageConsistancy.NailUpgrade:
                    if (!nailUpgradeDamages.TryGetValue(nailDamage, out int tempDamage))
                    {
                        tempDamage = RollDamage();
                        nailUpgradeDamages.Add(nailDamage, tempDamage);
                    }
                    return nailDamage + tempDamage;
                case PlayerNailDamageConsistancy.Game:
                    if (gameNailDamageOffset.Equals(int.MinValue)) gameNailDamageOffset = RollDamage();
                    return nailDamage + gameNailDamageOffset;
                case PlayerNailDamageConsistancy.None:
                    return nailDamage + RollDamage();
                default:
                    throw new NotImplementedException();
            }

            static int RollDamage()
            {
                return UnityEngine.Random.Range(PlayerNailDamageShift * (-1), PlayerNailDamageShift);
            }
        }

        /// <summary>
        /// Reset the saved damages
        /// </summary>
        private static void ResetDamages()
        {
            nailUpgradeDamages.Clear();
            gameNailDamageOffset = int.MinValue;
        }

        #region Settings
        /// <summary>
        /// Setting for how consistant nail damage should be
        /// </summary>
        public static PlayerNailDamageConsistancy ConsistancySetting
        {
            get => consistancySetting.Value;
            internal set => consistancySetting.Value = value;
        }
        private static ConfigEntry<PlayerNailDamageConsistancy> consistancySetting;
        /// <summary>
        /// Default choice for nail damage consistancy
        /// </summary>
        public static readonly PlayerNailDamageConsistancy defaultConsistancySetting = PlayerNailDamageConsistancy.None;
        /// <summary>
        /// Setting for if nail damage should be randomized
        /// </summary>
        public static bool PlayerNailDamageRando
        {
            get => playerNailDamageRando.Value;
            internal set => playerNailDamageRando.Value = value;
        }
        private static ConfigEntry<bool> playerNailDamageRando;
        /// <summary>
        /// Default choice for if nail damage should be randomized
        /// </summary>
        public static readonly bool defaultPlayerNailDamageRando = false;
        /// <summary>
        /// Setting for if there should be a minimum damage for the nail
        /// </summary>
        public static bool PlayerNailDamageMinimum
        {
            get => playerNailDamageMinimum.Value;
            internal set => playerNailDamageMinimum.Value = value;
        }
        private static ConfigEntry<bool> playerNailDamageMinimum;
        /// <summary>
        /// Default choice for minimum nail damage
        /// </summary>
        public static readonly bool defaultPlayerNailDamageMinimum = true;
        /// <summary>
        /// Setting for the amount we should shift the nail damage
        /// </summary>
        public static int PlayerNailDamageShift
        {
            get => playerNailDamageShift.Value;
            internal set => playerNailDamageShift.Value = value;
        }
        private static ConfigEntry<int> playerNailDamageShift;
        /// <summary>
        /// Default choice for the nail damage shift
        /// </summary>
        public static readonly int defaultPlayerNailDamageShift = 3;

        /// <summary>
        /// Config Section Name
        /// </summary>
        private static readonly string configSection = "Hornet Damage";

        /// <summary>
        /// Add the hero damage randomizer's settings into the core
        /// </summary>
        private static void InitSettings()
        {
            var config = Settings.Settings.ConfigFile;
            playerNailDamageRando = config.Bind(
                section: configSection,
                key: "Hornet Damage Randomizer",
                defaultValue: defaultPlayerNailDamageRando,
                configDescription: new ConfigDescription(
                    description: "Enable/Disable Randomization of Hornet's Damage.",
                    tags: new ConfigurationManagerAttributes
                    {
                        Order = 3
                    }));
            playerNailDamageShift = config.Bind(
                section: configSection,
                key: "Hornet Damage Shift",
                defaultValue: defaultPlayerNailDamageShift,
                configDescription: new ConfigDescription(
                    description: "Shifts Hornet's damage up or down within a set value around her normal needle upgrade value. Acceptable values range from 0 to 20.",
                    acceptableValues: new AcceptableValueRange<int>(0, 20),
                    tags: new ConfigurationManagerAttributes
                    {
                        Order = 2
                    }));
            playerNailDamageMinimum = config.Bind(
                section: configSection,
                key: "Hornet Damage Minimum",
                defaultValue: defaultPlayerNailDamageMinimum,
                configDescription: new ConfigDescription(
                    description: "Sets the minimum damage that Hornet can to to 1.",
                    tags: new ConfigurationManagerAttributes
                    {
                        Order = 1
                    }));
            consistancySetting = config.Bind(
                 section: configSection,
                key: "Hornet Damage Randomizer Consistancy",
                defaultValue: defaultConsistancySetting,
                configDescription: new ConfigDescription(
                    description: "Randomize Hornet's damage in a consistant way.",
                    tags: new ConfigurationManagerAttributes
                    {
                        Order = 0
                    }));

            Settings.Settings.enableRandomizer.SettingChanged += RandoCoreSetting;

            consistancySetting.SettingChanged += OnNailDamageChange;
            playerNailDamageShift.SettingChanged += OnNailDamageChange;
        }

        /// <summary>
        /// Event hook for when the core enable setting for the randomizer is changed
        /// </summary>
        /// <param name="sender">?</param>
        /// <param name="args">The setting that was changed</param>
        private static void RandoCoreSetting(object sender, EventArgs args)
        {
            coreEnableRandomization = (bool)((SettingChangedEventArgs)args).ChangedSetting.BoxedValue;
        }

        /// <summary>
        /// Reset the saved damage values when the settings changed
        /// </summary>
        /// <param name="sender">?</param>
        /// <param name="args">The setting that was changed</param>
        private static void OnNailDamageChange(object sender, EventArgs args)
        {
            ResetDamages();
        }
        #endregion
    }

    /// <summary>
    /// The options for the nail damage consistancy
    /// </summary>
    internal enum PlayerNailDamageConsistancy
    {
        None, // Each swing is different damage
        NailUpgrade, // Each nail upgrade is different damage
        Game // Each teir is adjusted by the same amount
    }
}
