using BeatSaberMarkupLanguage;
using BeatSaberMarkupLanguage.MenuButtons;
using BeatSaberMarkupLanguage.Util;
using IPA;
using IPA.Utilities.Async;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using TournamentAssistant.Behaviors;
using TournamentAssistant.Interop;
using TournamentAssistant.UI.FlowCoordinators;
using TournamentAssistant.UnityUtilities;
using TournamentAssistant.Utilities;
using TournamentAssistantShared;
using TournamentAssistantShared.Models;
using TournamentAssistantShared.SimpleJSON;
using TournamentAssistantShared.Utilities;
using UnityEngine;
using UnityEngine.SceneManagement;
using Config = TournamentAssistantShared.Config;
using Random = System.Random;

/**
 * Created by Moon on 8/5/2019
 * Base plugin class for the TournamentAssistant plugin
 * Intended to be the player-facing UI for tournaments, where
 * players' games can be handled by their match coordinators
 */

namespace TournamentAssistant
{
    [Plugin(RuntimeOptions.DynamicInit)]
    public class Plugin : IDisposable
    {
        // Constants
        public string Name => Constants.NAME;
        public string Version => Constants.PLUGIN_VERSION;

        // Instances
        public static Config config = new Config();

        private MenuButton menuButton;

        // Toggles
        public static bool UseSync { get; set; }
        public static bool UseFloatingScoreboard { get; set; }
        public static bool DisableFail { get; set; }
        public static bool DisablePause { get; set; }
        public static bool QualifierDisablePause { get; set; }
        public static User.PlayStates PreviousPlayState { get; set; }

        // FlowCoordinators
        private MainFlowCoordinator _mainFlowCoordinator;
        private TournamentSelectionCoordinator _tournamentSelectionCoordinator;

        // Localization
        private static Random _random = new Random();
        private static JSONNode parsedLocalization;
        private static string[] quotes;
        private static Dictionary<string, string> translations = new Dictionary<string, string>();

        public static string GetLocalized(string targetString)
        {
            try
            {
                if (!translations.ContainsKey(targetString))
                {
                    if (parsedLocalization == null)
                    {
                        string name = CultureInfo.CurrentUICulture.Name;
                        string[] parts = name.Split('-');
                        string iso639 = parts[0];
                        string bcp47 = parts.Length >= 2 ? parts[1] : string.Empty;

                        // Small random chance of funny translations
                        if (_random.Next(999) == 1)
                        {
                            iso639 = "owo";
                        }

                        using (var reader = new StreamReader(Assembly.GetCallingAssembly().GetManifestResourceStream($"TournamentAssistant.Localization.{iso639}.json")))
                        {
                            parsedLocalization = JSON.Parse(reader.ReadToEnd());
                        }
                    }

                    translations[targetString] = parsedLocalization[targetString];
                }

                if (translations[targetString] == null)
                {
                    throw new Exception($"Translation: {targetString} not found");
                }

                return translations[targetString];
            }
            catch
            {
                using (var reader = new StreamReader(Assembly.GetCallingAssembly().GetManifestResourceStream($"TournamentAssistant.Localization.en.json")))
                {
                    var localization = JSON.Parse(reader.ReadToEnd());

                    if (string.IsNullOrEmpty(localization[targetString]))
                    {
                        return targetString;
                    }

                    return localization[targetString];
                }
            }
        }

        public static string[] GetQuotes()
        {
            try
            {
                if (quotes == null)
                {
                    if (parsedLocalization == null)
                    {
                        string name = CultureInfo.CurrentUICulture.Name;
                        string[] parts = name.Split('-');
                        string iso639 = parts[0];
                        string bcp47 = parts.Length >= 2 ? parts[1] : string.Empty;

                        using (var reader = new StreamReader(Assembly.GetCallingAssembly().GetManifestResourceStream($"TournamentAssistant.Localization.{iso639}.json")))
                        {
                            parsedLocalization = JSON.Parse(reader.ReadToEnd());
                        }
                    }

                    quotes = parsedLocalization["quotes"].AsArray.Children.Select(x => x.ToString()).ToArray();
                }

                return quotes;
            }
            catch
            {
                using (var reader = new StreamReader(Assembly.GetCallingAssembly().GetManifestResourceStream($"TournamentAssistant.Localization.en.json")))
                {
                    var localization = JSON.Parse(reader.ReadToEnd());
                    return localization["quotes"].AsArray.Children.Select(x => x.ToString()).ToArray();
                }
            }
        }

        public Plugin()
        {
            menuButton = new MenuButton("TournamentAssistant", MenuButtonPressed);
        }

        [OnEnable]
        public void OnEnable()
        {
            SongUtils.OnEnable();
            MainMenuAwaiter.MainMenuInitializing += () =>
            {
                ZenjectSingleton<MenuButtons>.Instance.RegisterButton(menuButton);
            };

            var scoreSaber = IPA.Loader.PluginManager.GetPluginFromId("ScoreSaber");
            if (scoreSaber != null)
            {
                InitScoreSaber();
            }

            // This behaviour stays always
            new GameObject("ScreenOverlay").AddComponent<ScreenOverlay>();

            Updater.DeleteUpdater();

            SceneManager.sceneLoaded += SceneManager_sceneLoaded;
            SceneManager.sceneUnloaded += SceneManager_sceneUnloaded;
        }

        public static string[] GetPluginList()
        {
            return IPA.Loader.PluginManager.EnabledPlugins.Select(x => x.Id).ToArray();
        }

        private void SceneManager_sceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == "GameCore")
            {
                MidPlayModifiers.GameSceneLoaded();
            }
        }

        private void SceneManager_sceneUnloaded(Scene scene)
        {
            if (scene.name == "GameCore")
            {
                MidPlayModifiers.GameSceneUnloaded();
            }
        }

        // Broken off so that if scoresaber isn't installed, we don't try to load anything from it
        private static void InitScoreSaber()
        {
            ScoreSaberInterop.InitAndSignIn();
        }

        private void MenuButtonPressed()
        {
            _mainFlowCoordinator = Resources.FindObjectsOfTypeAll<MainFlowCoordinator>().First();
            _tournamentSelectionCoordinator = BeatSaberUI.CreateFlowCoordinator<TournamentSelectionCoordinator>();
            _tournamentSelectionCoordinator.DidFinishEvent += tournamentSelectionCoordinator_DidFinishEvent;

            _mainFlowCoordinator.InvokeMethod("PresentFlowCoordinatorOrAskForTutorial", _tournamentSelectionCoordinator);
        }

        private void tournamentSelectionCoordinator_DidFinishEvent()
        {
            _tournamentSelectionCoordinator.DidFinishEvent -= tournamentSelectionCoordinator_DidFinishEvent;
            _mainFlowCoordinator.DismissFlowCoordinator(_tournamentSelectionCoordinator);
        }

        public static bool IsInMenu() => UnityMainThreadTaskScheduler.Factory.StartNew(() => SceneManager.GetActiveScene().name == "MainMenu").Result;

        public void Dispose()
        {
            if (MenuButtons.Instance != null)
            {
                MenuButtons.Instance.UnregisterButton(menuButton);
            }
        }
    }
}