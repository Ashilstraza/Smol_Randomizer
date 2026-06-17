using BepInEx;
using BepInEx.Configuration;
using Cute_Randomizer.Randomizers;
using Silksong.ModMenu.Elements;
using Silksong.ModMenu.Models;
using Silksong.ModMenu.Plugin;
using Silksong.ModMenu.Screens;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Cute_Randomizer.Settings
{
    internal class SettingMenu : Cute_Randomizer_MenuBuilder
    {
        internal SettingMenu(LocalizedText title) : base(title)
        {
            Work();
#if DEBUG
            Label("World Objects", FontSizes.Medium);
            Button("Export World Objects", delegate
            {
                Console.WriteLine("Exporting World Objects");
                Basic_Item_Rando.ExportWorldObjectsFile();
            });
            Button("Import World Objects", delegate
            {
                Console.WriteLine("Import World Objects");
                Basic_Item_Rando.ImportWorldObjectsFile();
            });
#endif
        }

        private Dictionary<string, Dictionary<string, ConfigEntryBase>> settingList = [];

        private void Work()
        {
            ConfigFile config = Settings.ConfigFile;
            foreach (var item in config)
            {
                if (!settingList.TryGetValue(item.Key.Section, out Dictionary<string, ConfigEntryBase>? value))
                {
                    settingList[item.Key.Section] = new() { {item.Key.Key, item.Value } };
                }
                else
                {
                    value.Add(item.Key.Key, item.Value);
                }
            }
            
            foreach (var settingGroup in settingList)
            {
                if(settingGroup.Value.Count == 1)
                {
                    var setting = settingGroup.Value.First();
                    Label(settingGroup.Key, FontSizes.Medium);
                    ElementBuilder(setting.Value);
                }
                else
                {
                    SubMenuButton(BuildSubMenu(settingGroup.Key, settingGroup.Value));
                }
            }
        }
    }

    public class Cute_Randomizer_MenuBuilder : PaginatedMenuScreenBuilder
    {
        internal GameObject descriptionDummy;

        public Cute_Randomizer_MenuBuilder(LocalizedText title, int pageSize = 8) : base(title, pageSize)
        {
            TextButton button = new TextButton("dummy", "description");
            descriptionDummy = UnityEngine.Object.Instantiate<GameObject>(button.MenuButton.gameObject.transform.Find("Description").gameObject);
            UnityEngine.Object.DontDestroyOnLoad(descriptionDummy);
        }

        public TextLabel Label(string label, FontSizes fontSize = FontSizes.Medium)
        {
            return Label(label, Color.white, fontSize);
        }

        public TextLabel Label(string label, Color color, FontSizes fontSize = FontSizes.Medium)
        {
            TextLabel textLabel = new(label);
            textLabel.SetFontSizes(fontSize);
            textLabel.SetMainColor(color);
            Add(textLabel);
            return textLabel;
        }

        public TextButton Button(string label, Action onClick, string description = "", FontSizes fontSize = FontSizes.Medium)
        {
            TextButton button = new(label, description);
            button.SetFontSizes(fontSize);
            button.OnSubmit = (Action)Delegate.Combine(button.OnSubmit, onClick);
            Add(button);
            return button;
        }

        public TextButton SubMenuButton(AbstractMenuScreen subMenu, string description = "", FontSizes fontSize = FontSizes.Medium)
        {
            TextButton button = new(subMenu);
            button.SetFontSizes(fontSize);
            Add(button);
            return button;
        }

        internal GameObject ButtonLabeled(string label, string buttonText, Action onClick, string description = "", FontSizes fontSize = FontSizes.Medium)
        {

            GameObject buttonLabeled = new GameObject("Labeled Button");
            GameObject button = DefaultControls.CreateButton(new DefaultControls.Resources());
            button.transform.SetParent(buttonLabeled.transform, false);
            /*buttonLabeled.layer = 5;
            buttonLabeled.SetActive(false);
            UnityEngine.Object.DontDestroyOnLoad(buttonLabeled);
            HorizontalLayoutGroup layoutGroup = buttonLabeled.AddComponent<HorizontalLayoutGroup>();
            TextLabel textLabel = Label(label, fontSize, false);
            textLabel.UpdateLayout(new(-300,0));
            textLabel.SetGameObjectParent(layoutGroup.gameObject);
            TextButton buttonLabel = Button(buttonText, onClick, description, fontSize, false);
            buttonLabel.SetGameObjectParent(layoutGroup.gameObject);
            layoutGroup.spacing = 30;
            layoutGroup.SetLayoutHorizontal();
            layoutGroup.childForceExpandWidth = true;
            layoutGroup.childAlignment = TextAnchor.MiddleCenter;
            layoutGroup.childControlWidth = true;
            layoutGroup.childScaleWidth = true;
            layoutGroup.CalculateLayoutInputHorizontal();
            layoutGroup.SetAssetDirty();
            LayoutRebuilder.MarkLayoutForRebuild(layoutGroup.GetComponent<RectTransform>());
            MenuWideElement x = new(buttonLabeled);
            this.Add(x);*/
            return buttonLabeled;
        }

        public ChoiceElement<bool> ToggleElement(string label,  ConfigEntryBase configEntry, string description = "", FontSizes fontSizes = FontSizes.Medium)
        {
            ChoiceElement<bool> element = new(label, ChoiceModels.ForBool(), description);
            element.SynchronizeRawWith(configEntry);
            element.SetFontSizes(fontSizes);
            Add(element);
            return element;
        }

        public DoubleSliderElement<float> FloatSlider(string label, ConfigEntryBase configEntry, string description = "", FontSizes fontSize = FontSizes.Small)
        {
            Label(label, fontSize: FontSizes.Medium);
            var (min, max) = configEntry.Description.AcceptableValues is AcceptableValueRange<float> range ? Tuple.Create(range.MinValue, range.MaxValue) : Tuple.Create(0f, 100f);
            int ticks = (int)Math.Round(max - min) + 3;
            LinearFloatSliderModel model = SliderModels.ForFloats(min, max, ticks);
            DoubleSliderElement<float> slider = new("Minimum Percent", model);
            slider.SetFontSizes(fontSize);
            slider.SynchronizeRawWith(configEntry);
            /*GameObject x = UnityEngine.Object.Instantiate(descriptionDummy);
            x.name = "Description";
            x.GetComponent<Text>().text = description;
            RectTransform y = x.GetComponent<RectTransform>();
            y.SetParent(slider.Slider.gameObject.transform, false);
            y.anchoredPosition = new Vector2(0f, -60f);*/
            
            Add(slider);
            return slider;
        }

        public TextInput<int> IntInput(string label, ConfigEntryBase configEntry, string description = "")
        {
            ParserTextModel<int> model = configEntry.Description.AcceptableValues is AcceptableValueRange<int> range ? TextModels.ForIntegers(range.MinValue, range.MaxValue) : TextModels.ForIntegers();
            TextInput<int> intInput = new(label, model, description);
            intInput.SynchronizeRawWith(configEntry);
            intInput.Model.SetValue((int)configEntry.BoxedValue);
            Add(intInput);
            return intInput;
        }

        public static AbstractMenuScreen BuildSubMenu(string title, Dictionary<string, ConfigEntryBase> settings)
        {
            Cute_Randomizer_MenuBuilder screenBuilder = new(title);

            foreach (var setting in settings.Values)
            {
                screenBuilder.ElementBuilder(setting);
            }

            return screenBuilder.Build();
        }

        public void ElementBuilder(ConfigEntryBase entry)
        {
            var type = entry.SettingType.IsEnum ? "Enum" : entry.SettingType.Name;
            switch (type)
            {
                case nameof(Boolean):
                    ToggleElement(entry.LabelName(), entry, entry.Description.Description);
                    break;
                case nameof(Int32):
                    IntInput(entry.LabelName(), entry, entry.Description.Description);
                    break;
                case nameof(FloatRange):
                    FloatSlider(entry.LabelName(), entry, entry.Description.Description);
                    break;
                case nameof(IntRange):
                case "Enum":
                default:
                    Label(entry.LabelName(), Color.magenta);
                    break;
            }
        }
    }

    public class DoubleSliderElement<T> : SliderElement<T>
    {
        private Slider slider2;
        public Slider Slider2 => slider2;
        private Text labelText2;
        public Text LabelText2 
        {
            get => labelText2;
            set => labelText2 = value; 
        }
        private Text valueText2;

        public DoubleSliderElement(LocalizedText label, SliderModel<T> model) : base(label, model)
        {
            slider2 = UnityEngine.Object.Instantiate(Slider.gameObject).GetComponent<Slider>();
            slider2.name= "Slider 2";

            RectTransform slider2Transform = slider2.gameObject.GetComponent<RectTransform>();
            slider2Transform.SetParent(Slider.transform, false);
            slider2Transform.anchoredPosition = new Vector2(0f, -80f);

            labelText2 = slider2.transform.Find("Menu Option Label").GetComponent<Text>();
            valueText2 = slider2.transform.Find("Value").GetComponent<Text>();
        }

        public override void SetFontSizes(FontSizes fontSizes)
        {
            base.SetFontSizes(fontSizes);
            valueText2.fontSize = fontSizes.SliderSize();
            labelText2.fontSize = fontSizes.LabelSize();
        }
    }

    /*internal class WiderSliderElement<T> : SliderElement<T>
    {
        public WiderSliderElement(LocalizedText label, SliderModel<T> model) : base(label, model)
        {
            Vector2
                middleright = new(1, 0.5f),
                middleleft = new(0, 0.5f),
                uppercenter = new(0.5f, 1),
                lowercenter = new(0.5f, 0),
                center = Vector2.one * 0.5f;
            RectTransform
                Slider = AsRect(RectTransform.Find("Slider")),
                MenuOptionLabel = AsRect(Slider.Find("Menu Option Label")),
                CursorHotspot = AsRect(Slider.Find("CursorHotspot")),
                CursorLeft = AsRect(CursorHotspot.Find("CursorLeft")),
                CursorRight = AsRect(CursorHotspot.Find("CursorRight")),
                FillArea = AsRect(Slider.Find("Fill Area")),
                HandleSlideArea = AsRect(Slider.Find("Handle Slide Area")),
                Value = AsRect(Slider.Find("Value"));

            RectTransform.sizeDelta = RectTransform.sizeDelta with { x = 720 };
            SetAnchors(RectTransform, center);
            RectTransform.anchoredPosition = RectTransform.anchoredPosition with { x = 0 };

            SetAnchors(Slider, middleright);
            Slider.anchoredPosition = Vector2.zero;

            MenuOptionLabel.anchorMin = Vector2.zero;
            MenuOptionLabel.anchorMax = Vector2.one;
            MenuOptionLabel.pivot = middleleft;
            MenuOptionLabel.anchoredPosition = new Vector2(-(2 * RectTransform.offsetMax.x) - 91, 0);

            CursorHotspot.anchorMax = uppercenter;
            CursorHotspot.anchorMin = lowercenter;
            CursorHotspot.anchoredPosition = new(RectTransform.offsetMin.x, 0);
            CursorHotspot.sizeDelta = new Vector2(1000 + RectTransform.offsetMax.x, 0);

            SetAnchors(CursorLeft, middleleft);
            CursorLeft.anchoredPosition = Vector2.zero;

            SetAnchors(CursorRight, middleright);
            CursorRight.anchoredPosition = Vector2.zero;

            ValueText.alignByGeometry = true;
        }

        internal static RectTransform AsRect(Transform t) => (RectTransform)t;
        internal static void SetAnchors(Transform t, Vector2 anchor) => AsRect(t).anchorMax = AsRect(t).anchorMax = anchor;
    }*/
}