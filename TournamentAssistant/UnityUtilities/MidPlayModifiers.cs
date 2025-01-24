using HarmonyLib;
using IPA.Utilities;
using Libraries.HM.HMLib.VR;
using System.Linq;
using TournamentAssistantShared.Utilities;
using UnityEngine;
using UnityEngine.XR;
using Logger = TournamentAssistantShared.Logger;

namespace TournamentAssistant.UnityUtilities
{
    public class MidPlayModifiers
    {
        static readonly Harmony _harmony = new("TA:MidPlayModifiers");

        static bool _invertColors = true;
        static bool _invertHands = true;
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

                if (value)
                {
                    Logger.Info($"Switching back to normal colors");

                    SwapSaberColors();

                    Logger.Info($"Harmony unpatching {nameof(BeatmapObjectSpawnController)}.HandleNoteDataCallback");
                    _harmony.Unpatch(
                          AccessTools.Method(typeof(BeatmapObjectSpawnController), "HandleNoteDataCallback"),
                          AccessTools.Method(typeof(MidPlayModifiers), nameof(HandleNoteDataCallbackPrefix_Colors))
                    );

                    Logger.Info($"Harmony unpatching {nameof(HapticFeedbackManager)}.PlayHapticFeedback");
                    _harmony.Unpatch(
                          AccessTools.Method(typeof(HapticFeedbackManager), "PlayHapticFeedback"),
                          AccessTools.Method(typeof(MidPlayModifiers), nameof(PlayHapticFeedback_Colors))
                    );
                }
                else
                {
                    Logger.Info($"Switching to inverted colors");

                    SwapSaberColors();

                    Logger.Info($"Harmony patching {nameof(BeatmapObjectSpawnController)}.HandleNoteDataCallback");
                    _harmony.Patch(
                        AccessTools.Method(typeof(BeatmapObjectSpawnController), "HandleNoteDataCallback"),
                        new HarmonyMethod(AccessTools.Method(typeof(MidPlayModifiers), nameof(HandleNoteDataCallbackPrefix_Colors)))
                    );

                    Logger.Info($"Harmony patching {nameof(HapticFeedbackManager)}.PlayHapticFeedback");
                    _harmony.Patch(
                        AccessTools.Method(typeof(HapticFeedbackManager), "PlayHapticFeedback"),
                        new HarmonyMethod(AccessTools.Method(typeof(MidPlayModifiers), nameof(PlayHapticFeedback_Colors)))
                    );
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

                if (value)
                {
                    Logger.Info($"Switching back to normal handedness");

                    Logger.Info($"Harmony unpatching {nameof(BeatmapObjectSpawnController)}.HandleNoteDataCallback");
                    _harmony.Unpatch(
                          AccessTools.Method(typeof(BeatmapObjectSpawnController), "HandleNoteDataCallback"),
                          AccessTools.Method(typeof(MidPlayModifiers), nameof(HandleNoteDataCallbackPrefix_Handedness))
                    );

                    Logger.Info($"Harmony unpatching {nameof(BeatmapObjectSpawnController)}.HandleObstacleDataCallback");
                    _harmony.Unpatch(
                          AccessTools.Method(typeof(BeatmapObjectSpawnController), "HandleObstacleDataCallback"),
                          AccessTools.Method(typeof(MidPlayModifiers), nameof(HandleObstacleDataCallbackPrefix_Handedness))
                    );

                    Logger.Info($"Harmony unpatching {nameof(BeatmapObjectSpawnController)}.HandleSliderDataCallback");
                    _harmony.Unpatch(
                          AccessTools.Method(typeof(BeatmapObjectSpawnController), "HandleSliderDataCallback"),
                          AccessTools.Method(typeof(MidPlayModifiers), nameof(HandleSliderDataCallbackPrefix_Handedness))
                    );

                    Logger.Info($"Harmony unpatching {nameof(BeatmapObjectSpawnController)}.HandleSpawnRotationCallback");
                    _harmony.Unpatch(
                          AccessTools.Method(typeof(BeatmapObjectSpawnController), "HandleSpawnRotationCallback"),
                          AccessTools.Method(typeof(MidPlayModifiers), nameof(HandleSpawnRotationCallbackPrefix_Handedness))
                    );
                }
                else
                {
                    Logger.Info($"Switching to alternate handedness");

                    Logger.Info($"Harmony patching {nameof(BeatmapObjectSpawnController)}.HandleNoteDataCallback");
                    _harmony.Patch(
                        AccessTools.Method(typeof(BeatmapObjectSpawnController), "HandleNoteDataCallback"),
                        new HarmonyMethod(AccessTools.Method(typeof(MidPlayModifiers), nameof(HandleNoteDataCallbackPrefix_Handedness)))
                    );

                    Logger.Info($"Harmony patching {nameof(BeatmapObjectSpawnController)}.HandleObstacleDataCallback");
                    _harmony.Patch(
                        AccessTools.Method(typeof(BeatmapObjectSpawnController), "HandleObstacleDataCallback"),
                        new HarmonyMethod(AccessTools.Method(typeof(MidPlayModifiers), nameof(HandleObstacleDataCallbackPrefix_Handedness)))
                    );

                    Logger.Info($"Harmony patching {nameof(BeatmapObjectSpawnController)}.HandleSliderDataCallback");
                    _harmony.Patch(
                        AccessTools.Method(typeof(BeatmapObjectSpawnController), "HandleSliderDataCallback"),
                        new HarmonyMethod(AccessTools.Method(typeof(MidPlayModifiers), nameof(HandleSliderDataCallbackPrefix_Handedness)))
                    );

                    Logger.Info($"Harmony patching {nameof(BeatmapObjectSpawnController)}.HandleSpawnRotationCallback");
                    _harmony.Patch(
                        AccessTools.Method(typeof(BeatmapObjectSpawnController), "HandleSpawnRotationCallback"),
                        new HarmonyMethod(AccessTools.Method(typeof(MidPlayModifiers), nameof(HandleSpawnRotationCallbackPrefix_Handedness)))
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
            noteData = noteData.CopyWith(colorType: noteData.colorType.Opposite());
        }

        static void PlayHapticFeedback_Colors(ref XRNode node, HapticPresetSO hapticPreset)
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

        // Moon's note: if this saber Type swapping doesn't work, we can patch HandleCut and swap it there
        static void SwapSaberColors()
        {
            // Custom sabers can cause this to fail, let's not make that a death sentence
            try
            {
                var saberManager = Resources.FindObjectsOfTypeAll<SaberManager>().First();
                if (saberManager != null)
                {
                    var saberManager = Resources.FindObjectsOfTypeAll<SaberManager>().First();

                    saberManager.leftSaber.SetField("_saberType", rightSaberType);
                    saberManager.rightSaber.SetField("_saberType", leftSaberType);

                    // First two are actual sabers, the third... I have no idea. Headset maybe?
                    foreach (var saberModelController in Resources.FindObjectsOfTypeAll<SaberModelController>().Take(2))
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

        public static void Reset()
        {
            InvertColors = false;
            InvertHands = false;
            _numberOfLines = 0;
        }
    }
}
