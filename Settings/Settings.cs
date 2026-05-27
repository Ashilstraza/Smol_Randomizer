using BepInEx.Configuration;
using System;
using UnityEngine;

namespace Cute_Randomizer.Settings
{
    /// <summary>
    /// Handles the various settings <see href="https://github.com/BepInEx/BepInEx.ConfigurationManager/blob/master/README.md"/>
    /// </summary>
    public static class Settings
    {
        #region Settings
        /// <summary>
        /// If we want to test new things
        /// </summary>
        public static bool TestNewThings
        {
            get { return (bool)testNewThings.BoxedValue; }
            internal set { testNewThings.BoxedValue = value; }
        }
        private static ConfigEntry<bool> testNewThings;
        /// <summary>
        /// Default for if we want to test new things
        /// </summary>
        public static readonly bool defaultTestNewThings = false;
        /// <summary>
        /// Enables the randomization of the various things
        /// </summary>
        public static bool EnableRandomizer
        {
            get { return (bool)enableRandomizer.BoxedValue; }
            internal set { enableRandomizer.BoxedValue = value; }
        }
        internal static ConfigEntry<bool> enableRandomizer;
        /// <summary>
        /// Default if we want to randomize the various things
        /// </summary>
        public static readonly bool defaultEnableRandomizer = true;
        #endregion

        /// <summary>
        /// Our config file
        /// </summary>
        private static ConfigFile configFile;
        /// <summary>
        /// Max Slider Percentage
        /// </summary>
        private static readonly int maxPercent = 300;

        /// <summary>
        /// Max Slider Value
        /// </summary>
        private static readonly int maxValue = 100;

        /// <summary>
        /// Reference to the randomizer's config file to allow adding settings.
        /// </summary>
        public static ConfigFile ConfigFile { get { return configFile; } }

        /// <summary>
        /// Initialize the various settings.
        /// </summary>
        /// <remarks> Order is backwards for some reason?</remarks>
        /// <param name="config"></param>
        public static void Init(ConfigFile config)
        {
            configFile = config;
            TomlTypeConverter.AddConverter(typeof(FloatRange), FloatRangeConverter);
            TomlTypeConverter.AddConverter(typeof(IntRange), IntRangeConverter);

            enableRandomizer = config.Bind(
                "Main Settings",
                "Enable Randomizer",
                defaultEnableRandomizer,
                new ConfigDescription(
                    "Enable Randomization.",
                    null,
                    new ConfigurationManagerAttributes
                    {
                        Order = 1
                    }));
            testNewThings = config.Bind(
                "Testing",
                "Test new things",
                defaultTestNewThings,
                new ConfigDescription(
                    "Test new things"));


        }

        //private static 

        /// <summary>
        /// Custom Drawer for entering Ranges
        /// </summary>
        /// <param name="entry">The entry to draw.</param>
        public static void RangeDrawer(ConfigEntryBase entry)
        {
            Type entryType = entry.BoxedValue.GetType();
            if (entryType == typeof(FloatRange))
            {
                FloatRange value = (FloatRange)entry.BoxedValue;
                string min = (value.Min * 100).ToString();
                string max = (value.Max * 100).ToString();
                TextRange(ref min, ref max, RangeType.Percent);
                try
                {
                    entry.BoxedValue = new FloatRange(float.Parse(min) / 100, float.Parse(max) / 100);
                }
                catch (ArgumentException e)
                {
                    Console.Error.WriteLine(e.Message);
                }
            }
            else if (entryType == typeof(IntRange))
            {
                IntRange value = (IntRange)entry.BoxedValue;
                string min = value.Min.ToString();
                string max = value.Max.ToString();
                TextRange(ref min, ref max, RangeType.Value);
                try
                {
                    entry.BoxedValue = new IntRange((int)Math.Round(float.Parse(min)), (int)Math.Round(float.Parse(max)));
                }
                catch (ArgumentException e)
                {
                    Console.Error.WriteLine(e.Message);
                }
            }
            else
            {
                Console.Error.WriteLine("Unimplimented entryType");
            }

        }

        /// <summary>
        /// Range UI Control
        /// </summary>
        /// <param name="min">Minimum Value.</param>
        /// <param name="max">Maximum Value.</param>
        /// <param name="rangeType">Type of range the values are.</param>
        static void TextRange(ref string min, ref string max, RangeType rangeType)
        {
            using var verticalGroup = new GUILayout.VerticalScope("box");
            using (var horizontalGroup = new GUILayout.HorizontalScope("box"))
            {
                GUILayout.Label($"Minimum {(rangeType.Equals(RangeType.Percent) ? "Percent" : "Value")}");
                min = GUILayout.TextField(min, GUILayout.Width(30));
                min = GUILayout.HorizontalSlider((float)Math.Round(float.Parse(min)), 0, (float)Math.Round(float.Parse(max)), GUILayout.Width(100)).ToString();

            }
            using (var horizontalGroup = new GUILayout.HorizontalScope("box"))
            {
                GUILayout.Label($"Maximum {(rangeType.Equals(RangeType.Percent) ? "Percent" : "Value")}");
                max = GUILayout.TextField(max, GUILayout.Width(30));
                max = GUILayout.HorizontalSlider((float)Math.Round(float.Parse(max)), (float)Math.Round(float.Parse(min)), rangeType.Equals(RangeType.Percent) ? maxPercent : maxValue, GUILayout.Width(100)).ToString();
            }
        }

        /// <summary>
        /// Converter for BepInEx to convert the FloatRange into something savable then back again
        /// </summary>
        static readonly TypeConverter FloatRangeConverter = new()
        {
            ConvertToString = (obj, type) => obj.ToString(),
            ConvertToObject = (str, type) => FloatRange.Parse(str)
        };

        /// <summary>
        /// Converter for BepInEx to convert the IntRange into something savable then back again
        /// </summary>
        static readonly TypeConverter IntRangeConverter = new()
        {
            ConvertToString = (obj, type) => obj.ToString(),
            ConvertToObject = (str, type) => IntRange.Parse(str)
        };

        /// <summary>
        /// Randomizer Range Types
        /// </summary>
        public enum RangeType
        {
            Percent,
            Value
        }
    }

    /// <summary>
    /// Range Randomize Types
    /// </summary>
    public enum RandomizeByRangeTypes
    {
        Disabled,
        Percent,
        Value
    }

    /// <summary>
    /// Flat Amount Randomize Types
    /// </summary>
    internal enum RandomizeByFlatAmount
    {
        Disabled,
        Shift,
        Range
    }

    /// <summary>
    /// Randomizer Enable/Disable
    /// </summary>
    public enum RandomizerEnable
    {
        Disabled,
        Enabled
    }

    /// <summary>
    /// Randomizer Consistency Type
    /// </summary>
    public enum RandomizerConsistency4
    {
        None,
        EnemyType,
        Scene,
        Game
    }

    /// <summary>
    /// The options for world shard consistancy
    /// </summary>
    public enum RandomizerConsistency3
    {
        None,
        Scene,
        Game
    }
}
