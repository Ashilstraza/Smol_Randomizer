using System;
using System.Collections.Generic;
using System.Linq;

using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;

using HarmonyLib;

#if TESTING

using MonoMod.Utils;

using Newtonsoft.Json;

#endif

using Smol_Randomizer.Settings;

namespace Smol_Randomizer.Randomizers;

/// <summary>Randomizer for Hero Nail Damage</summary>
internal class Hero_Damage_Rando : Rando_Base
{
    /// <summary>We make a singleton of this rando</summary>
    private static readonly Lazy<Hero_Damage_Rando> instance = new(() => new Hero_Damage_Rando());

    /// <summary>Externally visible instance of this rando</summary>
    public static Hero_Damage_Rando Instance => instance.Value;

    /// <summary>The nail damage if based on the game instance</summary>
    public int saveNailDamageOffset = int.MinValue;

    /// <summary>The nail damage based on upgrade level</summary>
    public Dictionary<int, int> nailUpgradeDamages = [];

    /// <summary>Constructor for this singleton</summary>
    private Hero_Damage_Rando()
    {
        InitRandomizer();
    }

    protected override void InitRandomizer()
    {
        RandomizerName = "Hornet Damage Randomizer";
        RandomizerDescription = "Randomizes the damage Hornet does.";

        if (!CuteRandoCore.RegisterRandomizer(new(
            RandomizerName,
            RandomizerEventType.GameStartup,
            AccessTools.Method(
                typeof(Hero_Damage_Rando),
                nameof(GameStartup)),
            this))) return;

        base.InitRandomizer();
    }

#if TESTING // Enable Saving Data

    protected override void ApplySaveData(Dictionary<string, object> savedData)
    {
        if (savedData.TryGetValue(nameof(nailUpgradeDamages), out object tempDict))
            nailUpgradeDamages.AddRange(JsonConvert.DeserializeObject<Dictionary<int, int>>(tempDict.ToString()));
        if (savedData.TryGetValue(nameof(saveNailDamageOffset), out tempDict))
            saveNailDamageOffset = JsonConvert.DeserializeObject<int>(tempDict.ToString());
    }

    protected override void SetSaveData(Dictionary<string, object> savedData)
    {
        savedData[nameof(nailUpgradeDamages)] = nailUpgradeDamages;
        savedData[nameof(saveNailDamageOffset)] = saveNailDamageOffset;
    }

    protected override void OnSettingsSaved()
    {
        Dictionary<string, object> savedData = Settings.Settings.SaveData.GetSavedData(RandomizerName);

        savedData[nameof(saveNailDamageOffset)] = saveNailDamageOffset;
    }

#endif

    // Unused as we don't need
    protected override void Register() { }

    protected override void Unregister() { }

    protected override void OnLoaded() { }

    protected override void OnUnload() { }

    /// <summary>
    /// Patch the nail damage getter for either the base game, or debug mod if that is loaded.
    /// <para>
    /// Debug mod's patch takes precidence over any patches applied to PlayerData.get_nailDamage for some reason. We
    /// need to patch that instead to apply the damage properly.
    /// </para>
    /// </summary>
    private void GameStartup()
    {
        // Roundabout way of poking fingers into DebugMod without requiring it as a dependancy
        Chainloader.PluginInfos.TryGetValue("io.github.hk-speedrunning.debugmod", out PluginInfo DebugMod);

        if (Harmony.GetPatchInfo(AccessTools.Method(typeof(PlayerData), "get_nailDamage"))?.Postfixes?.FirstOrDefault(patch => patch.owner == "io.github.hk-speedrunning.debugmod") != null)
            CuteRandoCore.harmony.Patch(
                AccessTools.Method(DebugMod.Instance.GetType(), "Get_NailDamage"),
                postfix: new HarmonyMethod(typeof(Hero_Damage_Rando), nameof(DebugMod_Get_NailDamage_Postfix)));
        else
            CuteRandoCore.harmony.Patch(
                AccessTools.Method(typeof(PlayerData), "get_nailDamage"),
                postfix: new HarmonyMethod(typeof(Hero_Damage_Rando), nameof(PlayerData_Get_NailDamage_Postfix)));
    }

    /// <summary>
    /// Patch that hooks onto the DebugMod's Get_NailDamage postfix since for some reason HarmonyX isn't able to put our
    /// own version after DebugMod's
    /// </summary>
    /// <param name="__result">The to-be returned nail damage amount</param>
    private static void DebugMod_Get_NailDamage_Postfix(ref int __result)
    {
        PlayerData_Get_NailDamage_Postfix(ref __result);
    }

    /// <summary>Patch that hooks get_NailDamage to tweak the nail's damage</summary>
    /// <param name="__result">The to-be returned nail damage amount</param>
    private static void PlayerData_Get_NailDamage_Postfix(ref int __result)
    {
        if (!Instance.coreEnableRandomization || __result == 0 || !Instance.PlayerNailDamageRando) return;

        Instance.NailDamage(ref __result);
    }

