using System;
using System.Collections.Generic;

using BepInEx.Configuration;

using Silksong.ModMenu.Elements;
using Silksong.ModMenu.Models;
using Silksong.ModMenu.Plugin;
using Silksong.ModMenu.Screens;

using UnityEngine;

using static Smol_Randomizer.Settings.SliderCuteExtensions;

namespace Smol_Randomizer.Settings;

/// <summary>
/// Customized set of menu elements for easy building
/// </summary>
/// <param name="title">Title of the menu</param>
public class Smol_Randomizer_MenuBuilder(LocalizedText title) : ScrollingMenuScreen(title)
{
    // Additional Colors
    public static Color LightGray => new(0.75f, 0.75f, 0.75f);
    public static Color EBlue => RGBInttoColor(12, 123, 220);
    public static Color DYellow => RGBInttoColor(255, 194, 10);
    public static Color EPurple => RGBInttoColor(151, 93, 255);
    public static Color DOrange => RGBInttoColor(230, 97, 0);
    public static Color Enabled { get; internal set; }
    public static Color Disabled { get; internal set; }

    /// <summary>
    /// Takes int values and turns them into a new Color
    /// </summary>
    /// <param name="r">red</param>
    /// <param name="g">green</param>
    /// <param name="b">blue</param>
    /// <param name="a">alpha (optional)</param>
    /// <returns>A new color based on the given values.</returns>
    private static Color RGBInttoColor(int r, int g, int b, int a = 255)
    {
        return new((float)(clamp(r) / 255f), (float)(clamp(g) / 255f), (float)(clamp(b) / 255f), (float)(clamp(a) / 255f));

        static int clamp(int val)
        {
            if (val > 255)
                val = 255;
            if (val < 0)
                val = 0;
            return val;
        }
    }

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
        textLabel.Text.horizontalOverflow = HorizontalWrapMode.Overflow;
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
        button.DescriptionText.horizontalOverflow = HorizontalWrapMode.Overflow;
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
        button.DescriptionText.horizontalOverflow = HorizontalWrapMode.Overflow;

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
        element.DescriptionText.horizontalOverflow = HorizontalWrapMode.Overflow;

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
        ((ChoiceElement<object>)element).DescriptionText.horizontalOverflow = HorizontalWrapMode.Overflow;

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
        TextLabel descriptionLabel;

        if (description != "")
        {
            descriptionLabel = Label(description, LightGray, fontSize: FontSizes.Small);
            descriptionLabel.Text.horizontalOverflow = HorizontalWrapMode.Overflow;
        }

        (float min, float max) = configEntry.Description.AcceptableValues is AcceptableRangeforFloatRange range ? Tuple.Create(range.MinValue * 100, range.MaxValue * 100) : Tuple.Create(0f, (float)Settings.maxSliderPercent);
        int ticks = (int)Math.Round(max - min + 1);
        LinearFloatSliderModel minModel = SliderModels.ForFloats(min, max, ticks);
        minModel.DisplayFn = AsPercent;
        LinearFloatSliderModel maxModel = SliderModels.ForFloats(min, max, ticks);
        maxModel.DisplayFn = AsPercent;

        SliderElement<float> minSlider = new("Minimum Percent", minModel);
        minSlider.SetFontSizes(FontSizes.Small);
        minSlider.SynchronizeWithFloatRangeMin(configEntry);
        minSlider.ValueText.horizontalOverflow = HorizontalWrapMode.Overflow;
        Add(minSlider);

        SliderElement<float> maxSlider = new("Maximum Percent", maxModel);
        maxSlider.SetFontSizes(FontSizes.Small);
        maxSlider.SynchronizeWithFloatRangeMax(configEntry);
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
        TextLabel descriptionLabel;

        if (description != "")
        {
            descriptionLabel = Label(description, LightGray, fontSize: FontSizes.Small);
            descriptionLabel.Text.horizontalOverflow = HorizontalWrapMode.Overflow;
        }

        (int min, int max) = configEntry.Description.AcceptableValues is AcceptableRangeforIntRange range ? Tuple.Create(range.MinValue, range.MaxValue) : Tuple.Create(0, Settings.maxSliderPercent);
        IntSliderModel minModel = SliderModels.ForInts(min, max);
        IntSliderModel maxModel = SliderModels.ForInts(min, max);


