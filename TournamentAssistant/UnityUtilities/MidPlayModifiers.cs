using HarmonyLib;
using IPA.Utilities;
using Libraries.HM.HMLib.VR;
using System.Linq;
using System.Threading.Tasks;
using TournamentAssistantShared;
using TournamentAssistantShared.Utilities;
using UnityEngine;
using UnityEngine.XR;
using Logger = TournamentAssistantShared.Logger;

namespace TournamentAssistant.UnityUtilities
{
    public class MidPlayModifiers
    {
        static readonly Harmony _harmony = new("TA:MidPlayModifiers");

        static AudioTimeSyncController _audioSyncTimeController;

        static bool _gameSceneLoaded = false;

        static bool _willInvertColors = false;
        static bool _willInvertHands = false;

        static bool _invertColors = false;
        static bool _invertHaptics = false;
        static bool _invertHands = false;

        static bool _saberColorsNeedSwitching = false;
        static bool _switchingAtStartOfMap = false;
        static bool _switchingAtEndOfMap = false;
        static int _numberOfLines;

        public static bool InvertColors
        {
            get { return _invertColors; }
            set
            {
                if (value == _invertColors)
                {
                    return;
                }

                BS_Utils.Gameplay.ScoreSubmission.DisableSubmission(Constants.NAME);

                if (!_gameSceneLoaded)
                {
                    _willInvertColors = value;
                    return;
                }

                if (_switchingAtStartOfMap)
                {
                    if (value)
                    {
                        SwapSaberColors();
                    }
                    else
                    {
                        _invertHaptics = !_invertHaptics;
                    }
                }
                else
                {
                    _saberColorsNeedSwitching = !_saberColorsNeedSwitching;
                }

                if (value)
                {
                    Logger.Info($"Switching to inverted colors");
                }
                else
                {
                    Logger.Info($"Switching back to normal colors");
                }
                _invertColors = value;
            }
        }

        public static bool InvertHands
        {
            get { return _invertHands; }
            set
            {
                if (value == _invertHands)
                {
                    return;
                }

                BS_Utils.Gameplay.ScoreSubmission.DisableSubmission(Constants.NAME);

                if (!_gameSceneLoaded)
                {
                    _willInvertHands = value;
                    return;
                }

                if (value)
                {
                    Logger.Info($"Switching to alternate handedness");

                    Logger.Info($"Harmony patching {nameof(BeatmapObjectSpawnController)}.{nameof(BeatmapObjectSpawnController.HandleNoteDataCallback)}");
                    _harmony.Patch(
                        AccessTools.Method(typeof(BeatmapObjectSpawnController), nameof(BeatmapObjectSpawnController.HandleNoteDataCallback)),
                        new HarmonyMethod(AccessTools.Method(typeof(MidPlayModifiers), nameof(HandleNoteDataCallbackPrefix_Handedness)))
                    );

                    Logger.Info($"Harmony patching {nameof(BeatmapObjectSpawnController)}.{nameof(BeatmapObjectSpawnController.HandleObstacleDataCallback)}");
                    _harmony.Patch(
                        AccessTools.Method(typeof(BeatmapObjectSpawnController), nameof(BeatmapObjectSpawnController.HandleObstacleDataCallback)),
                        new HarmonyMethod(AccessTools.Method(typeof(MidPlayModifiers), nameof(HandleObstacleDataCallbackPrefix_Handedness)))
                    );

                    Logger.Info($"Harmony patching {nameof(BeatmapObjectSpawnController)}.{nameof(BeatmapObjectSpawnController.HandleSliderDataCallback)}");
                    _harmony.Patch(
                        AccessTools.Method(typeof(BeatmapObjectSpawnController), nameof(BeatmapObjectSpawnController.HandleSliderDataCallback)),
                        new HarmonyMethod(AccessTools.Method(typeof(MidPlayModifiers), nameof(HandleSliderDataCallbackPrefix_Handedness)))
                    );

                    Logger.Info($"Harmony patching {nameof(BeatmapObjectSpawnController)}.{nameof(BeatmapObjectSpawnController.HandleSpawnRotationCallback)}");
                    _harmony.Patch(
                        AccessTools.Method(typeof(BeatmapObjectSpawnController), nameof(BeatmapObjectSpawnController.HandleSpawnRotationCallback)),
                        new HarmonyMethod(AccessTools.Method(typeof(MidPlayModifiers), nameof(HandleSpawnRotationCallbackPrefix_Handedness)))
                    );
                }
                else
                {
                    Logger.Info($"Switching back to normal handedness");

                    Logger.Info($"Harmony unpatching {nameof(BeatmapObjectSpawnController)}.{nameof(BeatmapObjectSpawnController.HandleNoteDataCallback)}");
                    _harmony.Unpatch(
                          AccessTools.Method(typeof(BeatmapObjectSpawnController), nameof(BeatmapObjectSpawnController.HandleNoteDataCallback)),
                          AccessTools.Method(typeof(MidPlayModifiers), nameof(HandleNoteDataCallbackPrefix_Handedness))
                    );

                    Logger.Info($"Harmony unpatching {nameof(BeatmapObjectSpawnController)}.{nameof(BeatmapObjectSpawnController.HandleObstacleDataCallback)}");
                    _harmony.Unpatch(
                          AccessTools.Method(typeof(BeatmapObjectSpawnController), nameof(BeatmapObjectSpawnController.HandleObstacleDataCallback)),
                          AccessTools.Method(typeof(MidPlayModifiers), nameof(HandleObstacleDataCallbackPrefix_Handedness))
                    );

                    Logger.Info($"Harmony unpatching {nameof(BeatmapObjectSpawnController)}.{nameof(BeatmapObjectSpawnController.HandleSliderDataCallback)}");
                    _harmony.Unpatch(
                          AccessTools.Method(typeof(BeatmapObjectSpawnController), nameof(BeatmapObjectSpawnController.HandleSliderDataCallback)),
                          AccessTools.Method(typeof(MidPlayModifiers), nameof(HandleSliderDataCallbackPrefix_Handedness))
                    );

                    Logger.Info($"Harmony unpatching {nameof(BeatmapObjectSpawnController)}.{nameof(BeatmapObjectSpawnController.HandleSpawnRotationCallback)}");
                    _harmony.Unpatch(
                          AccessTools.Method(typeof(BeatmapObjectSpawnController), nameof(BeatmapObjectSpawnController.HandleSpawnRotationCallback)),
                          AccessTools.Method(typeof(MidPlayModifiers), nameof(HandleSpawnRotationCallbackPrefix_Handedness))
                    );
                }
                _invertHands = value;
            }
        }

