using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

using BepInEx;
using BepInEx.Logging;

using GlobalEnums;

using HarmonyLib;

using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;

using Newtonsoft.Json;

using Silksong.DataManager;
using Silksong.ModMenu.Plugin;
using Silksong.ModMenu.Screens;

using Smol_Randomizer.Patchers.Enemy;
using Smol_Randomizer.Patchers.Scene;
using Smol_Randomizer.Randomizers;
using Smol_Randomizer.Settings;

#if TESTING

using UnityEngine;

#endif

using UnityEngine.SceneManagement;

namespace Smol_Randomizer;

[BepInDependency("org.silksong-modding.modmenu")]
[BepInDependency(DataManagerPlugin.Id)]
[BepInPlugin(GUID, MODNAME, VERSION)]
public class CuteRandoCore : BaseUnityPlugin, IModMenuInterface, IModMenuCustomMenu, IRawSaveDataMod
{
    /// <summary>Mod ID</summary>
    public const string GUID = "ashilstraza.randomizer.cute";

    /// <summary>Mod Name</summary>
    public const string MODNAME = "Smol Randomizer";

    /// <summary>Mod Version</summary>
    public const string VERSION = "0.1.2";

    /// <summary>If we should update the active limit regions after a scene load</summary>
    private bool updateActiveLimitRegions = false;

    /// <summary>If we should update on next frame</summary>
    private bool updateOnFirstSceneFrame = false;

    /// <summary>The current scene loaded</summary>
    private Scene currentScene;

    /// <summary>List of all registered randomizer names</summary>
    private static readonly HashSet<string> allRandomizers = [];

    /// <summary>List of all registered randomizer actions</summary>
    private static readonly HashSet<Randomizer_Info> allRandomizerActions = [];

    /// <summary>Mod's directory</summary>
    public static string DataLocation { get; private set; } = "";

    /// <summary>If the mod has save data.</summary>
    public bool HasSaveData => Settings.Settings.SaveData != null;

    /// <summary>If we are testing new things</summary>
    public static bool Testing
    {
        get => testing;
        internal set => testing = value;
    }

    private static bool testing = false;

    /// <summary>If we actually want to randomize</summary>
    internal static bool randomize = true;

    /// <summary>Our reference for harmony</summary>
    internal static readonly Harmony harmony = new(GUID);

    /// <summary>The ModMenu settings menu window</summary>
    internal static SettingMenu cuteRandomizerSettingWindow;

    /// <summary>The supplier of RNG when no seed is wanted</summary>
    internal static System.Random noSeedRNG = new();

    /// <summary>Reference to our log source</summary>
    internal static ManualLogSource Log;

    /// <summary>If we have patched the HealthManager's OnEnable</summary>
    private static bool patchedHealthManager = false;

    /// <summary>If we have patched the DamageHero's OnEnable</summary>
    private static bool patchedDamageHero = false;

    /// <summary>Dictionary containing the randomizers that want to update the active limit regions</summary>
    private static readonly Dictionary<string, Action<ICurrencyLimitRegion>> activeLimitRegions = [];

    /// <summary>Dictionary containing the randomizers that want to update on scene load</summary>
    private static readonly Dictionary<string, Action<Scene, LoadSceneMode>> activeOnSceneLoad = [];

    /// <summary>Dictionary containing the randomizers that want to update on game startup</summary>
    private static readonly Dictionary<string, Action> activeGameStartup = [];

    /// <summary>Dictionary containing the randomizers that want to update on game shutdown</summary>
    private static readonly Dictionary<string, Action> activeGameShutdown = [];

    /// <summary>Dictionary containing the randomizers that want to update on first frame of a new scene</summary>
    private static readonly Dictionary<string, Action<Scene>> activeOnFirstSceneFrame = [];

    /// <summary>Dictionary containing the randomizers that want to update on unload</summary>
    private static readonly Dictionary<string, Action> activeOnUnload = [];

