using System;
using System.Collections.Generic;
using System.Linq;

using BepInEx.Configuration;

using Silksong.ModMenu.Elements;
using Silksong.ModMenu.Models;
using Silksong.ModMenu.Plugin;
using Silksong.ModMenu.Screens;

using Smol_Randomizer.Randomizers;

using UnityEngine;

namespace Smol_Randomizer.Settings;

/// <summary>
/// Creates a custom scroll based menu for the randomizer
/// </summary>
internal class SettingMenu : Cute_Randomizer_MenuBuilder
{
    /// <summary>
    /// Set of all the randomizer menu buttons
    /// </summary>
    private static readonly HashSet<TextButton> randoMenuButtons = [];
    /// <summary>
    /// Dictionary for the initial randomizer menu button enabled/disabled setting
    /// </summary>
    private static readonly Dictionary<string, bool> preInitSetting = [];
    /// <summary>
    /// If the setting menu has been initialized
    /// </summary>
    private static bool init = false;

    /// <summary>
    /// Main randomizer menu
    /// </summary>
    /// <param name="title">Title of the menu (The mod's name)</param>
    internal SettingMenu(LocalizedText title) : base(title)
    {
        Content.VerticalSpacing = VSPACE_TIGHT;
        GenerateMainPage();
#if DEBUG
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
        init = true;
        UpdateSubMenuColors();
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
                TextButton randoMenuButton = SubMenuButton(BuildPagedSubMenu(settingGroup.Key, settingGroup.Value), Cute_Rando_Core.GetRandoDescription(settingGroup.Key));
                randoMenuButtons.Add(randoMenuButton);
            }
        }
    }

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

        if (!init)
            return;

        UpdateSubMenuColor(entry.Definition.Section, enabled);
    }

    private static void UpdateSubMenuColors()
    {
        foreach (KeyValuePair<string, bool> setting in preInitSetting)
            UpdateSubMenuColor(setting.Key, setting.Value);
    }

    /// <summary>
    /// Updates the button colors via string entry
    /// </summary>
    /// <param name="entry">The string of the button to update</param>
    /// <param name="enabled">If the randomizer is enabled</param>
    private static void UpdateSubMenuColor(string entry, bool enabled)
    {
        foreach (TextButton button in randoMenuButtons)
        {
            if (button.ButtonText.text.TrimEnd() == entry)
            {
                if (enabled)
                {
                    button.SetMainColor(DisabledColor);
                }
                else
                {
                    button.SetMainColor(EnabledColor);
                }
            }
        }
    }

    /// <summary>
    /// Event hook to update the enabled/disabled colors
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="args"></param>
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
        switch (newColor)
        {
            case RandomizerColors.GreenRed:
                EnabledColor = Color.green;
                DisabledColor = Color.red;
                break;
            case RandomizerColors.BlueYellow:
                EnabledColor = new(0.05f, 0.48f, 0.86f);
                DisabledColor = new(1f, 0.76f, 0.04f);
                break;
            default:
                throw new NotImplementedException();
        }

        UpdateSubMenuColors();
    }
}

/// <summary>
/// Customized set of menu elements for easy building
/// </summary>
/// <param name="title">Title of the menu</param>
public class Cute_Randomizer_MenuBuilder(LocalizedText title) : ScrollingMenuScreen(title)
{
    // Additional Colors
    public static Color LightGray => new(0.75f, 0.75f, 0.75f);
    public static Color EnabledColor { get; internal set; }

    public static Color DisabledColor { get; internal set; }

    /// <summary>
    /// Tight spacing for vertical
    /// </summary>
    public const float VSPACE_TIGHT = 60f;

    /// <summary>
    /// A blank space for spacing reasons, good for giving room to descriptions
    /// </summary>
    /// <returns></returns>
    public TextLabel BlankSpace()
    {
        TextLabel blank = new("");
        Add(blank);
        return blank;
    }

    /// <summary>
    /// A basic text label
    /// </summary>
    /// <param name="label">Text of the label</param>
    /// <param name="fontSize">Font size of the label</param>
    /// <returns>The added label</returns>
    public TextLabel Label(string label, FontSizes fontSize = FontSizes.Medium)
    {
        return Label(label, Color.white, fontSize);
    }

    /// <summary>
    /// A text label
    /// </summary>
    /// <param name="label">Text of the label</param>
    /// <param name="color">Color of the text</param>
    /// <param name="fontSize">Font size of the label</param>
    /// <returns>The added label</returns>
    public TextLabel Label(string label, Color color, FontSizes fontSize = FontSizes.Medium)
    {
        TextLabel textLabel = new(label);
        textLabel.SetFontSizes(fontSize);
        textLabel.SetMainColor(color);
        Add(textLabel);
        return textLabel;
    }

