using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;

using BepInEx;

using GlobalEnums;

using HarmonyLib;

using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;

using Newtonsoft.Json;

using Silksong.DataManager;
using Silksong.ModMenu.Plugin;
using Silksong.ModMenu.Screens;

using Smol_Randomizer.Randomizers;
using Smol_Randomizer.Settings;

using UnityEngine.SceneManagement;

namespace Smol_Randomizer;

[BepInDependency("org.silksong-modding.modmenu")]
[BepInDependency(DataManagerPlugin.Id)]
[BepInPlugin(GUID, MODNAME, VERSION)]
public class Cute_Rando_Core : BaseUnityPlugin, IModMenuInterface, IModMenuCustomMenu, /*ISaveDataMod<RandoPerSaveData>*/ IRawSaveDataMod
{
    /// <summary>
    /// Mod ID
    /// </summary>
    public const string GUID = "ashilstraza.randomizer.cute";
    /// <summary>
    /// Mod Name
    /// </summary>
    public const string MODNAME = "Smol Randomizer";
    /// <summary>
    /// Mod Version
    /// </summary>
    public const string VERSION = "0.0.1";

    /// <summary>
    /// If we should update the active limit regions after a scene load
    /// </summary>
    private bool updateActiveLimitRegions = false;
    /// <summary>
    /// If we should update on next frame
    /// </summary>
    private bool updateOnFirstSceneFrame = false;
    /// <summary>
    /// List of all registered randomizer names
    /// </summary>
    private static readonly HashSet<string> allRandomizers = [];
    /// <summary>
    /// List of all registered randomizer actions
    /// </summary>
    private static readonly HashSet<Randomizer_Info> allRandomizerActions = [];
    /// <summary>
    /// Mod's directory
    /// </summary>
    public static string DataLocation { get; private set; } = "";
    /// <summary>
    /// If the mod has save data.
    /// </summary>
    public bool HasSaveData => SaveData != null;

    /// <summary>
    /// Per save settings
    /// </summary>
    [AllowNull]
    public static RandoPerSaveData SaveData
    {
        get => Settings.Settings.GetData(); set => Settings.Settings.Load(value ?? new RandoPerSaveData());
    }
    /// <summary>
    /// If we are testing new things
    /// </summary>
    internal static bool testing = false;
    /// <summary>
    /// If we actually want to randomize
    /// </summary>
    internal static bool randomize = true;
    /// <summary>
    /// Our reference for harmony
    /// </summary>
    internal static readonly Harmony harmony = new(GUID);
    /// <summary>
    /// The ModMenu settings menu window 
    /// </summary>
    internal static SettingMenu cuteRandomizerSettingWindow;

    /// <summary>
    /// Dictionary containing the randomizers that want to update the active limit regions
    /// </summary>
    private static readonly Dictionary<string, Action<ICurrencyLimitRegion>> activeLimitRegions = [];
    /// <summary>
    /// Dictionary containing the randomizers that want to update on scene load
    /// </summary>
    private static readonly Dictionary<string, Action<Scene, LoadSceneMode>> activeOnSceneLoad = [];
    /// <summary>
    /// Dictionary containing the randomizers that want to update on game startup
    /// </summary>
    private static readonly Dictionary<string, Action> activeGameStartup = [];
    /// <summary>
    /// Dictionary containing the randomizers that want to update on game shutdown
    /// </summary>
    private static readonly Dictionary<string, Action> activeGameShutdown = [];
    /// <summary>
    /// Dictionary containing the randomizers that want to update on first frame of a new scene
    /// </summary>
    private static readonly Dictionary<string, Action> activeOnFirstSceneFrame = [];
    /// <summary>
    /// Dictionary containing the descriptions of all the randomizers
    /// </summary>
    private static readonly Dictionary<string, string> randomizerDescriptions = [];

    /// <summary>
    /// On Startup (no window visible)
    /// </summary>
    private void Awake()
    {
        DataLocation = Info.Location.TrimEnd("\\\\Smol_Randomizer.dll".ToCharArray()) + "\\Smol_Randomizer";

        Settings.Settings.Init(Config);

        // Access the instance to initialize each randomizer.
        _ = Enemy_Currency_Rando.Instance;
        _ = World_Currency_Drop_Rando.Instance;
        _ = Enemy_Health_Rando.Instance;
        _ = Enemy_Damage_Rando.Instance;
        _ = Hero_Damage_Rando.Instance;
        _ = Enemy_Size_Rando.Instance;
    }

    /// <summary>
    /// On Loading (window visible)
    /// </summary>
    private void Start()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;