    /// <summary>Randomizes the nail's damage depending on the consistancy setting</summary>
    /// <param name="nailDamage">Reference to the initial nail damage value</param>
    /// <exception cref="NotImplementedException">Thrown if there is an unimplemented consistancy type.</exception>
    private void NailDamage(ref int nailDamage)
    {
        switch (ConsistancySetting)
        {
            case PlayerNailDamageConsistancy.NeedleUpgradeLevel:
                if (!nailUpgradeDamages.TryGetValue(nailDamage, out int tempDamage))
                {
                    tempDamage = RollDamage(CuteRandoCore.RNGSeed(nailDamage.ToString()));
                    nailUpgradeDamages.Add(nailDamage, tempDamage);
                }

                nailDamage += tempDamage;
                break;

            case PlayerNailDamageConsistancy.PerSave:
                if (saveNailDamageOffset.Equals(int.MinValue))
                {
                    saveNailDamageOffset = RollDamage(Settings.Settings.SaveData.SaveSeed);
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

        int RollDamage(int seed = int.MinValue)
        {
            return CuteRandoCore.RandomInt(PlayerNailDamageShift * (-1), PlayerNailDamageShift, seed);
        }
    }

    protected override void ResetAllLists()
    {
        nailUpgradeDamages.Clear();
        saveNailDamageOffset = int.MinValue;
    }

    #region Settings

    /// <summary>Setting for how consistant nail damage should be</summary>
    public PlayerNailDamageConsistancy ConsistancySetting
    {
        get => consistancySetting.Value;
        internal set => consistancySetting.Value = value;
    }

    private ConfigEntry<PlayerNailDamageConsistancy> consistancySetting;

    /// <summary>Default choice for nail damage consistancy</summary>
    public const PlayerNailDamageConsistancy defaultConsistancySetting = PlayerNailDamageConsistancy.None;

    /// <summary>Setting for if nail damage should be randomized</summary>
    public bool PlayerNailDamageRando
    {
        get => playerNailDamageRando.Value;
        internal set => playerNailDamageRando.Value = value;
    }

    private ConfigEntry<bool> playerNailDamageRando;

    /// <summary>Default choice for if nail damage should be randomized</summary>
    public const bool defaultPlayerNailDamageRando = false;

    /// <summary>Setting for if there should be a minimum damage for the nail</summary>
    public bool PlayerNailDamageMinimum
    {
        get => playerNailDamageMinimum.Value;
        internal set => playerNailDamageMinimum.Value = value;
    }

    private ConfigEntry<bool> playerNailDamageMinimum;

    /// <summary>Default choice for minimum nail damage</summary>
    public const bool defaultPlayerNailDamageMinimum = true;

    /// <summary>Setting for the amount we should shift the nail damage</summary>
    public int PlayerNailDamageShift
    {
        get => playerNailDamageShift.Value;
        internal set => playerNailDamageShift.Value = value;
    }

    private ConfigEntry<int> playerNailDamageShift;

    /// <summary>Default choice for the nail damage shift</summary>
    public const int defaultPlayerNailDamageShift = 3;

    /// <summary>Acceptable value range for player nail damage</summary>
    public AcceptableValueRange<int> acceptablePlayerNailDamageShift = new(0, 20);

    protected override void InitSettings()
    {
        ConfigFile config = Settings.Settings.ConfigFile;
        playerNailDamageRando = config.Bind(
            section: RandomizerName,
            key: "Randomize Hornets Damage",
            defaultValue: defaultPlayerNailDamageRando,
            configDescription: new ConfigDescription(
                description: "Enable/Disable Randomization of Hornet's Needle Damage.",
                tags: new ConfigurationManagerAttributes
                {
                    Order = 3
                }));
        consistancySetting = config.Bind(
             section: RandomizerName,
            key: "Damage Consistancy",
            defaultValue: defaultConsistancySetting,
            configDescription: new ConfigDescription(
                description: "Randomize Hornet's needle damage in a consistant way.",
                tags: new ConfigurationManagerAttributes
                {
                    Order = 2
                }));
        playerNailDamageShift = config.Bind(
            section: RandomizerName,
            key: "Damage Shift",
            defaultValue: defaultPlayerNailDamageShift,
            configDescription: new ConfigDescription(
                description: "Shifts Hornet's needle damage within a range of the set value.",
                acceptableValues: acceptablePlayerNailDamageShift,
                tags: new ConfigurationManagerAttributes
                {
                    Order = 1
                }));
        playerNailDamageMinimum = config.Bind(
            section: RandomizerName,
            key: "Hornet Damage Minimum",
            defaultValue: defaultPlayerNailDamageMinimum,
            configDescription: new ConfigDescription(
                description: "Sets the minimum damage that Hornet's needle can to to 1.",
                tags: new ConfigurationManagerAttributes
                {
                    Order = 0
                }));

        playerNailDamageShift.SettingChanged += OnSettingsUpdated;

        playerNailDamageRando.SettingChanged += SettingMenu.OnRandomizerEnable;
        SettingMenu.UpdateSubMenuColor(playerNailDamageRando);

        if (playerNailDamageRando.Value)
        {
            Register();
        }
    }

    protected override void OnSettingsUpdated(object sender, EventArgs args)
    {
        ResetAllLists();
    }

    #endregion Settings
}

/// <summary>The options for the nail damage consistancy</summary>
internal enum PlayerNailDamageConsistancy
{
    None, // Each swing is different damage
    NeedleUpgradeLevel, // Each nail upgrade is different damage
    PerSave // Each teir is adjusted by the same amount
}