        private static int NumberOfLines
        {
            get
            {
                if (_numberOfLines == 0)
                {
                    var _gameplayCoreInstaller = Resources.FindObjectsOfTypeAll<GameplayCoreInstaller>().First();
                    _numberOfLines = _gameplayCoreInstaller.GetField<GameplayCoreSceneSetupData>("_sceneSetupData").transformedBeatmapData.numberOfLines;
                }
                return _numberOfLines;
            }
        }

        static void HandleNoteDataCallbackPrefix_Colors(ref NoteData noteData)
        {
            if (_invertColors)
            {
                noteData = noteData.CopyWith(colorType: noteData.colorType.Opposite());
            }

            if (_saberColorsNeedSwitching)
            {
                _saberColorsNeedSwitching = false;

                // Note the extra 50ms subtraction to ensure the sabers swap before the first switched note
                Task.Delay((int)((noteData.time - _audioSyncTimeController.songTime) * 1000) - 100).ContinueWith(t => SwapSaberColors());
            }
        }

        static void PlayHapticFeedback_Colors(ref XRNode node, HapticPresetSO hapticPreset)
        {
            if (_invertHaptics)
            {
                if (node == XRNode.RightHand)
                {
                    node = XRNode.LeftHand;
                }
                else if (node == XRNode.LeftHand)
                {
                    node = XRNode.RightHand;
                }
            }
        }

