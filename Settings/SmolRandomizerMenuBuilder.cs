using BepInEx.Configuration;

using Silksong.ModMenu.Elements;
using Silksong.ModMenu.Models;
using Silksong.ModMenu.Plugin;
using Silksong.ModMenu.Screens;

using System;
using System.Collections.Generic;

using UnityEngine;

using static Smol_Randomizer.Settings.SliderCuteExtensions;

namespace Smol_Randomizer.Settings;

/// <summary>Customized set of menu elements for easy building</summary>
/// <param name="title">Title of the menu</param>
public class SmolRandomizerMenuBuilder(LocalizedText title) : ScrollingMenuScreen(title)
{
    // Additional Colors
    /// <summary>Light Gray</summary>
    public static Color LightGray => new(0.75f, 0.75f, 0.75f);

    /// <summary>Electric Blue</summary>
    public static Color EBlue => RGBIntToColor(12, 123, 220);

    /// <summary>Dark Yellow</summary>
    public static Color DYellow => RGBIntToColor(255, 194, 10);

    /// <summary>Electric Purple</summary>
    public static Color EPurple => RGBIntToColor(151, 93, 255);

    /// <summary>Dark Orange</summary>
    public static Color DOrange => RGBIntToColor(230, 97, 0);

    /// <summary>Current Enabled <see cref="Color" /></summary>
    public static Color Enabled { get; internal set; }

    /// <summary>Current Disabled <see cref="Color" /></summary>
    public static Color Disabled { get; internal set; }

    /// <summary>Takes <see langword="int" /> values and turns them into a new <see cref="Color" /></summary>
    /// <param name="r">red</param>
    /// <param name="g">green</param>
    /// <param name="b">blue</param>
    /// <param name="a">Optional: alpha</param>
    /// <returns>A new <see cref="Color" /> based on the given values.</returns>
    private static Color RGBIntToColor(int r, int g, int b, int a = 255)
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

    /// <summary>Tight spacing for vertical</summary>
    public const float VSPACE_TIGHT = 60f;

    /// <summary>A blank space for spacing reasons, good for giving room to descriptions</summary>
    /// <returns>The blank <see cref="TextLabel" /></returns>
    public TextLabel BlankSpace()
    {
        TextLabel blank = new("");
        Add(blank);
        return blank;
    }

    /// <summary>A basic <see cref="TextLabel" /> with the default color <see cref="Color.white" /></summary>
    /// <param name="label">   Text of the label</param>
    /// <param name="fontSize">Optional: <see cref="FontSizes" /> of the label, defaluts to <see cref="FontSizes.Medium" /></param>
    /// <returns>The added <see cref="TextLabel" /></returns>
    public TextLabel Label(string label, FontSizes fontSize = FontSizes.Medium)
    {
        return Label(label, Color.white, fontSize);
    }

    /// <summary>A <see cref="TextLabel" /> with a specific <see cref="Color" /></summary>
    /// <inheritdoc cref="Label" />
    /// <param name="color"><see cref="Color" /> of the text</param>
    public TextLabel Label(string label, Color color, FontSizes fontSize = FontSizes.Medium)
    {
        TextLabel textLabel = new(label);
        textLabel.SetFontSizes(fontSize);
        textLabel.SetMainColor(color);
        textLabel.Text.horizontalOverflow = HorizontalWrapMode.Overflow;
        Add(textLabel);
        return textLabel;
    }

    /// <summary>A clickable <see cref="TextButton" /> with a given <see cref="Action" /> to activate when clicked</summary>
    /// <param name="label">      The <see cref="TextButton" />'s text</param>
    /// <param name="onClick">    The <see cref="Action" /> to be performed when clicked</param>
    /// <param name="description">Optional: Description of the <see cref="TextButton" /></param>
    /// <param name="fontSize">   Optional: <see cref="FontSizes" /> of the <see cref="TextButton" /></param>
    /// <returns>The added <see cref="TextButton" /></returns>
    public TextButton Button(string label, Action onClick, string description = "", FontSizes fontSize = FontSizes.Medium)
    {
        TextButton button = new(label, description);
        button.SetFontSizes(fontSize);
        button.OnSubmit = (Action)Delegate.Combine(button.OnSubmit, onClick);
        button.DescriptionText.horizontalOverflow = HorizontalWrapMode.Overflow;
        Add(button);
        return button;
    }

    /// <summary>A clickable <see cref="TextButton" /> leading to a sub menu</summary>
    /// <inheritdoc cref="Button" />
    /// <param name="subMenu">The given menu to navigate to when clicked</param>
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

    /// <summary>A togglable <see cref="ChoiceElement{bool}" /> that is attached to a BepInEx setting</summary>
    /// <param name="label">      Label of the <see cref="ChoiceElement{bool}" /></param>
    /// <param name="configEntry">The BepInEx setting the <see cref="ChoiceElement{bool}" /> is attached to</param>
    /// <param name="description">Optional: Description of the <see cref="ChoiceElement{bool}" /></param>
    /// <param name="fontSizes">  Optional: <see cref="FontSizes" /> of the <see cref="ChoiceElement{bool}" /></param>
    /// <returns>The added <see cref="ChoiceElement{bool}" /></returns>
    public ChoiceElement<bool> ToggleElement(string label, ConfigEntryBase configEntry, string description = "", FontSizes fontSizes = FontSizes.Medium)
    {
        ChoiceElement<bool> element = new(label, ChoiceModels.ForBool(), description);
        element.SynchronizeRawWith(configEntry);
        element.SetFontSizes(fontSizes);
        element.DescriptionText.horizontalOverflow = HorizontalWrapMode.Overflow;

        Add(element);
        return element;
    }