    /// <summary>
    /// A clickable button with a given action to activate when clicked
    /// </summary>
    /// <param name="label">The button's text</param>
    /// <param name="onClick">The action to be performed when clicked</param>
    /// <param name="description">Description of the button</param>
    /// <param name="fontSize">Font size of the button</param>
    /// <returns>The added button</returns>
    public TextButton Button(string label, Action onClick, string description = "", FontSizes fontSize = FontSizes.Medium)
    {
        TextButton button = new(label, description);
        button.SetFontSizes(fontSize);
        button.OnSubmit = (Action)Delegate.Combine(button.OnSubmit, onClick);
        Add(button);
        return button;
    }

    /// <summary>
    /// A button leading to a sub menu
    /// </summary>
    /// <param name="subMenu">The given menu to navigate to when clicked</param>
    /// <param name="fontSize">Font size of the button</param>
    /// <returns>The added button</returns>
    public TextButton SubMenuButton(AbstractMenuScreen subMenu, string description = "", FontSizes fontSize = FontSizes.Medium)
    {
        TextButton button = new(subMenu);

        button.SetFontSizes(fontSize);
        LocalizedTextExtensions.set_LocalizedText(button.DescriptionText, description);

        Add(button);
        BlankSpace();

        return button;
    }

    /// <summary>
    /// A togglable element
    /// </summary>
    /// <param name="label">Label of the toggle</param>
    /// <param name="configEntry">The setting the toggle is attached to</param>
    /// <param name="description">Description of the toggle</param>
    /// <param name="fontSizes">Font size of the toggle</param>
    /// <returns>The Added toggle</returns>
    public ChoiceElement<bool> ToggleElement(string label, ConfigEntryBase configEntry, string description = "", FontSizes fontSizes = FontSizes.Medium)
    {
        ChoiceElement<bool> element = new(label, ChoiceModels.ForBool(), description);
        element.SynchronizeRawWith(configEntry);
        element.SetFontSizes(fontSizes);

        Add(element);
        return element;
    }

    /// <summary>
    /// A chooser of enums
    /// </summary>
    /// <param name="configEntry">The setting of the chooser</param>
    /// <param name="fontSizes">Font size of the chooser</param>
    /// <returns>The added object of the chooser, may be null if enum was not implemented</returns>
    public object? EnumList(ConfigEntryBase configEntry, FontSizes fontSizes = FontSizes.Medium)
    {
        bool success = ConfigEntryFactory.GenerateEnumChoiceElement(configEntry, out MenuElement? element);

        if (!success || element == null)
        {
            string error = "Failed Generation";

            if (element == null) error = "Null Element";

            Label($"({error})" + configEntry.LabelName(), Color.magenta);
            return null;
        }

        element.SetFontSizes(fontSizes);
        Add(element);
        return element;
    }

    /// <summary>
    /// A slider for a float range
    /// </summary>
    /// <param name="label">Label of the slider</param>
    /// <param name="configEntry">The setting of the slider</param>
    /// <param name="description">Description of the slider</param>
    /// <returns>The two added sliders in an array</returns>
    public SliderElement<float>[] SliderRangeFloat(string label, ConfigEntryBase configEntry, string description = "")
    {
        Label(label, fontSize: FontSizes.Medium);
        if (description != "") Label(description, LightGray, fontSize: FontSizes.Small);

        (float min, float max) = configEntry.Description.AcceptableValues is AcceptableValueRange<float> range ? Tuple.Create(range.MinValue, range.MaxValue) : Tuple.Create(0f, (float)Settings.maxSliderPercent);
        int ticks = (int)Math.Round(max - min);
        LinearFloatSliderModel minModel = SliderModels.ForFloats(min, max, ticks);
        LinearFloatSliderModel maxModel = SliderModels.ForFloats(min, max, ticks);

        SliderElement<float> minSlider = new("Minimum Percent", minModel);
        minSlider.SetFontSizes(FontSizes.Small);
        minSlider.SynchronizeWithFloatRangeMin((ConfigEntry<FloatRange>)configEntry);
        minSlider.ValueText.horizontalOverflow = HorizontalWrapMode.Overflow;
        Add(minSlider);

        SliderElement<float> maxSlider = new("Maximum Percent", maxModel);
        maxSlider.SetFontSizes(FontSizes.Small);
        maxSlider.SynchronizeWithFloatRangeMax((ConfigEntry<FloatRange>)configEntry);
        maxSlider.ValueText.horizontalOverflow = HorizontalWrapMode.Overflow;
        Add(maxSlider);

        return [minSlider, maxSlider];
    }

