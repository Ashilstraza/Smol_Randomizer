using BepInEx.Configuration;
using Cute_Randomizer.Settings;
using HarmonyLib;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Cute_Randomizer.Randomizers
{
    /// <summary>
    /// Randomizer for Enemy Health
    /// </summary>
    internal class Enemy_Health_Rando
    {
        /// <summary>
        /// Dictionary of the enemy health numbers in current game instance
        /// </summary>
        private static readonly Dictionary<string, int> enemyHealthNumbers = [];
        /// <summary>
        /// Dictionary of the scene health numbers in current game instance
        /// </summary>
        private static readonly Dictionary<string, Dictionary<string, int>> sceneHealthNumbers = [];
        /// <summary>
        /// Set of enemy HealthManagers that we have touched in this scene
        /// </summary>
        private static readonly HashSet<HealthManager> currentEnemyHealthManagers = [];

        /// <summary>
        /// Current scene
        /// </summary>
        private static string operatingScene;
        /// <summary>
        /// If the core of the randomizer is enabled
        /// </summary>
        private static bool coreEnableRandomization = false;

        // Used for determining if we need to update and clear the dictionaries
        private static EnemyHealthRandomizer currentHealthRandomizerSetting;
        private static FloatRange currentEnemyHealthPercentage;
        private static FloatRange currentBossHealthPercentage;

        #region Randomizer_Info
        /// <summary>
        /// Used for registering this randomizer in the core for when enemies activate
        /// </summary>
        private static readonly Randomizer_Info randomizerActiveEnemy = new(
            "Enemy Health Randomizer",
            RandomizerEventType.ActiveEnemy,
            AccessTools.Method(
                typeof(Enemy_Health_Rando),
                nameof(SetHealth)));
        /// <summary>
        /// Used for registering this randomizer in the core for when a scene loads
        /// </summary>
        private static readonly Randomizer_Info randomizerOnSceneLoad = new(
            "Enemy Health Randomizer",
            RandomizerEventType.OnFirstSceneFrame,
            AccessTools.Method(
                typeof(Enemy_Health_Rando),
                nameof(OnFirstSceneFrame)));
        /// <summary>
        /// Used for registering this randomizer in the core for when game starts up
        /// </summary>
        private static readonly Randomizer_Info randomizerGameStartup = new(
            "Enemy Health Randomizer",
            RandomizerEventType.GameStartup,
            AccessTools.Method(
                typeof(Enemy_Health_Rando),
                nameof(GameStartup)));
        #endregion

        /// <summary>
        /// Initialize Enemy Health Randomizer
        /// </summary>
        internal static void InitRandomizer()
        {
            if (!Cute_Rando_Core.RegisterRandomizer(randomizerActiveEnemy)) return;
            if (!Cute_Rando_Core.RegisterRandomizer(randomizerOnSceneLoad)) return;
            if (!Cute_Rando_Core.RegisterRandomizer(randomizerGameStartup)) return;

            InitSettings();

            coreEnableRandomization = Settings.Settings.EnableRandomizer;
        }

        /// <summary>
        /// Patch HealthManager.OnEnable on game startup
        /// </summary>
        private static void GameStartup()
        {
            Cute_Rando_Core.harmony.Patch(AccessTools.Method(typeof(HealthManager), "OnEnable"), postfix: new HarmonyMethod(typeof(Enemy_Health_Rando), nameof(HealthManagerOnEnablePostfix)));
        }

        /// <summary>
        /// On First Frame, clean the active health manager list. This is done on the first frame since some health managers get added before the scene is loaded, and some afterwards.
        /// </summary>
        private static void OnFirstSceneFrame()
        {
            CleanCurrentHealthManagerList();
            operatingScene = GameManager.instance.sceneName;
        }

        /// <summary>
        /// Patch that hooks the end of OnEnable of objects that have a HealthManager to adjust their HP
        /// </summary>
        /// <param name="__instance">The HealthManager that we want to adjust</param>
        internal static void HealthManagerOnEnablePostfix(ref HealthManager __instance)
        {
            if (!coreEnableRandomization || EnemyHealthRandomizerSetting == EnemyHealthRandomizer.None) return;
            if (currentEnemyHealthManagers.Add(__instance)) SetHealth(__instance);
        }

        /// <summary>
        /// Updates an enemy with a new health value
        /// </summary>
        /// <param name="thing">the HealthManager to adjust hp within</param>
        /// /// <exception cref="NotImplementedException">Thrown if there is an unimplemented randomizer type.</exception>
        private static void SetHealth(HealthManager thing)
        {
            if (thing == null) return;

            bool boss = IsBoss(thing);

            if (boss && !EnemyHealthRandomizerSetting.HasFlag(EnemyHealthRandomizer.Boss)) return;

            Traverse initHp = Cute_Rando_Core.TraverseHelper(thing, "initHp");
            Traverse hp = Cute_Rando_Core.TraverseHelper(thing, "hp"); // unneeded, but still doing it for future proofing if it becomes private
            int tempHp;
            string name = thing.name;

            int cullIndex = name.IndexOf('(') - 1;
            if (cullIndex > 0) name = name.Remove(cullIndex);

            switch (RandomizerConsistency)
            {
                case RandomizerConsistency4.EnemyType:
                    if (enemyHealthNumbers.TryGetValue(name, out tempHp))
                    {
                        SetHp(tempHp, initHp, hp);
                    }
                    else
                    {
                        enemyHealthNumbers.Add(name, RandomizeHp(boss, initHp, hp));
                    }
                    break;
                case RandomizerConsistency4.Scene:
                    if (sceneHealthNumbers.TryGetValue(operatingScene, out Dictionary<string, int> healthManagerSet))
                    {
                        if (healthManagerSet.TryGetValue(name, out tempHp))
                        {
                            SetHp(tempHp, initHp, hp);
                        }
                        else
                        {
                            healthManagerSet[name] = RandomizeHp(boss, initHp, hp);
                        }
                    }
                    else
                    {
                        sceneHealthNumbers[operatingScene] = new() { { name, RandomizeHp(boss, initHp, hp) } };
                    }
                    break;
                case RandomizerConsistency4.None:
                    RandomizeHp(boss, initHp, hp);
                    break;
                default:
                    throw new NotImplementedException();
            }

            // Helper to randomize hp
            static int RandomizeHp(bool boss, Traverse initHp, Traverse hp)
            {
                float randFloat = Cute_Rando_Core.TupleRandoHelper(boss ? BossHealthPercentRange.AsTuple() : EnemyHealthPercentRange.AsTuple());
                int tempHp;

                if ((int)initHp.GetValue() <= 0)
                {
                    tempHp = (int)Math.Round((int)hp.GetValue() * randFloat);
                    SetHp(tempHp, initHp, hp);
                }
                else
                {
                    tempHp = (int)Math.Round((int)initHp.GetValue() * randFloat);
                    SetHp(tempHp, initHp, hp);
                }

                return tempHp;
            }

            // Helper to set hp
            static void SetHp(int hpIn, Traverse initHp, Traverse hp)
            {
                hp.SetValue(hpIn);
                initHp.SetValue(hpIn);
            }
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
            if (thing == null || thing.GetComponent<EnemyDeathEffects>() is EnemyDeathEffectsNoEffect) return false;

            // Test by boss name
            foreach (string bossName in bossFilter)
            {
                if (thing.name.Contains(bossName)) return true;
            }

            // Test by boss title card
            foreach (PlayMakerFSM fsm in thing.GetComponents<PlayMakerFSM>())
            {
                foreach (FsmState state in fsm.FsmStates)
                {
                    if (state?.Actions.Any(action => action is DisplayBossTitle) == true) return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Cleans the current scene's HealthManager list
        /// </summary>
        private static void CleanCurrentHealthManagerList()
        {
            currentEnemyHealthManagers.RemoveWhere(x => x == null);
        }

        /// <summary>
        /// Reset all tracked lists
        /// </summary>
        private static void ResetAllHealthLists()
        {
            currentEnemyHealthManagers.Clear();
            enemyHealthNumbers.Clear();
            sceneHealthNumbers.Clear();
        }

        #region Settings
        /// <summary>
        /// Setting for how consistant the enemy health should be
        /// </summary>
        public static RandomizerConsistency4 RandomizerConsistency
        {
            get { return (RandomizerConsistency4)randomizerConsistency.BoxedValue; }
            internal set { randomizerConsistency.BoxedValue = value; }
        }
        private static ConfigEntry<RandomizerConsistency4> randomizerConsistency;
        /// <summary>
        /// Default setting for how consistant the enemy health should be
        /// </summary>
        public static readonly RandomizerConsistency4 defaultRandomizerConsistency = RandomizerConsistency4.None;
        /// <summary>
        /// Randomize the health of enemies
        /// </summary>
        public static EnemyHealthRandomizer EnemyHealthRandomizerSetting
        {
            get { return (EnemyHealthRandomizer)enemyHealthRandomizerSetting.BoxedValue; }
            internal set { enemyHealthRandomizerSetting.BoxedValue = value; }
        }
        private static ConfigEntry<EnemyHealthRandomizer> enemyHealthRandomizerSetting;
        /// <summary>
        /// Default choice for the health randomizer
        /// </summary>
        public static readonly EnemyHealthRandomizer defaultEnemyHealthRandomizerSetting = EnemyHealthRandomizer.None;
        /// <summary>
        /// Randomize the health of normal enemies
        /// </summary>
        public static FloatRange EnemyHealthPercentRange
        {
            get { return (FloatRange)enemyHealthPercentRange.BoxedValue; }
            internal set { enemyHealthPercentRange.BoxedValue = value; }
        }
        private static ConfigEntry<FloatRange> enemyHealthPercentRange;
        /// <summary>
        /// Default choice for enemy health randomizer
        /// </summary>
        public static readonly FloatRange defaultEnemyHealthPercentRange = new(0.25f, 3.0f);
        /// <summary>
        /// Randomize the health of boss enemies
        /// </summary>
        public static FloatRange BossHealthPercentRange
        {
            get { return (FloatRange)bossHealthPercentRange.BoxedValue; }
            internal set { bossHealthPercentRange.BoxedValue = value; }
        }
        private static ConfigEntry<FloatRange> bossHealthPercentRange;
        /// <summary>
        /// Default choice for boss health randomizer
        /// </summary>
        public static readonly FloatRange defaultBossHealthPercentRange = new(0.75f, 1.25f);
        
        /// <summary>
        /// Config Section Name
        /// </summary>
        private static readonly string configSection = "Enemy Health";

        /// <summary>
        /// Adds the enemy health randomizer's settings into the core
        /// </summary>
        private static void InitSettings()
        {
            ConfigFile config = Settings.Settings.ConfigFile;
            enemyHealthRandomizerSetting = config.Bind(
                section: configSection,
                key: "Enemy Health Randomizer",
                defaultValue: defaultEnemyHealthRandomizerSetting,
                configDescription: new ConfigDescription(
                    description: "Allow randomization of enemy and/or boss health.",
                    tags: new ConfigurationManagerAttributes
                    {
                        Order = 3
                    }));
            enemyHealthPercentRange = config.Bind(
                section: configSection,
                key: "Enemy Randomizer",
                defaultValue: defaultEnemyHealthPercentRange,
                configDescription: new ConfigDescription(
                    description: "Randomize regular enemy health.",
                    tags: new ConfigurationManagerAttributes
                    {
                        Order = 2,
                        CustomDrawer = Settings.Settings.RangeDrawer
                    }));
            bossHealthPercentRange = config.Bind(
                section: configSection,
                key: "Boss Randomizer",
                defaultValue: defaultBossHealthPercentRange,
                configDescription: new ConfigDescription(
                    description: "Randomize boss health.",
                    tags: new ConfigurationManagerAttributes
                    {
                        Order = 1,
                        CustomDrawer = Settings.Settings.RangeDrawer
                    }));
            randomizerConsistency = config.Bind(
                section: configSection,
                key: "Enemy Health Randomizer Consistancy",
                defaultValue: defaultRandomizerConsistency,
                configDescription: new ConfigDescription(
                    description: "Setting for if the enemy health should be consistent per enemy type or room.",
                    tags: new ConfigurationManagerAttributes
                    {
                        Order = 0
                    }));

            Settings.Settings.enableRandomizer.SettingChanged += RandoCoreSetting;
            randomizerConsistency.SettingChanged += OnRandoConsistancyUpdated;
            enemyHealthRandomizerSetting.SettingChanged += OnHealthRandoSettingUpdated;
            enemyHealthPercentRange.SettingChanged += OnEnemyHealthSettingUpdated;
            bossHealthPercentRange.SettingChanged += OnBossHealthSettingUpdated;
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
        /// Event hook for when the boss health setting is updated
        /// </summary>
        /// <param name="sender">?</param>
        /// <param name="args">The setting that was changed</param>
        private static void OnBossHealthSettingUpdated(object sender, EventArgs args)
        {
            if (((SettingChangedEventArgs)args).ChangedSetting.BoxedValue is FloatRange fr && !fr.Equals(currentBossHealthPercentage))
            {
                currentBossHealthPercentage = fr;
                ResetAllHealthLists();
            }
        }

        /// <summary>
        /// Event hook for when the enemy health setting is updated
        /// </summary>
        /// <param name="sender">?</param>
        /// <param name="args">The setting that was changed</param>
        private static void OnEnemyHealthSettingUpdated(object sender, EventArgs args)
        {
            if (((SettingChangedEventArgs)args).ChangedSetting.BoxedValue is FloatRange fr && !fr.Equals(currentEnemyHealthPercentage))
            {
                currentEnemyHealthPercentage = fr;
                ResetAllHealthLists();
            }
        }

        /// <summary>
        /// Event hook for when the health randomizer setting is updated
        /// </summary>
        /// <param name="sender">?</param>
        /// <param name="args">The setting that was changed</param>
        private static void OnHealthRandoSettingUpdated(object sender, EventArgs args)
        {
            if (((SettingChangedEventArgs)args).ChangedSetting.BoxedValue is EnemyHealthRandomizer ehr && !ehr.Equals(currentHealthRandomizerSetting))
            {
                currentHealthRandomizerSetting = ehr;
                ResetAllHealthLists();
            }
        }

        /// <summary>
        /// Event hook for when the randomizer consistancy setting is updated
        /// </summary>
        /// <param name="sender">?</param>
        /// <param name="args">The setting that was changed</param>
        private static void OnRandoConsistancyUpdated(object sender, EventArgs args)
        {
            ResetAllHealthLists();
        }
        #endregion
    }

    [Flags]
    internal enum EnemyHealthRandomizer
    {
        None,
        Enemy,
        Boss,
        Both
    }
}