    /// <summary>A <see cref="ChoiceElement{object}" /> of BepInEx setting <see langword="enum" /> s</summary>
    /// <param name="configEntry">The BepInEx setting of the <see cref="ChoiceElement{object}" /></param>
    /// <param name="fontSizes">  Optional: Font size of the <see cref="ChoiceElement{object}" /></param>
    /// <returns>
    /// The added <see langword="object" /> of the <see cref="ChoiceElement{object}" />, may be <see langword="null" />
    /// if the <see langword="enum" /> was not implemented
    /// </returns>
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

    /// <summary>A <see cref="SliderElement{float}" /> for a <see cref="FloatRange" /> BepInEx setting</summary>
    /// <param name="label">      Label of the <see cref="SliderElement{float}" /></param>
    /// <param name="configEntry">The BepInEx setting of the <see cref="SliderElement{float}" /></param>
    /// <param name="description">Optional: Description of the <see cref="SliderElement{float}" /></param>
    /// <returns>The two added <see cref="SliderElement{float}" /> s in an <see cref="Array" /></returns>
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

    /// <summary>A <see cref="SliderElement{int}" /> for a <see cref="IntRange" /> BepInEx setting</summary>
    /// <param name="label">      Label of the <see cref="SliderElement{int}" /></param>
    /// <param name="configEntry">The BepInEx setting of the <see cref="SliderElement{int}" /></param>
    /// <param name="description">Optional: Description of the <see cref="SliderElement{int}" /></param>
    /// <returns>The two added <see cref="SliderElement{int}" /> s in an <see cref="Array" /></returns>
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

    /// <summary><see cref="TextInput{int}" /> for an <see langword="int" /> BepInEx setting</summary>
    /// <param name="label">      Label of the <see cref="TextInput{int}" /></param>
    /// <param name="configEntry">The BepInEx setting of the <see cref="TextInput{int}" /></param>
    /// <param name="description">Optional: Description of the <see cref="TextInput{int}" /></param>
    /// <returns>The added <see cref="TextInput{int}" /></returns>
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

    /// <summary>A <see cref="SliderElement{int}" /> for an <see langword="int" /> BepInEx setting</summary>
    /// <param name="label">      Label of the <see cref="SliderElement{int}" /></param>
    /// <param name="configEntry">The BepInEx setting of the <see cref="SliderElement{int}" /></param>
    /// <param name="description">Optional: Description of the <see cref="SliderElement{int}" /></param>
    /// <returns>The added <see cref="SliderElement{int}" /></returns>
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

    /// <summary>Sub menu builder for the sub menu button</summary>
    /// <param name="settingGroup">
    /// A <see cref="KeyValuePair" /> containing the Title and a Dictionary containing the various settings for a sub menu
    /// </param>
    /// <returns>The new sub menu screen</returns>
    public static ScrollingMenuScreen BuildPagedSubMenu(KeyValuePair<string, Dictionary<string, ConfigEntryBase>> settingGroup)
    {
        string title = settingGroup.Key;
        Dictionary<string, ConfigEntryBase> settings = settingGroup.Value;

        SmolRandomizerMenuBuilder screenBuilder = new(title);
        screenBuilder.Content.VerticalSpacing = VSPACE_TIGHT;

        foreach (ConfigEntryBase setting in settings.Values)
        {
            screenBuilder.ElementBuilder(setting);
        }
        return screenBuilder;
    }

    /// <summary>Adds a <see cref="MenuElement" /> to the menu depending on the type of setting that was given</summary>
    /// <param name="entry">The <see cref="ConfigEntryBase" /> to have a <see cref="MenuElement" /> added for</param>
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

/// <summary>Helper Extensions for the Range Sliders</summary>
public static class SliderCuteExtensions
{
    /// <summary>Formats a slider value to have a % sign after it.</summary>
    /// <param name="_">   Index number, discarded</param>
    /// <param name="item"><see langword="float" /> to be formatted</param>
    /// <returns>The formatted <see langword="float" /> as a <see langword="string" /></returns>
    public static LocalizedText AsPercent(int _, float item)
        => $"{item:0.###}%";

    /// <summary>Converts a range to a <see cref="Tuple" /></summary>
    /// <param name="range">The range for conversion</param>
    /// <returns>A <see cref="Tuple" /> containing the min and max values</returns>
    public static (int min, int max) RangeAsTuple(AcceptableValueRange<int> range)
    {
        return (range.MinValue, range.MaxValue);
    }

    /// <summary>Synchronize with a minimum <see langword="float" /> value</summary>
    /// <param name="element">The slider that we want to sync</param>
    /// <param name="entry">  The BepInEx setting we are syncing with</param>
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

    /// <summary>Synchronize with a maximum <see langword="float" /> value</summary>
    /// <param name="element">The slider that we want to sync</param>
    /// <param name="entry">  The BepInEx setting we are syncing with</param>
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

    /// <summary>Synchronize with a minimum <see langword="int" /> value</summary>
    /// <param name="element">The slider that we want to sync</param>
    /// <param name="entry">  The BepInEx setting we are syncing with</param>
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

    /// <summary>Synchronize with a maximum <see langword="int" /> value</summary>
    /// <param name="element">The slider that we want to sync</param>
    /// <param name="entry">  The BepInEx setting we are syncing with</param>
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