        SliderElement<int> minSlider = new("Minimum Value", minModel);
        minSlider.SetFontSizes(FontSizes.Small);
        minSlider.SynchronizeWithIntRangeMin(configEntry);
        minSlider.ValueText.horizontalOverflow = HorizontalWrapMode.Overflow;
        Add(minSlider);

        SliderElement<int> maxSlider = new("Maximum Value", maxModel);
        maxSlider.SetFontSizes(FontSizes.Small);
        maxSlider.SynchronizeWithIntRangeMax(configEntry);
        maxSlider.ValueText.horizontalOverflow = HorizontalWrapMode.Overflow;
        Add(maxSlider);

        return [minSlider, maxSlider];
    }

    /// <summary>
    /// Text input for an int value
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
        intInput.DescriptionText.horizontalOverflow = HorizontalWrapMode.Overflow;

        Add(intInput);
        return intInput;
    }

    /// <summary>
    /// Slider input for an int value
    /// </summary>
    /// <param name="label">Label of the input</param>
    /// <param name="configEntry">The setting of the input</param>
    /// <param name="description">Description of the input</param>
    /// <returns>The added input</returns>
    public SliderElement<int> IntSlider(string label, ConfigEntryBase configEntry, string description = "")
    {
        (int min, int max) = configEntry.Description.AcceptableValues is AcceptableValueRange<int> range ? RangeAsTuple(range) : (0, Settings.maxSliderValue);
        IntSliderModel model = SliderModels.ForInts(min, max);
        SliderElement<int> slider = new(label, model);
        slider.SynchronizeRawWith(configEntry);
        slider.Model.SetValue((int)configEntry.BoxedValue);
        Add(slider);

        TextLabel descriptionLabel;

        if (description != "")
        {
            descriptionLabel = Label(description, LightGray, fontSize: FontSizes.Small);
            descriptionLabel.Text.horizontalOverflow = HorizontalWrapMode.Overflow;
        }

        return slider;
    }

    /// <summary>
    /// Sub menu builder for the sub menu button
    /// </summary>
    /// <param name="title">Title of the sub menu</param>
    /// <param name="settings">The list of settings for the sub menu</param>
    /// <returns>The new sub menu screen</returns>
    public static ScrollingMenuScreen BuildPagedSubMenu(KeyValuePair<string, Dictionary<string, ConfigEntryBase>> settingGroup)
    {
        string title = settingGroup.Key;
        Dictionary<string, ConfigEntryBase> settings = settingGroup.Value;

        Smol_Randomizer_MenuBuilder screenBuilder = new(title);
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
                IntSlider(entry.LabelName(), entry, entry.Description.Description);
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
    /// Formats a slider value to have a % sign after it.
    /// </summary>
    /// <param name="_">Index number, discarded</param>
    /// <param name="item">Float to be formatted</param>
    /// <returns>The formatted float</returns>
    public static LocalizedText AsPercent(int _, float item)
        => $"{item:0.###}%";

    /// <summary>
    /// Converts a range to a tuple
    /// </summary>
    /// <param name="range">The range for conversion</param>
    /// <returns>A tuple containing the min and max values</returns>
    public static (int min, int max) RangeAsTuple(AcceptableValueRange<int> range)
    {
        return (range.MinValue, range.MaxValue);
    }

    /// <summary>
    /// Synchronize with a minimum float value
    /// </summary>
    /// <param name="element">The slider that we want to sync</param>
    /// <param name="entry">The setting we are syncing with</param>
    public static void SynchronizeWithFloatRangeMin(this SelectableValueElement<float> element, ConfigEntryBase entry)
    {
        ConfigEntry<FloatRange> configEntry = entry as ConfigEntry<FloatRange>
            ?? throw new ArgumentException("Parameter entry was not a FloatRange");

        IValueModel<float> model = element.Model;
        model.SetValue(configEntry.Value.Min * 100);

        model.OnValueChanged += delegate (float value)
        {
            value /= 100;
            if (value > configEntry.Value.Max)
            {
                value = configEntry.Value.Max;
                model.SetValue(value * 100);
            }

            configEntry.BoxedValue = new FloatRange(value, configEntry.Value.Max);
        };

        element.OnVisibilityChanged += delegate (bool visible)
        {
            if (visible) model.SetValue(configEntry.Value.Min * 100);
        };

        configEntry.SettingChanged += handler;
        element.OnDispose += () => configEntry.SettingChanged -= handler;

        void handler(object _, EventArgs args)
        {
            model.SetValue(((FloatRange)((SettingChangedEventArgs)args).ChangedSetting.BoxedValue).Min * 100);
        }
    }

    /// <summary>
    /// Synchronize with a maximum float value
    /// </summary>
    /// <param name="element">The slider that we want to sync</param>
    /// <param name="entry">The setting we are syncing with</param>
    public static void SynchronizeWithFloatRangeMax(this SelectableValueElement<float> element, ConfigEntryBase entry)
    {
        ConfigEntry<FloatRange> configEntry = entry as ConfigEntry<FloatRange>
            ?? throw new ArgumentException("Parameter entry was not a FloatRange");

        IValueModel<float> model = element.Model;
        model.SetValue(configEntry.Value.Max * 100);

        model.OnValueChanged += delegate (float value)
        {
            value /= 100;
            if (value < configEntry.Value.Min)
            {
                value = configEntry.Value.Min;
                model.SetValue(value * 100);
            }

            configEntry.BoxedValue = new FloatRange(configEntry.Value.Min, value);
        };

        element.OnVisibilityChanged += delegate (bool visible)
        {
            if (visible) model.SetValue(configEntry.Value.Max * 100);
        };

        configEntry.SettingChanged += handler;

        element.OnDispose += () => configEntry.SettingChanged -= handler;

        void handler(object _, EventArgs args)
        {
            model.SetValue(((FloatRange)((SettingChangedEventArgs)args).ChangedSetting.BoxedValue).Max * 100);
        }
    }

    /// <summary>
    /// Synchronize with a minimum int value
    /// </summary>
    /// <param name="element">The slider that we want to sync</param>
    /// <param name="entry">The setting we are syncing with</param>
    public static void SynchronizeWithIntRangeMin(this SelectableValueElement<int> element, ConfigEntryBase entry)
    {
        ConfigEntry<IntRange> configEntry = entry as ConfigEntry<IntRange>
            ?? throw new ArgumentException("Parameter entry was not an IntRange");

        IValueModel<int> model = element.Model;
        model.SetValue(configEntry.Value.Min);
        model.OnValueChanged += delegate (int value)
        {
            if (value > configEntry.Value.Max)
            {
                value = configEntry.Value.Max;
                model.SetValue(value);
            }

            configEntry.BoxedValue = new IntRange(value, configEntry.Value.Max);
        };

        element.OnVisibilityChanged += delegate (bool visible)
        {
            if (visible) model.SetValue(configEntry.Value.Min);
        };

        configEntry.SettingChanged += handler;
        element.OnDispose += () => configEntry.SettingChanged -= handler;

        void handler(object _, EventArgs args)
            => model.SetValue(((IntRange)((SettingChangedEventArgs)args).ChangedSetting.BoxedValue).Min);
    }

    /// <summary>
    /// Synchronize with a maximum int value
    /// </summary>
    /// <param name="element">The slider that we want to sync</param>
    /// <param name="entry">The setting we are syncing with</param>
    public static void SynchronizeWithIntRangeMax(this SelectableValueElement<int> element, ConfigEntryBase entry)
    {
        ConfigEntry<IntRange> configEntry = entry as ConfigEntry<IntRange>
            ?? throw new ArgumentException("Parameter entry was not an IntRange");

        IValueModel<int> model = element.Model;
        model.SetValue(configEntry.Value.Max);
        model.OnValueChanged += delegate (int value)
        {
            if (value < configEntry.Value.Min)
            {
                value = configEntry.Value.Min;
                model.SetValue(value);
            }

            configEntry.BoxedValue = new IntRange(configEntry.Value.Min, value);
        };

        element.OnVisibilityChanged += delegate (bool visible)
        {
            if (visible) model.SetValue(configEntry.Value.Max);
        };

        configEntry.SettingChanged += handler;

        element.OnDispose += () => configEntry.SettingChanged -= handler;

        void handler(object _, EventArgs args)
            => model.SetValue(((IntRange)((SettingChangedEventArgs)args).ChangedSetting.BoxedValue).Max);
    }
}