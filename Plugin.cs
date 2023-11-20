using BaboonAPI.Hooks.Initializer;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TootTallyCore.Utils.TootTallyModules;
using TootTallySettings;
using UnityEngine;

namespace TootTallyCustomNote
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    [BepInDependency("TootTallyCore", BepInDependency.DependencyFlags.HardDependency)]
    [BepInDependency("TootTallySettings", BepInDependency.DependencyFlags.HardDependency)]
    public class Plugin : BaseUnityPlugin, ITootTallyModule
    {
        public static Plugin Instance;

        private const string CONFIG_NAME = "CustomNote.cfg";
        private const string NOTE_CONFIG_FIELD = "CustomNote";
        public static string NOTES_FOLDER_PATH = "CustomNotes";
        public const string DEFAULT_NOTENAME = "Default";
        public Options option;
        private Harmony _harmony;
        public ConfigEntry<bool> ModuleConfigEnabled { get; set; }
        public bool IsConfigInitialized { get; set; }

        //Change this name to whatever you want
        public string Name { get => "Custom Note"; set => Name = value; }

        public static TootTallySettingPage settingPage;

        public static void LogInfo(string msg) => Instance.Logger.LogInfo(msg);
        public static void LogError(string msg) => Instance.Logger.LogError(msg);

        private void Awake()
        {
            if (Instance != null) return;
            Instance = this;
            _harmony = new Harmony(Info.Metadata.GUID);

            GameInitializationEvent.Register(Info, TryInitialize);
        }

        private void TryInitialize()
        {
            // Bind to the TTModules Config for TootTally
            ModuleConfigEnabled = TootTallyCore.Plugin.Instance.Config.Bind("Modules", "Custom Note", true, "Enable Custom Note Module");
            TootTallyModuleManager.AddModule(this);
            TootTallySettings.Plugin.Instance.AddModuleToSettingPage(this);
        }

        public void LoadModule()
        {
            string configPath = Path.Combine(Paths.BepInExRootPath, "config/");
            ConfigFile config = new ConfigFile(configPath + CONFIG_NAME, true);
            option = new Options()
            {
                NoteName = config.Bind(NOTE_CONFIG_FIELD, nameof(option.NoteName), DEFAULT_NOTENAME),
                NoteHeadSize = config.Bind(NOTE_CONFIG_FIELD, nameof(option.NoteHeadSize), 1f, "Size of the start and end note circles"),
                NoteBodySize = config.Bind(NOTE_CONFIG_FIELD, nameof(option.NoteBodySize), 1f, "Size of the note line"),
                RandomNoteColor = config.Bind(NOTE_CONFIG_FIELD, nameof(option.RandomNoteColor), false, "Randomize all the colors of the notes"),
                OverwriteNoteColor = config.Bind(NOTE_CONFIG_FIELD, nameof(option.OverwriteNoteColor), false, "Make the note color consistent"),
                NoteColorStart = config.Bind(NOTE_CONFIG_FIELD, nameof(option.NoteColorStart), Color.white),
                NoteColorEnd = config.Bind(NOTE_CONFIG_FIELD, nameof(option.NoteColorEnd), Color.black),
            };

            TryMigrateFolder("CustomNotes");

            settingPage = TootTallySettingsManager.AddNewPage("Custom Note", "Custom Note", 40f, new Color(0,0,0,0));
            CreateDropdownFromFolder(NOTES_FOLDER_PATH, option.NoteName, DEFAULT_NOTENAME);

            var headSlider = settingPage.AddSlider("NoteHeadSizeSlider", 0f, 5f, 250f, "Note Head Size", option.NoteHeadSize, false);
            var bodySlider = settingPage.AddSlider("NoteBodySizeSlider", 0f, 5f, 250f, "Note Body Size", option.NoteBodySize, false);

            settingPage.AddButton("ResetSliders", new Vector2(160, 80), "Reset", () =>
            {
                headSlider.slider.value = 1f;
                bodySlider.slider.value = 1f;
            });

            settingPage.AddToggle("RandomNoteColor", option.RandomNoteColor);
            settingPage.AddToggle("OverwriteNoteColor", option.OverwriteNoteColor, OnToggleValueChange);
            if (option.OverwriteNoteColor.Value) OnToggleValueChange(true);

            _harmony.PatchAll(typeof(CustomNotePatches));
            LogInfo($"Module loaded!");
        }

        public void UnloadModule()
        {
            _harmony.UnpatchSelf();
            settingPage.Remove();
            LogInfo($"Module unloaded!");
        }

        public void TryMigrateFolder(string folderName)
        {
            string targetFolderPath = Path.Combine(Paths.BepInExRootPath, folderName);
            if (!Directory.Exists(targetFolderPath))
            {
                string sourceFolderPath = Path.Combine(Path.GetDirectoryName(Plugin.Instance.Info.Location), folderName);
                LogInfo($"{folderName} folder not found. Attempting to move folder from " + sourceFolderPath + " to " + targetFolderPath);
                if (Directory.Exists(sourceFolderPath))
                    Directory.Move(sourceFolderPath, targetFolderPath);
                else
                {
                    LogError($"Source {folderName} Folder Not Found. Cannot Create {folderName} Folder. Download the module again to fix the issue.");
                    return;
                }
            }
        }

        public void CreateDropdownFromFolder(string folderName, ConfigEntry<string> config, string defaultValue)
        {
            var folderNames = new List<string> { defaultValue };
            var folderPath = Path.Combine(Paths.BepInExRootPath, folderName);
            if (Directory.Exists(folderPath))
            {
                var directories = Directory.GetDirectories(folderPath).ToList();
                directories.ForEach(d =>
                {
                    if (!d.Contains("TEMPALTE"))
                        folderNames.Add(Path.GetFileNameWithoutExtension(d));
                });
            }
            settingPage.AddLabel(folderName, folderName, 24, TMPro.FontStyles.Normal, TMPro.TextAlignmentOptions.BottomLeft);
            settingPage.AddDropdown($"{folderName}Dropdown", config, folderNames.ToArray());
        }

        public void OnToggleValueChange(bool value)
        {
            if (value)
            {
                settingPage.AddLabel("Note Start Color");
                settingPage.AddColorSliders("NoteStart", "Note Start Color", option.NoteColorStart);
                settingPage.AddLabel("Note End Color");
                settingPage.AddColorSliders("NoteEnd", "Note End Color", option.NoteColorEnd);
            }
            else
            {
                settingPage.RemoveSettingObjectFromList("Note Start Color");
                settingPage.RemoveSettingObjectFromList("NoteStart");
                settingPage.RemoveSettingObjectFromList("Note End Color");
                settingPage.RemoveSettingObjectFromList("NoteEnd");
            }

        }

        public static class CustomNotePatches
        {
            [HarmonyPatch(typeof(HomeController), nameof(HomeController.tryToSaveSettings))]
            [HarmonyPostfix]
            public static void OnSettingsChange()
            {
               CustomNote.ResolvePresets(null);
            }

            [HarmonyPatch(typeof(HomeController), nameof(HomeController.Start))]
            [HarmonyPostfix]
            public static void OnHomeStartLoadTexture()
            {
                CustomNote.ResolvePresets(null);
            }

            [HarmonyPatch(typeof(GameController), nameof(GameController.buildNotes))]
            [HarmonyPrefix]
            public static void PatchCustorTexture(GameController __instance)
            {
                CustomNote.ResolvePresets(__instance);
                CustomNote.ApplyNoteResize(__instance);

                if (Instance.option.OverwriteNoteColor.Value)
                    CustomNote.ApplyColor(__instance);
            }

            [HarmonyPatch(typeof(NoteDesigner), nameof(NoteDesigner.setColorScheme))]
            [HarmonyPrefix]
            public static void PatchCursorColorRandom(ref float col_r, ref float col_g, ref float col_b, ref float col_r2, ref float col_g2, ref float col_b2)
            {
                if (Instance.option.RandomNoteColor.Value)
                    CustomNote.ApplyRandomColor(ref col_r, ref col_g, ref col_b, ref col_r2, ref col_g2, ref col_b2);
            }

            [HarmonyPatch(typeof(GameController), nameof(GameController.buildNotes))]
            [HarmonyPostfix]
            public static void FixEndNotePosition(GameController __instance)
            {
                CustomNote.FixNoteEndPosition(__instance);
            }
        }

        public class Options
        {
            public ConfigEntry<string> NoteName { get; set; }
            public ConfigEntry<float> NoteHeadSize { get; set; }
            public ConfigEntry<float> NoteBodySize { get; set; }
            public ConfigEntry<bool> RandomNoteColor { get; set; }
            public ConfigEntry<bool> OverwriteNoteColor { get; set; }
            public ConfigEntry<Color> NoteColorStart { get; set; }
            public ConfigEntry<Color> NoteColorEnd { get; set; }
        }
    }
}