    /// <summary>
    /// A slider for a int range
    /// </summary>
    /// <param name="label">Label of the slider</param>
    /// <param name="configEntry">The setting of the slider</param>
    /// <param name="description">Description of the slider</param>
    /// <returns>The two added sliders in an array</returns>
    public SliderElement<int>[] SliderRangeInt(string label, ConfigEntryBase configEntry, string description = "")
    {
        Label(label, fontSize: FontSizes.Medium);
        if (description != "") Label(description, LightGray, fontSize: FontSizes.Small);

        (int min, int max) = configEntry.Description.AcceptableValues is AcceptableValueRange<int> range ? Tuple.Create(range.MinValue, range.MaxValue) : Tuple.Create(0, Settings.maxSliderPercent);
        IntSliderModel minModel = SliderModels.ForInts(min, max);
        IntSliderModel maxModel = SliderModels.ForInts(min, max);

        SliderElement<int> minSlider = new("Minimum Value", minModel);
        minSlider.SetFontSizes(FontSizes.Small);
        minSlider.SynchronizeWithIntRangeMin((ConfigEntry<IntRange>)configEntry);
        minSlider.ValueText.horizontalOverflow = HorizontalWrapMode.Overflow;
        Add(minSlider);

        SliderElement<int> maxSlider = new("Maximum Value", maxModel);
        maxSlider.SetFontSizes(FontSizes.Small);
        maxSlider.SynchronizeWithIntRangeMax((ConfigEntry<IntRange>)configEntry);
        maxSlider.ValueText.horizontalOverflow = HorizontalWrapMode.Overflow;
        Add(maxSlider);

        return [minSlider, maxSlider];
    }

    /// <summary>
    /// Input for an int value
    /// </summary>
    /// <param name="label">Label of the input</param>
    /// <param name="configEntry">The setting of the input</param>
    /// <param name="description">Description of the input</param>
    /// <returns>The added input</returns>
    public TextInput<int> IntInput(string label, ConfigEntryBase configEntry, string description = "")
    {
        ParserTextModel<int> model = configEntry.Description.AcceptableValues is AcceptableValueRange<int> range ? TextModels.ForIntegers(range.MinValue, range.MaxValue) : TextModels.ForIntegers();
        TextInput<int> intInput = new(label, model, description);
        intInput.SynchronizeRawWith(configEntry);
        intInput.Model.SetValue((int)configEntry.BoxedValue);
        Add(intInput);
        return intInput;
    }

    /// <summary>
    /// Sub menu builder for the sub menu button
    /// </summary>
    /// <param name="title">Title of the sub menu</param>
    /// <param name="settings">The list of settings for the sub menu</param>
    /// <returns>The new sub menu screen</returns>
    public static ScrollingMenuScreen BuildPagedSubMenu(string title, Dictionary<string, ConfigEntryBase> settings)
    {
        Cute_Randomizer_MenuBuilder screenBuilder = new(title);
        screenBuilder.Content.VerticalSpacing = VSPACE_TIGHT;

        foreach (ConfigEntryBase setting in settings.Values)
        {
            screenBuilder.ElementBuilder(setting);
        }

        return screenBuilder;
    }

    /// <summary>
    /// Adds an element to the menu depending on the type of setting that was given
    /// </summary>
    /// <param name="entry">The setting to have an element added for</param>
    public void ElementBuilder(ConfigEntryBase entry)
    {
        string type = entry.SettingType.IsEnum ? "Enum" : entry.SettingType.Name;
        switch (type)
        {
            case nameof(Boolean):
                ToggleElement(entry.LabelName(), entry, entry.Description.Description);
                if (entry.Description.Description != "") BlankSpace();
                break;
            case nameof(Int32):
                IntInput(entry.LabelName(), entry, entry.Description.Description);
                if (entry.Description.Description != "") BlankSpace();
                break;
            case nameof(FloatRange):
                SliderRangeFloat(entry.LabelName(), entry, entry.Description.Description);
                break;
            case nameof(IntRange):
                SliderRangeInt(entry.LabelName(), entry, entry.Description.Description);
                break;
            case "Enum":
                EnumList(entry);
                if (entry.Description.Description != "") BlankSpace();
                break;
            default:
                Label("(Unimplemented)" + entry.LabelName(), Color.magenta);
                break;
        }
    }
}

