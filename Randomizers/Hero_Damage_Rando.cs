using System;
using System.Collections.Generic;
using System.Linq;

using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;

using HarmonyLib;

using MonoMod.Utils;

using Newtonsoft.Json;

using Smol_Randomizer.Settings;

namespace Smol_Randomizer.Randomizers;

/// <summary>
/// Randomizer for Hero Nail Damage
/// </summary>
internal class Hero_Damage_Rando : Rando_Base
{
    /// <summary>
    /// We make a singleton of this rando
    /// </summary>
    private static readonly Lazy<Hero_Damage_Rando> instance = new(() => new Hero_Damage_Rando());
    /// <summary>
    /// Externally visible instance of this rando
    /// </summary>
    public static Hero_Damage_Rando Instance => instance.Value;

    /// <summary>
    /// The nail damage if based on the game instance
    /// </summary>
    public int saveNailDamageOffset = int.MinValue;
    /// <summary>
    /// The nail damage based on upgrade level
    /// </summary>
    public Dictionary<int, int> nailUpgradeDamages = [];

    private Hero_Damage_Rando()
    {
        InitRandomizer();
    }

    /// <summary>
    /// Initialize Hero Damage Randomizer
    /// </summary>
    private protected override void InitRandomizer()
    {
        RandomizerName = "Hero Damage Randomizer";
        RandomizerDescription = "Randomizes the damage Hornet does.";

        if (!Cute_Rando_Core.RegisterRandomizer(new(
            RandomizerName,
            RandomizerEventType.GameStartup,
            AccessTools.Method(
                typeof(Hero_Damage_Rando),
                nameof(GameStartup)),
            this))) return;

        base.InitRandomizer();
    }

    private protected override void ApplySaveData(Dictionary<string, object> savedData)
    {
        if (savedData.TryGetValue(nameof(nailUpgradeDamages), out object tempDict))
            nailUpgradeDamages.AddRange(JsonConvert.DeserializeObject<Dictionary<int, int>>(tempDict.ToString()));
        if (savedData.TryGetValue(nameof(saveNailDamageOffset), out tempDict))
            saveNailDamageOffset = JsonConvert.DeserializeObject<int>(tempDict.ToString());
    }

    private protected override void SetSaveData(Dictionary<string, object> savedData)
    {
        savedData[nameof(nailUpgradeDamages)] = nailUpgradeDamages;
        savedData[nameof(saveNailDamageOffset)] = saveNailDamageOffset;
    }

    // Unused as we don't need
    private protected override void Register() { }
    private protected override void Unregister() { }

    /// <summary>
    /// Patch the nail damage getter for either the base game, or debug mod if that is loaded.
    /// <para>
    /// Debug mod's patch takes precidence over any patches applied to PlayerData.get_nailDamage for some reason. We need to patch that instead to apply the damage properly.
    /// </para>
    /// </summary>
    private void GameStartup()
    {
        // Roundabout way of poking fingers into DebugMod without requiring it as a dependancy
        Chainloader.PluginInfos.TryGetValue("io.github.hk-speedrunning.debugmod", out PluginInfo DebugMod);

        if (Harmony.GetPatchInfo(AccessTools.Method(typeof(PlayerData), "get_nailDamage"))?.Postfixes?.FirstOrDefault(patch => patch.owner == "io.github.hk-speedrunning.debugmod") != null)
            Cute_Rando_Core.harmony.Patch(AccessTools.Method(DebugMod.Instance.GetType(), "Get_NailDamage"), postfix: new HarmonyMethod(typeof(Hero_Damage_Rando), nameof(DebugModGetNailDamagePostfix)));
        else
            Cute_Rando_Core.harmony.Patch(AccessTools.Method(typeof(PlayerData), "get_nailDamage"), postfix: new HarmonyMethod(typeof(Hero_Damage_Rando), nameof(PlayerDataGetNailDamagePostfix)));
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
        if (!Instance.coreEnableRandomization || __result == 0 || !Instance.PlayerNailDamageRando) return;

        Instance.NailDamage(ref __result);
    }

    /// <summary>
    /// Randomizes the nail's damage depending on the consistancy setting
    /// </summary>
    /// <param name="nailDamage">Reference to the initial nail damage value</param>
    /// <exception cref="NotImplementedException">Thrown if there is an unimplemented consistancy type.</exception>
    private void NailDamage(ref int nailDamage)
    {
        switch (ConsistancySetting)
        {
            case PlayerNailDamageConsistancy.NailUpgradeLevel:
                if (!nailUpgradeDamages.TryGetValue(nailDamage, out int tempDamage))
                {
                    tempDamage = RollDamage();
                    nailUpgradeDamages.Add(nailDamage, tempDamage);
                }

                nailDamage += tempDamage;
                break;
            case PlayerNailDamageConsistancy.PerSave:
                if (saveNailDamageOffset.Equals(int.MinValue))
                {
                    saveNailDamageOffset = RollDamage();
                }

                nailDamage += saveNailDamageOffset;
                break;
            case PlayerNailDamageConsistancy.None:
                nailDamage += RollDamage();
                break;
            default:
                throw new NotImplementedException();

        }

        if (nailDamage <= 0 && PlayerNailDamageMinimum) nailDamage = 1;

        int RollDamage()
        {
            return UnityEngine.Random.Range(PlayerNailDamageShift * (-1), PlayerNailDamageShift);
        }
    }

