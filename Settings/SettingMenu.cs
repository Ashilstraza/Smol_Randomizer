using System;
using System.Collections.Generic;
using System.Linq;

using BepInEx.Configuration;

using Silksong.ModMenu.Elements;
using Silksong.ModMenu.Models;

using Smol_Randomizer.Randomizers;

using UnityEngine;

namespace Smol_Randomizer.Settings;

/// <summary>
/// Creates a custom scroll based menu for the randomizer
/// </summary>
internal class SettingMenu : SmolRandomizerMenuBuilder
{
    /// <summary>
    /// Set of all the randomizer menu buttons
    /// </summary>
    private readonly HashSet<TextButton> randoMenuButtons = [];
    /// <summary>
    /// Dictionary for the initial randomizer menu button enabled/disabled setting
    /// </summary>
    private static readonly Dictionary<string, bool> preInitSetting = [];
    /// <summary>
    /// If the setting menu has been initialized
    /// </summary>
    internal static SettingMenu thisSettingMenu;

    /// <summary>
    /// Main randomizer menu
    /// </summary>
    /// <param name="title">Title of the menu (The mod's name)</param>
    internal SettingMenu(LocalizedText title) : base(title)
    {
        Content.VerticalSpacing = VSPACE_TIGHT;
        GenerateMainPage();
#if DEBUG && TESTING // Basic Item Rando
        BlankSpace();
        Label("World Objects", FontSizes.Medium);
        Button("Export World Objects",
            delegate
            {
                Console.WriteLine("Exporting World Objects");
                Basic_Item_Rando.ExportWorldObjectsFile();
            },
            fontSize: FontSizes.Small);
        Button("Import World Objects",
            delegate
            {
                Console.WriteLine("Import World Objects");
                Basic_Item_Rando.ImportWorldObjectsFile();
            },
            fontSize: FontSizes.Small);
#endif
        thisSettingMenu = this;
        UpdateAllSubMenuColors();
    }

    /// <summary>
    /// Generates the various menus and options on the main page
    /// </summary>
    private void GenerateMainPage()
    {
        ConfigFile config = Settings.ConfigFile;
        Dictionary<string, Dictionary<string, ConfigEntryBase>> settingList = [];

        foreach (KeyValuePair<ConfigDefinition, ConfigEntryBase> item in config)
        {
            if (!settingList.TryGetValue(item.Key.Section, out Dictionary<string, ConfigEntryBase>? value))
                settingList[item.Key.Section] = new() { { item.Key.Key, item.Value } };
            else
                value.Add(item.Key.Key, item.Value);
        }

        foreach (KeyValuePair<string, Dictionary<string, ConfigEntryBase>> settingGroup in settingList)
        {
            if (settingGroup.Value.Count == 1)
            {
                KeyValuePair<string, ConfigEntryBase> setting = settingGroup.Value.First();
                Label(settingGroup.Key, FontSizes.Medium);
                ElementBuilder(setting.Value);
            }
            else
            {
                SmolRandomizerMenuBuilder subMenu = (SmolRandomizerMenuBuilder)BuildPagedSubMenu(settingGroup);
                Additional_Elements(ref subMenu, settingGroup);
                randoMenuButtons.Add(SubMenuButton(subMenu, CuteRandoCore.GetRandoDescription(settingGroup.Key)));

                // Add Current Save Menu after Main Settings button
                if (settingGroup.Key == "Main Settings")
                {
                    TextButton saveInfo = SubMenuButton(SaveInfoScreen(), "Information for the current save.");
                    saveInfo.OnVisibilityChanged += delegate (bool visible)
                    {
                        if (visible)
                        {
                            saveInfo.SetMainColor(Settings.Loaded ? Color.white : Color.gray);
                        }
                    };

                    randoMenuButtons.Add(saveInfo);
                }
            }
        }
    }

