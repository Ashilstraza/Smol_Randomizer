using BepInEx;
using Cute_Randomizer.Randomizers;
using Silksong.ModMenu.Elements;
using Silksong.ModMenu.Models;
using Silksong.ModMenu.Plugin;
using Silksong.ModMenu.Screens;
using System;
using UnityEngine;
using UnityEngine.UI;

namespace Cute_Randomizer.Settings
{
    [BepInDependency("org.silksong-modding.modmenu")]
    [BepInPlugin(Cute_Rando_Core.GUID + ".menu", Cute_Rando_Core.MODNAME + " Menu", Cute_Rando_Core.VERSION)]
    public class SettingMenu : BaseUnityPlugin, IModMenuInterface, IModMenuCustomMenu
    {
        public AbstractMenuScreen BuildCustomMenu()
        {
            Cutebold_Listing screenBuilder = new(ModMenuName());
            screenBuilder.VerticalSpacing = SpacingConstants.VSPACE_SMALL;
            screenBuilder.Label("Main Settings", FontSizes.Medium);
            screenBuilder.SubMenuButton(BuildSubMenu("Sub Menu"));
            screenBuilder.Label("World Objects", FontSizes.Medium);
            screenBuilder.Button("Export World Objects", delegate
            {
                Console.WriteLine("Exporting World Objects");
                Basic_Item_Rando.ExportWorldObjectsFile();
            });
            screenBuilder.Button("Import World Objects", delegate
            {
                Console.WriteLine("Import World Objects");
                Basic_Item_Rando.ImportWorldObjectsFile();
            });

            return screenBuilder.Build();
        }

        public AbstractMenuScreen BuildSubMenu(string title)
        {
            Cutebold_Listing screenBuilder = new(title);
            screenBuilder.VerticalSpacing = SpacingConstants.VSPACE_SMALL;


            return screenBuilder.Build();
        }

        public LocalizedText ModMenuName() => Cute_Rando_Core.MODNAME;
    }

    internal class Cutebold_Listing(LocalizedText title, int pageSize = 8) : PaginatedMenuScreenBuilder(title, pageSize)
    {
        internal TextLabel Label(string label, FontSizes fontSize = FontSizes.Small, bool add = true)
        {
            TextLabel textLabel = new(label);
            textLabel.SetFontSizes(fontSize);
            if (add) Add(textLabel);
            return textLabel;
        }

        internal TextButton Button(string label, Action onClick, string description = "", FontSizes fontSize = FontSizes.Small, bool add = true)
        {
            TextButton button = new(label, description);
            button.SetFontSizes(fontSize);
            button.OnSubmit = (Action)Delegate.Combine(button.OnSubmit, onClick);
            if (add) Add(button);
            return button;
        }

        internal TextButton SubMenuButton(AbstractMenuScreen subMenu)
        {
            TextButton button = new(subMenu);
            Add(button);
            return button;
        }

        internal GameObject ButtonLabeled(string label, string buttonText, Action onClick, string description = "", FontSizes fontSize = FontSizes.Small)
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

        internal void Checkbox()
        {

        }

        internal void CheckboxLabeled()
        {

        }

        internal void SectionLabel()
        {

        }

        internal void Dropdown()
        {

        }

        internal void DropdownLabeled()
        {

        }

        internal SliderElement<float> FloatSlider(string label, FloatRange range, FontSizes fontSize = FontSizes.Small)
        {
            var (min, max) = range.AsTuple();
            int ticks = (int)Math.Round(min - max) + 3;
            LinearFloatSliderModel model = SliderModels.ForFloats(min, max, ticks);
            SliderElement<float> slider = new(label, model);
            slider.SetFontSizes(fontSize);
            Add(slider);
            return slider;
        }

        internal void SliderLabeled()
        {

        }
    }
}