    private protected override void ResetAllLists()
    {
        nailUpgradeDamages.Clear();
        saveNailDamageOffset = int.MinValue;
    }

    #region Settings
    /// <summary>
    /// Setting for how consistant nail damage should be
    /// </summary>
    public PlayerNailDamageConsistancy ConsistancySetting
    {
        get => consistancySetting.Value;
        internal set => consistancySetting.Value = value;
    }
    private ConfigEntry<PlayerNailDamageConsistancy> consistancySetting;
    /// <summary>
    /// Default choice for nail damage consistancy
    /// </summary>
    public readonly PlayerNailDamageConsistancy defaultConsistancySetting = PlayerNailDamageConsistancy.None;
    /// <summary>
    /// Setting for if nail damage should be randomized
    /// </summary>
    public bool PlayerNailDamageRando
    {
        get => playerNailDamageRando.Value;
        internal set => playerNailDamageRando.Value = value;
    }
    private ConfigEntry<bool> playerNailDamageRando;
    /// <summary>
    /// Default choice for if nail damage should be randomized
    /// </summary>
    public readonly bool defaultPlayerNailDamageRando = false;
    /// <summary>
    /// Setting for if there should be a minimum damage for the nail
    /// </summary>
    public bool PlayerNailDamageMinimum
    {
        get => playerNailDamageMinimum.Value;
        internal set => playerNailDamageMinimum.Value = value;
    }
    private ConfigEntry<bool> playerNailDamageMinimum;
    /// <summary>
    /// Default choice for minimum nail damage
    /// </summary>
    public readonly bool defaultPlayerNailDamageMinimum = true;
    /// <summary>
    /// Setting for the amount we should shift the nail damage
    /// </summary>
    public int PlayerNailDamageShift
    {
        get => playerNailDamageShift.Value;
        internal set => playerNailDamageShift.Value = value;
    }
    private ConfigEntry<int> playerNailDamageShift;
    /// <summary>
    /// Default choice for the nail damage shift
    /// </summary>
    public readonly int defaultPlayerNailDamageShift = 3;

    private protected override void InitSettings()
    {
        ConfigFile config = Settings.Settings.ConfigFile;
        playerNailDamageRando = config.Bind(
            section: RandomizerName,
            key: "Randomizer Hornet Damage",
            defaultValue: defaultPlayerNailDamageRando,
            configDescription: new ConfigDescription(
                description: "Enable/Disable Randomization of Hornet's Damage.",
                tags: new ConfigurationManagerAttributes
                {
                    Order = 3
                }));
        consistancySetting = config.Bind(
             section: RandomizerName,
            key: "Damage Consistancy",
            defaultValue: defaultConsistancySetting,
            configDescription: new ConfigDescription(
                description: "Randomize Hornet's damage in a consistant way.",
                tags: new ConfigurationManagerAttributes
                {
                    Order = 2
                }));
        playerNailDamageShift = config.Bind(
            section: RandomizerName,
            key: "Damage Shift",
            defaultValue: defaultPlayerNailDamageShift,
            configDescription: new ConfigDescription(
                description: "Shifts Hornet's within a range of the set value. Acceptable values range from 0 to 20.",
                acceptableValues: new AcceptableValueRange<int>(0, 20),
                tags: new ConfigurationManagerAttributes
                {
                    Order = 1
                }));
        playerNailDamageMinimum = config.Bind(
            section: RandomizerName,
            key: "Hornet Damage Minimum",
            defaultValue: defaultPlayerNailDamageMinimum,
            configDescription: new ConfigDescription(
                description: "Sets the minimum damage that Hornet can to to 1.",
                tags: new ConfigurationManagerAttributes
                {
                    Order = 0
                }));

        playerNailDamageRando.SettingChanged += SettingMenu.OnRandomizerEnable;
        SettingMenu.UpdateSubMenuColor(playerNailDamageRando);
    }
    #endregion
}

/// <summary>
/// The options for the nail damage consistancy
/// </summary>
internal enum PlayerNailDamageConsistancy
{
    None, // Each swing is different damage
    NailUpgradeLevel, // Each nail upgrade is different damage
    PerSave // Each teir is adjusted by the same amount
}