    /// <summary>
    /// Add reset to defaults and reset saved values
    /// </summary>
    /// <param name="screenBuilder">The screenBuilder menu we are modifying</param>
    /// <param name="settingGroup">The settings for the menu</param>
    private static void Additional_Elements(ref SmolRandomizerMenuBuilder screenBuilder, KeyValuePair<string, Dictionary<string, ConfigEntryBase>> settingGroup)
    {
        string title = settingGroup.Key;
        Dictionary<string, ConfigEntryBase> settings = settingGroup.Value;

        if (!title.Equals("Main Settings"))
        {
            // Every menu except Main Settings Menu

            screenBuilder.Button("Defaults",
            delegate
            {
                foreach (ConfigEntryBase setting in settings.Values)
                    setting.BoxedValue = setting.DefaultValue;
            },
            "Resets the settings to their default values.");
            screenBuilder.BlankSpace();
#if DEBUG && TESTING // Enable Saving Data
            TextButton resetButton = screenBuilder.Button("Reset Saved Values for Current Slot",
                delegate
                {
                    string sectionName = settings.First().Value.Definition.Section;
                    try
                    {
                        OnResetClicked?.Invoke(settings.First().Value.Definition.Section);
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLineAsync($"[{CuteRandoCore.MODNAME}] Exception encountered when invoking OnResetClicked().\n" + ex.Message);
                    }
                },
                "Resets the saved values for the current slot.");

            resetButton.OnVisibilityChanged += delegate (bool visible)
            {
                if (!visible) return;

                resetButton.SetMainColor(Settings.Loaded ? Color.white : Color.gray);
            };

            randoResetButtons.Add(resetButton);
#endif
        }
        else
        {
            // Main Settings Menu

            screenBuilder.Button("Reset All Settings to Their Defaults",
                delegate
                {
                    try
                    {
                        foreach (KeyValuePair<ConfigDefinition, ConfigEntryBase> setting in Settings.ConfigFile)
                            setting.Value.BoxedValue = setting.Value.DefaultValue;
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLineAsync($"[{CuteRandoCore.MODNAME}] Exception encountered when resetting all settings.\n" + ex.Message);
                    }
                },
                "Resets the settings for all the randomizers to their default settings.");
            screenBuilder.BlankSpace();
#if DEBUG && TESTING // Enable Saving Data
            TextButton resetButton = screenBuilder.Button("Reset All Saved Values for Current Slot",
                ResetAllSavedData,
                "Resets the saved values for the current slot.");

            resetButton.OnVisibilityChanged += delegate (bool visible)
            {
                if (!visible) return;

                resetButton.SetMainColor(Settings.Loaded ? Color.white : Color.gray);
            };

            randoResetButtons.Add(resetButton);
#endif
        }
    }