        foreach (KeyValuePair<string, Action> rando in activeGameStartup)
            rando.Value.Invoke();
    }

    /// <summary>
    /// On Frame Update
    /// </summary>
    private void Update()
    {
        if (GameManager.SilentInstance == null) return;

        if (!randomize || (GameManager.instance.sm != null ? GameManager.instance.sm.sceneType : null) != SceneType.GAMEPLAY) return;

        if (updateActiveLimitRegions)
        {
            foreach (ICurrencyLimitRegion region in (HashSet<ICurrencyLimitRegion>)Traverse.Create<CurrencyObjectLimitRegion>().Field("_activeRegions").GetValue())
            {
                foreach (KeyValuePair<string, Action<ICurrencyLimitRegion>> randomizer in activeLimitRegions)
                    randomizer.Value.Invoke(region);
            }

            updateActiveLimitRegions = false;
        }

        if (updateOnFirstSceneFrame)
        {
            foreach (KeyValuePair<string, Action> randomizer in activeOnFirstSceneFrame)
                randomizer.Value.Invoke();

            updateOnFirstSceneFrame = false;
        }
    }

    /// <summary>
    /// Update the settings we care about
    /// </summary>
    private static void UpdateSettings()
    {
#if DEBUG
        testing = Settings.Settings.TestNewThings;
#endif
        randomize = Settings.Settings.EnableRandomizer;
    }

    /// <summary>
    /// On Scene Transition, get ready to process the new scene
    /// </summary>
    /// <param name="scene">The new scene</param>
    /// <param name="mode">TODO: dunno</param>
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        UpdateSettings();

        if (!randomize) return;

        foreach (KeyValuePair<string, Action<Scene, LoadSceneMode>> randomizer in activeOnSceneLoad)
            randomizer.Value.Invoke(scene, mode);

        updateOnFirstSceneFrame = true;
        updateActiveLimitRegions = true;
    }

    /// <summary>
    /// On Application Quit, we dump the world object dictionary to file
    /// </summary>
    private void OnApplicationQuit()
    {
        foreach (KeyValuePair<string, Action> randomizer in activeGameShutdown)
            randomizer.Value.Invoke();
    }

    /// <summary>
    /// Create the custom mod menu
    /// </summary>
    /// <returns>the built mod menu</returns>
    public AbstractMenuScreen BuildCustomMenu()
    {
        cuteRandomizerSettingWindow = new(MODNAME);
        return cuteRandomizerSettingWindow;
    }

    /// <summary>
    /// Retrieves the description of the given randomizer
    /// </summary>
    /// <param name="randoName">The name of the randomizer we want the description for</param>
    /// <returns>The description of the given randomizer, returns an empty string if it was not found</returns>
    public static string GetRandoDescription(string randoName)
    {
        randomizerDescriptions.TryGetValue(randoName, out string description);
        return description;
    }

    /// <summary>
    /// Sets the description of the given randomizer
    /// </summary>
    /// <param name="randoName">The name of the randomizer we want to add the description of</param>
    /// <param name="randoDescription">The description of the randomizer</param>
    internal static void AddRandoDescription(string randoName, string randoDescription)
    {
        randomizerDescriptions.Add(randoName, randoDescription);
    }

    /// <summary>
    /// Writes the save data for the current save slot.
    /// </summary>
    /// <param name="saveFile">The stream for the save file.</param>
    public void WriteSaveData(Stream saveFile)
    {
        string json = JsonConvert.SerializeObject(SaveData, Formatting.Indented);
        using StreamWriter sw = new(saveFile);

        try
        {
            sw.Write(json);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[{MODNAME}] Exception encountered, unable to write per-save data for current save slot.\n" + ex.Message);
        }
    }

    /// <summary>
    /// Reads the save data for the current slave slot.
    /// </summary>
    /// <param name="saveFile">The stream for the save file.</param>
    public void ReadSaveData(Stream? saveFile)
    {
        if (saveFile == null)
        {
            SaveData = null;
            return;
        }

        using StreamReader sr = new(saveFile);

        try
        {
            SaveData = JsonConvert.DeserializeObject<RandoPerSaveData>(sr.ReadToEnd());
        }
        catch (Exception ex)
        {
            Console.Error.WriteLineAsync($"[{MODNAME}] Exception encountered, unable to load per-save data for current save slot.\n" + ex.Message);
        }
    }

    /// <summary>
    /// Imports a json file to a string object for further processing. Will auto append .json extension.
    /// </summary>
    /// <param name="fileName">the file's name without extension</param>
    /// <param name="importTarget">string of the object to load into</param>
    internal static void ImportJsonFile(string fileName, out string importTarget)
    {
        if (!File.Exists(DataLocation + "\\\\" + fileName + ".json"))
        {
            importTarget = "";
            return;
        }

        try
        {
            importTarget = File.ReadAllText(DataLocation + "\\\\" + fileName + ".json");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Exception encountered, unable to import {fileName}.\n" + ex.Message);
            importTarget = "";
        }
    }

    /// <summary>
    /// Exports the given object to a json file. Will auto append .json extension.
    /// </summary>
    /// <param name="fileName">the file's name without extension</param>
    /// <param name="exportTarget">the object to save</param>
    internal static void ExportJsonFile(string fileName, object exportTarget)
    {
        string json = JsonConvert.SerializeObject(exportTarget, Formatting.Indented);
        try
        {
            File.WriteAllText(DataLocation + "\\\\" + fileName + ".json", json);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Exception encountered, unable to export {fileName}.\n" + ex.Message);
        }
    }

    /// <summary>
    /// Registers a new randomizer to be used.
    /// </summary>
    /// <param name="randomizer">A Randomizer_Info object that contains the name of the randomizer, the type, and the method to call when needed.</param>
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
            if (randomizer.Name == randoInfo.Name)
                Console.Error.WriteLine($"[{MODNAME}] Unable to register randomizer action: There already is a randomizer with the same name.");
            else if (randomizer.Method == randoInfo.Method)
                Console.Error.WriteLine($"[{MODNAME}] Unable to register randomizer action: Duplicate action.");
        }

        if (!allRandomizerActions.Add(randomizer)) return false;

        if (allRandomizers.Add(randomizer.Name))
            Console.WriteLine($"[{MODNAME}] Registering randomizer: {randomizer.Name}.");

        switch (randomizer.RandomizerType)
        {
            case RandomizerEventType.ActiveHeroDamager:
            case RandomizerEventType.ActiveEnemy:
                // Handled by Harmony Patches
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
                    (Action)DelegateHelper(
                        typeof(Action), 
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
                Console.Error.WriteLine("Unimplimented entryType");
                allRandomizerActions.Remove(randomizer);
                return false;
        }

        return true;

        static Delegate DelegateHelper(Type type, MethodInfo methodInfo, object? firstArgument = null)
        {
            if(firstArgument != null)
            {
                return Delegate.CreateDelegate(type: type, method: methodInfo, firstArgument: firstArgument);
            }
            return Delegate.CreateDelegate(type: type, method: methodInfo);
        }
    }

    /// <summary>
    /// Unregisters a randomizer that was used.
    /// </summary>
    /// <param name="randomizer">A Randomizer_Info object that contains the name of the randomizer, the type, and the method to call when needed.</param>
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

        Console.WriteLine($"[{MODNAME}] Unregistering randomizer event: {randomizer.RandomizerType} from {randomizer.Name}.");

        switch (randomizer.RandomizerType)
        {
            case RandomizerEventType.ActiveHeroDamager:
            case RandomizerEventType.ActiveEnemy:
                // Handled by Harmony Patches
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
                Console.Error.WriteLine("Unimplimented entryType");
                return false;
        }

        if (allRandomizerActions.FirstOrDefault(x => x.Name == randomizer.Name) == null)
            allRandomizers.Remove(randomizer.Name);

        return true;
    }

    /// <summary>
    /// Helper method for grabbing private variables
    /// </summary>
    /// <param name="type">The private field's class.</param>
    /// <param name="field">The private field we want.</param>
    /// <returns></returns>
    public static Traverse TraverseHelper(object type, string field)
    {
        return Traverse.Create(type).Field(field);
    }

    /// <summary>
    /// Helper to simplify using Tuples to get a random float value
    /// </summary>
    /// <param name="tuple">The tuple to use as min and max float values</param>
    /// <returns>A random float provided by UnityEngine.Random between the min and max of the tuple</returns>
    public static float TupleRandoHelper((float min, float max) tuple)
    {
        return UnityEngine.Random.Range(tuple.min, tuple.max);
    }

    /// <summary>
    /// Helper to simplify using Tuples to get a random int value
    /// </summary>
    /// <param name="tuple">The tuple to use as min and max int values</param>
    /// <returns>A random int provided by UnityEngine.Random between the min and max of the tuple</returns>
    public static int TupleRandoHelper((int min, int max) tuple)
    {
        return UnityEngine.Random.Range(tuple.min, tuple.max);
    }

    /// <summary>
    /// Array of bosses that we want to look for
    /// </summary>
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
    /// 
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
}

/// <summary>
/// The various types of events to handle for randomizers.
/// </summary>
public enum RandomizerEventType : byte
{
    ActiveEnemy,
    ActiveLimitRegion,
    OnSceneLoad,
    OnFirstSceneFrame,
    GameStartup,
    GameShutdown,
    ActiveHeroDamager
}

/// <summary>
/// A Class containing the information for a randomizer that wants to be added.
/// </summary>
/// <param name="name">Unique name of the randomizer.</param>
/// <param name="randomizerType">The type of the randomizer.</param>
/// <param name="method">The method to call to use the randomizer.</param>
public class Randomizer_Info(string name, RandomizerEventType randomizerType, MethodInfo method, object? o = null)
{
    /// <summary>
    /// Unique name of the randomizer.
    /// </summary>
    public string Name => name;
    /// <summary>
    /// The type of the randomizer.
    /// </summary>
    public RandomizerEventType RandomizerType => randomizerType;
    /// <summary>
    /// The method to call to use the randomizer.
    /// </summary>
    public MethodInfo Method => method;
    public object? Object => o;
}