/// <summary>
/// Helper Extensions for the Range Sliders
/// </summary>
public static class SliderCuteExtensions
{
    /// <summary>
    /// Synchronize with a minimum float value
    /// </summary>
    /// <param name="element">The slider that we want to sync</param>
    /// <param name="entry">The setting we are syncing with</param>
    public static void SynchronizeWithFloatRangeMin(this SelectableValueElement<float> element, ConfigEntry<FloatRange> entry)
    {
        IValueModel<float> model = element.Model;
        model.SetValue(entry.Value.Min * 100);

        model.OnValueChanged += delegate (float v)
        {
            v = (float)Math.Round(v);
            v /= 100;
            if (v > entry.Value.Max)
            {
                v = entry.Value.Max;
                model.SetValue(v * 100);
            }

            entry.Value.Min = v;
        };

        entry.SettingChanged += handler;

        element.OnVisibilityChanged += delegate (bool visible)
        {
            if (visible) model.SetValue(entry.Value.Min * 100);
        };

        element.OnDispose += delegate
        {
            entry.SettingChanged -= handler;
        };

        void handler(object _, EventArgs args)
        {
            model.SetValue((float)Math.Round(((FloatRange)((SettingChangedEventArgs)args).ChangedSetting.BoxedValue).Min) / 100);
        }
    }

    /// <summary>
    /// Synchronize with a maximum float value
    /// </summary>
    /// <param name="element">The slider that we want to sync</param>
    /// <param name="entry">The setting we are syncing with</param>
    public static void SynchronizeWithFloatRangeMax(this SelectableValueElement<float> element, ConfigEntry<FloatRange> entry)
    {
        IValueModel<float> model = element.Model;
        model.SetValue(entry.Value.Max * 100);

        model.OnValueChanged += delegate (float v)
        {
            v = (float)Math.Round(v);
            v /= 100;
            if (v < entry.Value.Min)
            {
                v = entry.Value.Min;
                model.SetValue(v * 100);
            }

            entry.Value.Max = v;
        };

        entry.SettingChanged += handler;

        element.OnVisibilityChanged += delegate (bool visible)
        {
            if (visible) model.SetValue(entry.Value.Max * 100);
        };

        element.OnDispose += delegate
        {
            entry.SettingChanged -= handler;
        };

        void handler(object _, EventArgs args)
        {
            model.SetValue((float)Math.Round(((FloatRange)((SettingChangedEventArgs)args).ChangedSetting.BoxedValue).Max) / 100);
        }
    }

    /// <summary>
    /// Synchronize with a minimum int value
    /// </summary>
    /// <param name="element">The slider that we want to sync</param>
    /// <param name="entry">The setting we are syncing with</param>
    public static void SynchronizeWithIntRangeMin(this SelectableValueElement<int> element, ConfigEntry<IntRange> entry)
    {
        IValueModel<int> model = element.Model;
        model.SetValue(entry.Value.Min);
        model.OnValueChanged += delegate (int v)
        {
            if (v > entry.Value.Max)
            {
                v = entry.Value.Max;
                model.SetValue(v);
            }

            entry.Value.Min = v;
        };
        entry.SettingChanged += handler;
        element.OnDispose += delegate
        {
            entry.SettingChanged -= handler;
        };

        void handler(object _, EventArgs args)
        {
            model.SetValue(((IntRange)((SettingChangedEventArgs)args).ChangedSetting.BoxedValue).Min);
        }
    }

    /// <summary>
    /// Synchronize with a maximum int value
    /// </summary>
    /// <param name="element">The slider that we want to sync</param>
    /// <param name="entry">The setting we are syncing with</param>
    public static void SynchronizeWithIntRangeMax(this SelectableValueElement<int> element, ConfigEntry<IntRange> entry)
    {
        IValueModel<int> model = element.Model;
        model.SetValue(entry.Value.Max);
        model.OnValueChanged += delegate (int v)
        {
            if (v < entry.Value.Min)
            {
                v = entry.Value.Min;
                model.SetValue(v);
            }

            entry.Value.Max = v;
        };
        entry.SettingChanged += handler;
        element.OnDispose += delegate
        {
            entry.SettingChanged -= handler;
        };

        void handler(object _, EventArgs args)
        {
            model.SetValue(((IntRange)((SettingChangedEventArgs)args).ChangedSetting.BoxedValue).Max);
        }
    }
}