    /// <summary>Dictionary containing the randomizers that want to update when an enemy is enabled</summary>
    private static readonly Dictionary<string, Action<HealthManager>> activeEnemy = [];

    /// <summary>Dictionary containing the randomizers that want to update when the hero is damaged</summary>
    private static readonly Dictionary<string, Action<DamageHero, HealthManager>> activeHeroDamager = [];

    /// <summary>Dictionary containing the descriptions of all the randomizers</summary>
    private static readonly Dictionary<string, string> randomizerDescriptions = [];

    /// <summary>On Startup (no window visible)</summary>
    private void Awake()
    {
        TrySetDataLocation();

        Settings.Settings.Init(Config);

        Log = Logger;

        // Access the instance to initialize each randomizer.
        _ = Enemy_Currency_Rando.Instance;
        _ = World_Currency_Drop_Rando.Instance;
        _ = Enemy_Health_Rando.Instance;
        _ = Enemy_Damage_Rando.Instance;
        _ = Hero_Damage_Rando.Instance;
        _ = Enemy_Size_Rando.Instance;
        _ = Hero_Size_Rando.Instance;
    }

    /// <summary>On Loading (window visible)</summary>
    private void Start()
    {
        DebugDrawing.UnityDebugPatches.RegisterHarmonyPatches();

        TrySetDataLocation();

        SceneManager.sceneLoaded += OnSceneLoaded;

        foreach (KeyValuePair<string, Action> rando in activeGameStartup)
            rando.Value();
    }

    /// <summary>Grabs the data location if it is available</summary>
    /// <returns>True if the DataLocation field is set</returns>
    private bool TrySetDataLocation()
    {
        if (string.IsNullOrEmpty(DataLocation) && !string.IsNullOrEmpty(Info.Location))
        {
            DataLocation = Info.Location.TrimEnd("\\\\Smol_Randomizer.dll".ToCharArray()) + "Smol_Randomizer";
            return true;
        }
        else if (!string.IsNullOrEmpty(DataLocation))
            return true;

        return false;
    }

    /// <summary>On Frame Update</summary>
    private void Update()
    {
        if (GameManager.SilentInstance == null) return;

        if (!randomize || (GameManager.instance.sm != null ? GameManager.instance.sm.sceneType : null) != SceneType.GAMEPLAY) return;

        if (updateActiveLimitRegions)
        {
            foreach (ICurrencyLimitRegion region in (HashSet<ICurrencyLimitRegion>)Traverse.Create<CurrencyObjectLimitRegion>().Field("_activeRegions").GetValue())
            {
                foreach (KeyValuePair<string, Action<ICurrencyLimitRegion>> randomizer in activeLimitRegions)
                    randomizer.Value(region);
            }

            updateActiveLimitRegions = false;
        }

        if (updateOnFirstSceneFrame)
        {
            foreach (KeyValuePair<string, Action<Scene>> randomizer in activeOnFirstSceneFrame)
                randomizer.Value(currentScene);

            EnemyFSMPatches.OnFirstFrame();

            updateOnFirstSceneFrame = false;
        }
#if TESTING
        if (Input.GetKeyDown(KeyCode.F11))
        {
            Smol_Randomizer.Patchers.External_Patches.LoadPatches();
        }
#endif
    }

    /// <summary>Update the settings we care about</summary>
    /// <param name="sender">?</param>
    /// <param name="args">  The setting that was changed</param>
    internal static void UpdateSettings(object sender, EventArgs args)
    {
#if TESTING
        testing = Settings.Settings.TestNewThings;
#endif
        randomize = Settings.Settings.EnableRandomizer;
        if (!randomize)
        {
            SmolRandomizerMenuBuilder.Enabled = SmolRandomizerMenuBuilder.LightGray;
            SmolRandomizerMenuBuilder.Disabled = SmolRandomizerMenuBuilder.LightGray;
        }
        else
            SettingMenu.ChangeColors(Settings.Settings.EnabledRandomizerColors);

        SettingMenu.thisSettingMenu?.UpdateAllSubMenuColors();
    }