    private static void ResetAllSavedData()
    {

        try
        {
            HashSet<string> resetRandos = [];
            foreach (KeyValuePair<ConfigDefinition, ConfigEntryBase> setting in Settings.ConfigFile)
            {
                string section = setting.Key.Section;
                if (!resetRandos.Contains(setting.Key.Section))
                {
                    OnResetClicked?.Invoke(section);
                    resetRandos.Add(section);
                }
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLineAsync($"[{CuteRandoCore.MODNAME}] Exception encountered when resetting all saved values.\n" + ex.Message);
        }
    }

    /// <summary>
    /// Builds the Save Information Screen
    /// </summary>
    /// <returns>The scrolling menu for the Save Information Screen</returns>
    private static SmolRandomizerMenuBuilder SaveInfoScreen()
    {
        SmolRandomizerMenuBuilder screenBuilder = new("Current Save Information");
        TextLabel seedLabel = screenBuilder.Label(SeedText());
        seedLabel.OnVisibilityChanged += delegate (bool visible)
        {
            if (!visible) return;

            seedLabel.Text.text = SeedText();
            seedLabel.SetMainColor(Settings.Loaded ? Color.white : Color.gray);
        };

        TextInput<int> seedInput = SeedInput("Set Seed", "Sets the seed to the entered value.", seedLabel);

        TextButton seedButton = screenBuilder.Button("Reroll Seed",
            delegate
            {
                if (!Settings.Loaded) return;

                Settings.SaveData.RerollSeed();
                seedLabel.Text.text = SeedText();
                seedInput.Model.SetValue(Settings.SaveData.SaveSeed);
            });
        seedButton.OnVisibilityChanged += delegate (bool visible)
        {
            if (!visible) return;

            seedButton.SetMainColor(Settings.Loaded ? Color.white : Color.gray);
        };

        static TextInput<int> SeedInput(string label, string description, TextLabel seedLabel)
        {
            ParserTextModel<int> model = TextModels.ForIntegers();
            TextInput<int> intInput = new(label, model, description)
            {
                Value = Settings.Loaded ? Settings.SaveData.SaveSeed : 0
            };

            intInput.OnValueChanged += delegate (int value)
            {
                if (!Settings.Loaded) return;

                Settings.SaveData.SetSeed(value);
                seedLabel.Text.text = SeedText();
                ResetAllSavedData();
            };

            intInput.OnVisibilityChanged += delegate (bool visible)
            {
                if (!visible) return;

                intInput.Value = Settings.Loaded ? Settings.SaveData.SaveSeed : 0;
                intInput.SetMainColor(Settings.Loaded ? Color.white : Color.gray);
            };

            return intInput;
        }

        static string SeedText()
        {
            return Settings.Loaded ? "Seed: " + Settings.SaveData.SaveSeed : "No Save Loaded";
        }

        screenBuilder.Add(seedInput);



        return screenBuilder;
    }

    /// <summary>
    /// Called when a "Reset Values" button is clicked
    /// </summary>
    public static event Action<string> OnResetClicked;

    /// <summary>
    /// Set of all the reset buttons
    /// </summary>
    private static readonly HashSet<TextButton> randoResetButtons = [];

    /// <summary>
    /// Event hook to listen for updates on a specific setting
    /// </summary>
    /// <param name="sender">?</param>
    /// <param name="args">The setting that was changed</param>
    internal static void OnRandomizerEnable(object sender, EventArgs args)
    {
        UpdateSubMenuColor(((SettingChangedEventArgs)args).ChangedSetting);
    }

    /// <summary>
    /// Updates the button color if it was enabled or disabled
    /// </summary>
    /// <param name="entry">The setting we are using to check if the button is enabled or not</param>
    internal static void UpdateSubMenuColor(ConfigEntryBase entry)
    {
        bool enabled = entry.BoxedValue.ToString().ToLower() is "none" or "disabled" or "false";

        preInitSetting[entry.Definition.Section] = enabled;

        if (thisSettingMenu == null)
            return;

        thisSettingMenu?.UpdateSubMenuColor(entry.Definition.Section, enabled);
    }

    /// <summary>
    /// Updates all the menu colors at once.
    /// </summary>
    internal void UpdateAllSubMenuColors()
    {
        foreach (KeyValuePair<string, bool> setting in preInitSetting)
            UpdateSubMenuColor(setting.Key, setting.Value);
    }

    /// <summary>
    /// Updates the button colors via string entry
    /// </summary>
    /// <param name="entry">The string of the button to update</param>
    /// <param name="enabled">If the randomizer is enabled</param>
    private void UpdateSubMenuColor(string entry, bool enabled)
    {
        foreach (TextButton button in randoMenuButtons)
        {
            if (button.ButtonText.text.TrimEnd() == entry)
            {
                if (enabled)
                {
                    button.SetMainColor(Disabled);
                }
                else
                {
                    button.SetMainColor(Enabled);
                }
            }
        }
    }

    /// <summary>
    /// Event hook to update the enabled/disabled colors
    /// </summary>
    /// <param name="sender">?</param>
    /// <param name="args">The setting that was changed</param>
    internal static void OnEnabledRandomizerColorChanged(object sender, EventArgs args)
    {
        ChangeColors((RandomizerColors)((SettingChangedEventArgs)args).ChangedSetting.BoxedValue);
    }

    /// <summary>
    /// Changes the enabled/disabled colors
    /// </summary>
    /// <param name="newColor">The new color set</param>
    /// <exception cref="NotImplementedException">Thrown if the color set has not been implemented</exception>
    internal static void ChangeColors(RandomizerColors newColor)
    {
        if (CuteRandoCore.randomize)
            switch (newColor)
            {
                case RandomizerColors.GreenRed:
                    Enabled = Color.green;
                    Disabled = Color.red;
                    break;
                case RandomizerColors.BlueYellow:
                    Enabled = EBlue;
                    Disabled = DYellow;
                    break;
                case RandomizerColors.PurpleOrange:
                    Enabled = EPurple;
                    Disabled = DOrange;
                    break;
                default:
                    throw new NotImplementedException();
            }
        else
        {
            Enabled = LightGray;
            Disabled = LightGray;
        }


        thisSettingMenu?.UpdateAllSubMenuColors();
    }
}