        // Moon's note: if this saber Type swapping doesn't work, we can patch HandleCut and swap it there
        static void SwapSaberColors()
        {
            // Custom sabers can cause this to fail, let's not make that a death sentence
            try
            {
                _invertHaptics = !_invertHaptics;

                var saberManager = Resources.FindObjectsOfTypeAll<SaberManager>().First();

                if (saberManager != null)
                {
                    var leftSaberType = saberManager.leftSaber.GetField<SaberTypeObject>("_saberType");
                    var rightSaberType = saberManager.rightSaber.GetField<SaberTypeObject>("_saberType");

                    saberManager.leftSaber.SetField("_saberType", rightSaberType);
                    saberManager.rightSaber.SetField("_saberType", leftSaberType);

                    // First two are actual sabers, the third... I have no idea. Headset maybe?
                    foreach (var saberModelController in Resources.FindObjectsOfTypeAll<SaberModelController>().Take(2))
                    {
                        foreach (var setSaberGlowColor in saberModelController.GetField<SetSaberGlowColor[]>("_setSaberGlowColors"))
                        {
                            setSaberGlowColor.saberType = setSaberGlowColor.GetField<SaberType>("_saberType") == SaberType.SaberA ? SaberType.SaberB : SaberType.SaberA;
                        }
                    }
                }
            }
            catch { }
        }

        static void HandleNoteDataCallbackPrefix_Handedness(ref NoteData noteData)
        {
            noteData.Mirror(NumberOfLines);
        }

        static void HandleObstacleDataCallbackPrefix_Handedness(ref ObstacleData obstacleData)
        {
            obstacleData.Mirror(NumberOfLines);
        }

        static void HandleSliderDataCallbackPrefix_Handedness(ref SliderData sliderNoteData)
        {
            sliderNoteData.Mirror(NumberOfLines);
        }

        static void HandleSpawnRotationCallbackPrefix_Handedness(ref SpawnRotationBeatmapEventData beatmapEventData)
        {
            beatmapEventData.Mirror();
        }

        public static void GameSceneLoaded()
        {
            _gameSceneLoaded = true;

            _audioSyncTimeController = Resources.FindObjectsOfTypeAll<AudioTimeSyncController>().FirstOrDefault();

            // Note data color patch is handled here so that we can get the next-note time when switching back from inverted colors
            Logger.Info($"Harmony patching {nameof(BeatmapObjectSpawnController)}.{nameof(BeatmapObjectSpawnController.HandleNoteDataCallback)}");
            _harmony.Patch(
                AccessTools.Method(typeof(BeatmapObjectSpawnController), nameof(BeatmapObjectSpawnController.HandleNoteDataCallback)),
                new HarmonyMethod(AccessTools.Method(typeof(MidPlayModifiers), nameof(HandleNoteDataCallbackPrefix_Colors)))
            );

            Logger.Info($"Harmony patching {nameof(HapticFeedbackController)}.{nameof(HapticFeedbackController.PlayHapticFeedback)}");
            _harmony.Patch(
                AccessTools.Method(typeof(HapticFeedbackController), nameof(HapticFeedbackController.PlayHapticFeedback)),
                new HarmonyMethod(AccessTools.Method(typeof(MidPlayModifiers), nameof(PlayHapticFeedback_Colors)))
            );

            _switchingAtStartOfMap = true;
            InvertColors = _willInvertColors;
            InvertHands = _willInvertHands;
            _switchingAtStartOfMap = false;
        }

        public static void GameSceneUnloaded()
        {
            _gameSceneLoaded = false;

            _switchingAtEndOfMap = true;
            InvertColors = false;
            InvertHands = false;
            _switchingAtEndOfMap = false;

            _numberOfLines = 0;
            _saberColorsNeedSwitching = false;
            _audioSyncTimeController = null;

            // Note data color patch is handled here so that we can get the next-note time when switching back from inverted colors
            Logger.Info($"Harmony unpatching {nameof(BeatmapObjectSpawnController)}.{nameof(BeatmapObjectSpawnController.HandleNoteDataCallback)}");
            _harmony.Unpatch(
                  AccessTools.Method(typeof(BeatmapObjectSpawnController), nameof(BeatmapObjectSpawnController.HandleNoteDataCallback)),
                  AccessTools.Method(typeof(MidPlayModifiers), nameof(HandleNoteDataCallbackPrefix_Colors))
            );

            Logger.Info($"Harmony unpatching {nameof(BeatmapObjectSpawnController)}.{nameof(BeatmapObjectSpawnController.HandleNoteDataCallback)}");
            _harmony.Unpatch(
                  AccessTools.Method(typeof(HapticFeedbackController), nameof(HapticFeedbackController.PlayHapticFeedback)),
                  AccessTools.Method(typeof(MidPlayModifiers), nameof(PlayHapticFeedback_Colors))
            );
        }
    }
}
