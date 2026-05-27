using BepInEx;
using Cute_Randomizer.Randomizers;
using GlobalEnums;
using HarmonyLib;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine.SceneManagement;

namespace Cute_Randomizer
{
    [ModMenuIgnore]
    [BepInPlugin(GUID, MODNAME, VERSION)]
    public class Cute_Rando_Core : BaseUnityPlugin
    {
        /// <summary>
        /// Mod ID
        /// </summary>
        public const string GUID = "ashilstraza.randomizer.cute";
        /// <summary>
        /// Mod Name
        /// </summary>
        public const string MODNAME = "Ashen Randomizer";
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
        public static string dataLocation = "";
        /// <summary>
        /// Dictionary containing the randomizers that want to update active enemies
        /// </summary>
        private static readonly Dictionary<string, Action<HealthManager>> activeEnemyRandomizers = [];
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
        /// Dictionary containing the randomizers that want to update when hero damagers activate
        /// </summary>
        private static readonly Dictionary<string, Action<DamageHero>> activeHeroDamagers = [];
        /// <summary>
        /// Dictionary containing the randomizers that want to update on first frame of a new scene
        /// </summary>
        private static readonly Dictionary<string, Action> activeOnFirstSceneFrame = [];

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
        /// On Startup (no window visible)
        /// </summary>
        private void Awake()
        {
            dataLocation = Info.Location.TrimEnd("\\\\Cute_Randomizer.dll".ToCharArray()) + "\\Cute_Randomizer";

            Settings.Settings.Init(Config);
            Enemy_Currency_Rando.InitRandomizer();
            World_Currency_Drop_Rando.InitRandomizer();
            Enemy_Health_Rando.InitRandomizer();
            Basic_Item_Rando.InitRandomizer();
            Enemy_Damage_Rando.InitRandomizer();
            Hero_Damage_Rando.InitRandomizer();
        }

        /// <summary>
        /// On Loading (window visible)
        /// </summary>
        private void Start()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;

            foreach (var rando in activeGameStartup)
            {
                rando.Value.Invoke();
            }
        }

        /// <summary>
        /// On Frame Update
        /// </summary>
        private void Update()
        {
            if (GameManager.SilentInstance == null) return;
            if (!randomize || GameManager.instance.sm?.sceneType != SceneType.GAMEPLAY) return;

            if (updateActiveLimitRegions)
            {
                foreach (ICurrencyLimitRegion region in (HashSet<ICurrencyLimitRegion>)Traverse.Create<CurrencyObjectLimitRegion>().Field("_activeRegions").GetValue())
                {
                    foreach (var randomizer in activeLimitRegions)
                    {
                        randomizer.Value.Invoke(region);
                    }
                }
                updateActiveLimitRegions = false;
            }

            if (updateOnFirstSceneFrame)
            {
                foreach (var randomizer in activeOnFirstSceneFrame)
                {
                    randomizer.Value.Invoke();
                }

                updateOnFirstSceneFrame = false;
            }
        }

        /// <summary>
        /// Update the settings we care about
        /// </summary>
        private void UpdateSettings()
        {
            testing = Settings.Settings.TestNewThings;
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

            foreach (var randomizer in activeOnSceneLoad)
            {
                randomizer.Value.Invoke(scene, mode);
            }

            updateOnFirstSceneFrame = true;
            updateActiveLimitRegions = true;
        }

        /// <summary>
        /// On Application Quit, we dump the world object dictionary to file
        /// </summary>
        private void OnApplicationQuit()
        {
            foreach (var randomizer in activeGameShutdown)
            {
                randomizer.Value.Invoke();
            }
        }

        /// <summary>
        /// Imports a json file to a string object for further processing. Will auto append .json extension.
        /// </summary>
        /// <param name="fileName">the file's name without extension</param>
        /// <param name="importTarget">string of the object to load into</param>
        internal static void ImportJsonFile(string fileName, out string importTarget)
        {
            try
            {
                importTarget = File.ReadAllText(dataLocation + "\\\\" + fileName + ".json");
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
                File.WriteAllText(dataLocation + "\\\\" + fileName + ".json", json);
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
            {
                throw new ArgumentNullException("randomizer", "Unable to register null as a randomizer.");
            }
            else if (randomizer.Name == null)
            {
                throw new ArgumentNullException("Name", "Randomizer has a null name.");
            }
            else if (randomizer.Method == null)
            {
                throw new ArgumentNullException("Action", "Randomizer has a null action.");
            }

            if (!allRandomizerActions.Add(randomizer)) return false;

            if (allRandomizers.Add(randomizer.Name))
                Console.WriteLine($"[{MODNAME}] Registering randomizer: {randomizer.Name}.");

            switch (randomizer.RandomizerType)
            {
                case RandomizerEventType.ActiveHeroDamager:
                    activeHeroDamagers.Add(randomizer.Name, (Action<DamageHero>)Delegate.CreateDelegate(type: typeof(Action<DamageHero>), method: randomizer.Method));
                    break;
                case RandomizerEventType.ActiveEnemy:
                    activeEnemyRandomizers.Add(randomizer.Name, (Action<HealthManager>)Delegate.CreateDelegate(type: typeof(Action<HealthManager>), method: randomizer.Method));
                    break;
                case RandomizerEventType.ActiveLimitRegion:
                    activeLimitRegions.Add(randomizer.Name, (Action<ICurrencyLimitRegion>)Delegate.CreateDelegate(type: typeof(Action<ICurrencyLimitRegion>), method: randomizer.Method));
                    break;
                case RandomizerEventType.OnSceneLoad:
                    activeOnSceneLoad.Add(randomizer.Name, (Action<Scene, LoadSceneMode>)Delegate.CreateDelegate(type: typeof(Action<Scene, LoadSceneMode>), method: randomizer.Method));
                    break;
                case RandomizerEventType.OnFirstSceneFrame:
                    activeOnFirstSceneFrame.Add(randomizer.Name, (Action)Delegate.CreateDelegate(type: typeof(Action), method: randomizer.Method));
                    break;
                case RandomizerEventType.GameStartup:
                    activeGameStartup.Add(randomizer.Name, (Action)Delegate.CreateDelegate(type: typeof(Action), method: randomizer.Method));
                    break;
                default:
                    Console.Error.WriteLine("Unimplimented entryType");
                    allRandomizerActions.Remove(randomizer);
                    return false;
            }
            return true;
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
            {
                throw new ArgumentNullException("randomizer", "Not able to unregister a null randomizer.");
            }
            if (randomizer.Name == null)
            {
                throw new ArgumentNullException("randomizer", "Not able to unregister a randomizer with a null name.");
            }

            if (!allRandomizerActions.Remove(randomizer)) return false;

            Console.WriteLine($"[{MODNAME}] Unregistering randomizer: {randomizer.Name}.");

            switch (randomizer.RandomizerType)
            {
                case RandomizerEventType.ActiveEnemy:
                    activeEnemyRandomizers.Remove(randomizer.Name);
                    break;
                case RandomizerEventType.ActiveLimitRegion:
                    activeLimitRegions.Remove(randomizer.Name);
                    break;
                default:
                    Console.Error.WriteLine("Unimplimented entryType");
                    return false;
            }
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
    public class Randomizer_Info(string name, RandomizerEventType randomizerType, MethodInfo method)
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
    }

    /// <summary>
    /// Class stub to allow adding the [ModMenuIgnore] attribute to the mod so that it doesn't get auto generated.
    /// </summary>
    internal class ModMenuIgnoreAttribute : Attribute { }
}