    /// <summary>On Scene Transition, get ready to process the new scene</summary>
    /// <param name="scene">The new scene</param>
    /// <param name="mode"> We don't really care about it, but still pass it on</param>
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!randomize) return;

        currentScene = scene;

        foreach (KeyValuePair<string, Action<Scene, LoadSceneMode>> randomizer in activeOnSceneLoad)
            randomizer.Value(scene, mode);

        updateOnFirstSceneFrame = true;
        updateActiveLimitRegions = true;

        SceneFSMPatches.ApplyPatches(scene);
        EnemyFSMPatches.OnSceneLoaded();

        EnemyObjectPatchCollection.OnSceneLoaded();
    }

    /// <summary>We be supportin Hot Reload bois!</summary>
    private void OnDestroy()
    {
        // First Call each modules' Unload
        foreach (KeyValuePair<string, Action> randomizer in activeOnUnload)
        {
            randomizer.Value();
        }

        // Remove any FSM patches
        SceneFSMPatches.RemovePatches(currentScene);
        EnemyFSMPatches.RemovePatches();

        // Remove any Object patches
        EnemyObjectPatchCollection.RemovePatches();

        // Then Unhook Ourself
        SceneManager.sceneLoaded -= OnSceneLoaded;

        // Finally Unpatch
        harmony.UnpatchSelf();
    }

    /// <summary>On Application Quit, we dump the world object dictionary to file</summary>
    private void OnApplicationQuit()
    {
        foreach (KeyValuePair<string, Action> randomizer in activeGameShutdown)
            randomizer.Value();
    }

    #region Randomizer Harmony Patches

    /// <summary>Patch that hooks the end of OnEnable of objects that have a HealthManager</summary>
    /// <param name="__instance">The HealthManager that we may want to adjust</param>
    private static void HealthManager_OnEnable_Postfix(ref HealthManager __instance)
    {
        if (!randomize) return;

        EnemyFSMPatches.ApplyPatches(__instance);

        foreach (KeyValuePair<string, Action<HealthManager>> randomizer in activeEnemy)
            randomizer.Value(__instance);

        EnemyObjectPatchCollection.ApplyPatches(__instance);
    }

    /// <summary>Patch that hooks the end of OnEnable of damage hero objects</summary>
    /// <param name="__instance">      The hero damager</param>
    /// <param name="___healthManager">The HealthManager of the hero damager if it was an enemy</param>
    private static void DamageHero_OnEnable_Postfix(ref DamageHero __instance, ref HealthManager ___healthManager)
    {
        if (!randomize) return;

        foreach (KeyValuePair<string, Action<DamageHero, HealthManager>> randomizer in activeHeroDamager)
            randomizer.Value(__instance, ___healthManager);
    }

    #endregion Randomizer Harmony Patches

    #region File_Import/Export

    /// <summary>Writes the save data for the current save slot.</summary>
    /// <param name="saveFile">The stream for the save file.</param>
    public void WriteSaveData(Stream saveFile)
    {
        RandoPerSaveData.Saving();
        string json = JsonConvert.SerializeObject(Settings.Settings.SaveData, Formatting.Indented);
        using StreamWriter sw = new(saveFile);

        try
        {
            sw.Write(json);
        }
        catch (Exception ex)
        {
            Log.LogError($"Exception encountered, unable to write per-save data for current save slot.\n" + ex.Message);
        }
    }

    /// <summary>Reads the save data for the current slave slot.</summary>
    /// <param name="saveFile">The stream for the save file.</param>
    public void ReadSaveData(Stream? saveFile)
    {
        if (saveFile == null)
        {
            Settings.Settings.SaveData = new();
            return;
        }

        using StreamReader sr = new(saveFile);

        try
        {
            Settings.Settings.SaveData = JsonConvert.DeserializeObject<RandoPerSaveData>(sr.ReadToEnd()) ?? new();
        }
        catch (Exception ex)
        {
            Log.LogError($"Exception encountered, unable to load per-save data for current save slot.\n" + ex.Message);
        }
    }

    /// <summary>Imports a json file to a string object for further processing. Will auto append .json extension.</summary>
    /// <param name="fileName">    the file's name without extension</param>
    /// <param name="importTarget">string of the object to load into</param>
    /// <param name="dataLocation">
    /// string of custom location to read data; if not supplied, it will be read from the default Smol Rando folder
    /// </param>
    public static void ImportJsonFile(string fileName, out string importTarget, string dataLocation = "")
    {
        if (!File.Exists((dataLocation.Equals("") ? DataLocation : dataLocation) + "\\" + fileName + ".json"))
        {
            importTarget = "";
            return;
        }

        try
        {
            importTarget = File.ReadAllText((dataLocation.Equals("") ? DataLocation : dataLocation) + "\\" + fileName + ".json");
        }
        catch (Exception ex)
        {
            Log.LogError($"Exception encountered, unable to import {fileName}.\n" + ex.Message);
            importTarget = "";
        }
    }

    /// <summary>Exports the given object to a json file. Will auto append .json extension.</summary>
    /// <param name="fileName">    the file's name without extension</param>
    /// <param name="exportTarget">the object to save</param>
    /// <param name="dataLocation">
    /// string of custom location to place data; if not supplied, it will be placed in the default Smol Rando folder
    /// </param>
    public static void ExportJsonFile(string fileName, object exportTarget, string dataLocation = "")
    {
        string json = JsonConvert.SerializeObject(exportTarget, Formatting.Indented);
        try
        {
            File.WriteAllText((dataLocation.Equals("") ? DataLocation : dataLocation) + "\\" + fileName + ".json", json);
        }
        catch (Exception ex)
        {
            Log.LogError($"Exception encountered, unable to export {fileName}.\n" + ex.Message);
        }
    }

    #endregion File_Import/Export

    #region Randomizer_Functions

    /// <summary>Create the custom mod menu</summary>
    /// <returns>the built mod menu</returns>
    public AbstractMenuScreen BuildCustomMenu()
    {
        cuteRandomizerSettingWindow = new(MODNAME);
        return cuteRandomizerSettingWindow;
    }

    /// <summary>Retrieves the description of the given randomizer</summary>
    /// <param name="randoName">The name of the randomizer we want the description for</param>
    /// <returns>The description of the given randomizer, returns an empty string if it was not found</returns>
    public static string GetRandoDescription(string randoName)
    {
        randomizerDescriptions.TryGetValue(randoName, out string description);
        return description;
    }

    /// <summary>Sets the description of the given randomizer</summary>
    /// <param name="randoName">       The name of the randomizer we want to add the description of</param>
    /// <param name="randoDescription">The description of the randomizer</param>
    internal static void AddRandoDescription(string randoName, string randoDescription)
    {
        randomizerDescriptions.Add(randoName, randoDescription);
    }

    /// <summary>Registers a new randomizer to be used.</summary>
    /// <param name="randomizer">
    /// A Randomizer_Info object that contains the name of the randomizer, the type, and the method to call when needed.
    /// </param>
    /// <returns>Returns true if the randomizer was registered, otherwise false is returned.</returns>
    /// <exception cref="ArgumentNullException">Thrown if the randomizer's info being registered is null</exception>
    public static bool RegisterRandomizer(Randomizer_Info randomizer)
    {
        if (randomizer == null)
            throw new ArgumentNullException(nameof(randomizer), "Unable to register null as a randomizer.");
        else if (randomizer.Name == null)
            throw new ArgumentException("Randomizer has a null name.");
        else if (randomizer.Method == null)
            throw new ArgumentException("Randomizer has a null action.");

        foreach (Randomizer_Info randoInfo in allRandomizerActions)
        {
            if (randomizer.Method == randoInfo.Method)
                Log.LogError($"Unable to register randomizer action: Duplicate action.");
        }

        if (!allRandomizerActions.Add(randomizer)) return false;

        if (allRandomizers.Add(randomizer.Name))
            Log.LogInfo($"Registering randomizer: {randomizer.Name}.");

        switch (randomizer.RandomizerType)
        {
            case RandomizerEventType.ActiveHeroDamager:
                activeHeroDamager.Add(
                    randomizer.Name,
                    (Action<DamageHero, HealthManager>)DelegateHelper(
                        typeof(Action<DamageHero, HealthManager>),
                        randomizer.Method,
                        randomizer.Object));

                if (!patchedDamageHero)
                {
                    harmony.Patch(
                        AccessTools.Method(typeof(DamageHero), "OnEnable"),
                        postfix: new HarmonyMethod(typeof(CuteRandoCore), nameof(DamageHero_OnEnable_Postfix)));
                    patchedDamageHero = true;
                }

                break;

            case RandomizerEventType.ActiveEnemy:
                activeEnemy.Add(
                    randomizer.Name,
                    (Action<HealthManager>)DelegateHelper(
                        typeof(Action<HealthManager>),
                        randomizer.Method,
                        randomizer.Object));

                if (!patchedHealthManager)
                {
                    harmony.Patch(
                        AccessTools.Method(typeof(HealthManager), "OnEnable"),
                        postfix: new HarmonyMethod(typeof(CuteRandoCore), nameof(HealthManager_OnEnable_Postfix)));
                    patchedHealthManager = true;
                }

                break;

            case RandomizerEventType.ActiveLimitRegion:
                activeLimitRegions.Add(
                    randomizer.Name,
                    (Action<ICurrencyLimitRegion>)DelegateHelper(
                        typeof(Action<ICurrencyLimitRegion>),
                        randomizer.Method,
                        randomizer.Object));
                break;

            case RandomizerEventType.OnSceneLoad:
                activeOnSceneLoad.Add(
                    randomizer.Name,
                    (Action<Scene, LoadSceneMode>)DelegateHelper(
                        typeof(Action<Scene, LoadSceneMode>),
                        randomizer.Method,
                        randomizer.Object));
                break;

            case RandomizerEventType.OnFirstSceneFrame:
                activeOnFirstSceneFrame.Add(
                    randomizer.Name,
                    (Action<Scene>)DelegateHelper(
                        typeof(Action<Scene>),
                        randomizer.Method,
                        randomizer.Object));
                break;

            case RandomizerEventType.GameStartup:
                activeGameStartup.Add(
                    randomizer.Name,
                    (Action)DelegateHelper(
                        typeof(Action),
                        randomizer.Method,
                        randomizer.Object));
                break;

            case RandomizerEventType.GameShutdown:
                activeGameShutdown.Add(
                    randomizer.Name,
                    (Action)DelegateHelper(
                        typeof(Action),
                        randomizer.Method,
                        randomizer.Object));
                break;

            default:
                Log.LogError("Unimplemented entryType");
                allRandomizerActions.Remove(randomizer);
                return false;
        }

        return true;

        static Delegate DelegateHelper(Type type, MethodInfo methodInfo, object? firstArgument = null)
        {
            if (firstArgument != null)
            {
                return Delegate.CreateDelegate(type: type, method: methodInfo, firstArgument: firstArgument);
            }
            return Delegate.CreateDelegate(type: type, method: methodInfo);
        }
    }

    /// <summary>Unregisters a randomizer that was used.</summary>
    /// <param name="randomizer">
    /// A Randomizer_Info object that contains the name of the randomizer, the type, and the method to call when needed.
    /// </param>
    /// <returns>Returns true if the randomizer was unregistered, otherwise false is returned.</returns>
    /// <exception cref="ArgumentNullException">Thrown if the randomizer's info being registered is null</exception>
    public static bool UnregisterRandomizer(Randomizer_Info randomizer)
    {
        if (randomizer == null)
            throw new ArgumentNullException(nameof(randomizer), "Not able to unregister a null randomizer.");

        if (randomizer.Name == null)
            throw new ArgumentException("Not able to unregister a randomizer with a null name.");

        if (!allRandomizerActions.Remove(randomizer))
            return false;

        Log.LogInfo($"Unregistering randomizer event: {randomizer.RandomizerType} from {randomizer.Name}.");

        switch (randomizer.RandomizerType)
        {
            case RandomizerEventType.ActiveHeroDamager:
                activeHeroDamager.Remove(randomizer.Name);
                break;

            case RandomizerEventType.ActiveEnemy:
                activeEnemy.Remove(randomizer.Name);
                break;

            case RandomizerEventType.ActiveLimitRegion:
                activeLimitRegions.Remove(randomizer.Name);
                break;

            case RandomizerEventType.OnSceneLoad:
                activeOnSceneLoad.Remove(randomizer.Name);
                break;

            case RandomizerEventType.OnFirstSceneFrame:
                activeOnFirstSceneFrame.Remove(randomizer.Name);
                break;

            case RandomizerEventType.GameStartup:
                activeGameStartup.Remove(randomizer.Name);
                break;

            case RandomizerEventType.GameShutdown:
                activeGameShutdown.Remove(randomizer.Name);
                break;

            default:
                Log.LogError("Unimplemented entryType");
                return false;
        }

        if (allRandomizerActions.FirstOrDefault(x => x.Name == randomizer.Name) == null)
            allRandomizers.Remove(randomizer.Name);

        return true;
    }

    #endregion Randomizer_Functions

    #region Helper_Functions

    /// <summary>Helper method for grabbing private variables</summary>
    /// <param name="type"> The private field's class.</param>
    /// <param name="field">The private field we want.</param>
    /// <returns></returns>
    public static Traverse TraverseCreator(object type, string field)
    {
        return Traverse.Create(type).Field(field);
    }

    /// <summary>Helper to simplify using Tuples to get a random float value</summary>
    /// <param name="tuple">The tuple to use as min and max float values</param>
    /// ///
    /// <param name="seed"> 
    /// The seed to ensure that we are deterministic, if seed is int's minimum value then we don't use that.
    /// </param>
    /// <returns>A random float between the min and max of the tuple</returns>
    public static float RandomFloat((float min, float max) tuple, int seed = int.MinValue)
    {
        return RandomFloat(tuple.min, tuple.max, seed);
    }

    /// <summary>Helper to simplify using Tuples to get a random int value</summary>
    /// <param name="tuple">The tuple to use as min and max int values</param>
    /// ///
    /// <param name="seed"> 
    /// The seed to ensure that we are deterministic, if seed is int's minimum value then we don't use that.
    /// </param>
    /// <returns>A random int between the min and max of the tuple</returns>
    public static int RandomInt((int min, int max) tuple, int seed = int.MinValue)
    {
        return RandomInt(tuple.min, tuple.max, seed);
    }

    /// <summary>Helper to choose a random value</summary>
    /// <param name="min"> Minimum random value, is inclusive</param>
    /// <param name="max"> Maximum random value, is inclusive when seeded, exclusive when it is not.</param>
    /// ///
    /// <param name="seed">
    /// The seed to ensure that we are deterministic, if seed is int's minimum value then we don't use that.
    /// </param>
    /// <returns>A random float between the min and max</returns>
    public static float RandomFloat(float min, float max, int seed = int.MinValue)
    {
        if (seed > int.MinValue)
        {
            UnityEngine.Random.InitState(seed);
            return UnityEngine.Random.Range(min, max);
        }
        double num = noSeedRNG.NextDouble();
        return (float)((num * (max - min)) + min);
    }

    /// <summary>Helper to choose a random value</summary>
    /// <param name="min"> Minimum random value, is inclusive</param>
    /// <param name="max"> Maximum random value, is inclusive</param>
    /// ///
    /// <param name="seed">
    /// The seed to ensure that we are deterministic, if seed is int's minimum value then we don't use that.
    /// </param>
    /// <returns>A random float between the min and max</returns>
    public static int RandomInt(int min, int max, int seed = int.MinValue)
    {
        if (seed > int.MinValue)
        {
            UnityEngine.Random.InitState(seed);
            return UnityEngine.Random.Range(min, max + 1); // we want max to be inclusive
        }
        return noSeedRNG.Next(min, max + 1);
    }

    public static int GetNewSaveSeed() => UnityEngine.Random.Range(int.MinValue + 1, int.MaxValue);

    /// <summary>Inspired by SimpleEnemyRando's GetCode() for getting the bytes in a string.</summary>
    /// <param name="modifier">A string to modify the seed.</param>
    /// <returns>A modified seed</returns>
    public static int RNGSeed(string modifier)
    {
        int saveSeed = Settings.Settings.SaveData.SaveSeed;
        foreach (byte b in Encoding.Unicode.GetBytes(modifier))
        {
            saveSeed += (int)b;
        }
        return saveSeed;
    }

    /// <summary>Array of bosses that we want to look for</summary>
    public static readonly string[] bossFilter =
    [
        "Lace",
        "Phantom",
        "Silk Boss",
        "Bone Beast",
        "Trobbio",
        "Shakra",
        "Mapper",
        "Forebrother",
        "Garmond",
        "SG_head"
    ];

    /// <summary>
    /// Checks to see if a given HealthManager is attached to a boss
    /// <para>Logic borrowed from SimpleEnemyRando</para>
    /// </summary>
    /// <param name="thing">HealthManager we want to check</param>
    /// <returns>Returns true if it is a boss, otherwise false</returns>
    public static bool IsBoss(HealthManager thing)
    {
        // Test if thing is somehow null or it has no death effect (moss mother arena eggs for example)
        if (thing == null || thing.GetComponent<EnemyDeathEffects>() is EnemyDeathEffectsNoEffect)
            return false;

        // Test by boss name
        foreach (string bossName in bossFilter)
            if (thing.name.Contains(bossName)) return true;

        // Test by boss title card
        foreach (PlayMakerFSM fsm in thing.GetComponents<PlayMakerFSM>())
        {
            foreach (FsmState state in fsm.FsmStates)
            {
                if (state?.Actions.Any(action => action is DisplayBossTitle) == true)
                    return true;
            }
        }

        return false;
    }

    #endregion Helper_Functions
}

/// <summary>The various types of events to handle for randomizers.</summary>
public enum RandomizerEventType : byte
{
    ActiveEnemy,
    ActiveLimitRegion,
    OnSceneLoad,
    OnFirstSceneFrame,
    GameStartup,
    GameShutdown,
    ActiveHeroDamager,
    OnUnload
}

/// <summary>Contains the information for a randomizer that wants to be added.</summary>
/// <param name="name">          Unique name of the randomizer.</param>
/// <param name="randomizerType">The type of the randomizer.</param>
/// <param name="method">        The method to call to use the randomizer.</param>
public class Randomizer_Info(string name, RandomizerEventType randomizerType, MethodInfo method, object? o)
{
    /// <summary>Unique name of the randomizer.</summary>
    public string Name => name;

    /// <summary>The type of the randomizer.</summary>
    public RandomizerEventType RandomizerType => randomizerType;

    /// <summary>The method to call to use the randomizer.</summary>
    public MethodInfo Method => method;

    /// <summary>The first argument of the method being called.</summary>
    public object? Object => o;
}