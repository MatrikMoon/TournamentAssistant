using HarmonyLib;
using System.Collections;
using System.Linq;
using TournamentAssistant.Utilities;
using TournamentAssistantShared;
using UnityEngine;
using Logger = TournamentAssistantShared.Logger;

/**
 * Created by Moon on 6/13/2020
 * ...and then subsequently left empty until 6/16/2020, 2:40 AM
 */

namespace TournamentAssistant.Behaviors
{
    class AntiFail : MonoBehaviour
    {
        public static AntiFail Instance { get; set; }

        static readonly Harmony _harmony = new("TA:AntiFail");

        private StandardLevelGameplayManager standardLevelGameplayManager;
        private GameSongController gameSongController;
        private GameEnergyCounter gameEnergyCounter;
        private BeatmapObjectManager beatmapObjectManager;

        private float _nextFrameEnergyChange;
        private float _oldObstacleEnergyDrainPerSecond;
        private bool _wouldHaveFailed = false;

        void Awake()
        {
            Instance = this;

            DontDestroyOnLoad(this); //Will actually be destroyed when the main game scene is loaded again, but unfortunately this 
                                     //object is created before the game scene loads, so we need to do this to prevent the game scene
                                     //load from destroying it

            standardLevelGameplayManager = Resources.FindObjectsOfTypeAll<StandardLevelGameplayManager>().First();

            StartCoroutine(DoOnLevelStart());
        }

        public virtual void LateUpdate()
        {
            if (gameEnergyCounter != null && gameEnergyCounter.GetField<SaberClashChecker>("_saberClashChecker").AreSabersClashing(out var _) && gameEnergyCounter.failOnSaberClash)
            {
                if (_wouldHaveFailed)
                {
                    gameEnergyCounter.InvokeMethod("ProcessEnergyChange", gameEnergyCounter.energy);
                }

                _nextFrameEnergyChange -= gameEnergyCounter.energy;
            }

            //Thanks to kObstacleEnergyDrainPerSecond becoming a constant in 1.20, we can no longer prevent the player from dying by sticking their head in a wall, so
            //instead I've chosen to just... Add back the health they lost. It's hacky, but it works.
            if (gameEnergyCounter != null && gameEnergyCounter.GetField<PlayerHeadAndObstacleInteraction>("_playerHeadAndObstacleInteraction").playerHeadIsInObstacle)
            {
                if (_wouldHaveFailed)
                {
                    gameEnergyCounter.InvokeMethod("ProcessEnergyChange", Time.deltaTime * _oldObstacleEnergyDrainPerSecond);
                }

                _nextFrameEnergyChange -= Time.deltaTime * _oldObstacleEnergyDrainPerSecond;
            }

            if (!Mathf.Approximately(_nextFrameEnergyChange, 0f))
            {
                AddEnergy(_nextFrameEnergyChange);
                _nextFrameEnergyChange = 0f;
            }
        }

        public IEnumerator DoOnLevelStart()
        {
            yield return new WaitUntil(() => standardLevelGameplayManager.GetField<StandardLevelGameplayManager.GameState>("_gameState") == StandardLevelGameplayManager.GameState.Playing);
            yield return new WaitUntil(() => Resources.FindObjectsOfTypeAll<GameEnergyCounter>().Any());

            gameEnergyCounter = Resources.FindObjectsOfTypeAll<GameEnergyCounter>().First();
            gameSongController = standardLevelGameplayManager.GetField<GameSongController>("_gameSongController");
            beatmapObjectManager = gameEnergyCounter.GetField<BeatmapObjectManager>("_beatmapObjectManager");

            //Get the value for obstacle energy drain
            _oldObstacleEnergyDrainPerSecond = gameEnergyCounter.GetField<float>("kObstacleEnergyDrainPerSecond", typeof(GameEnergyCounter));

            InstallPatches();
        }

        private void InstallPatches()
        {
            Logger.Info($"Patching fail methods");

            Logger.Info($"Harmony patching {nameof(GameEnergyCounter)}.HandleNoteWasMissed");
            _harmony.Patch(
                AccessTools.Method(typeof(GameEnergyCounter), "HandleNoteWasMissed"),
                new(AccessTools.Method(typeof(AntiFail), nameof(NoteWasMissedEvent)))
            );

            Logger.Info($"Harmony patching {nameof(GameEnergyCounter)}.HandleNoteWasCut");
            _harmony.Patch(
                AccessTools.Method(typeof(GameEnergyCounter), "HandleNoteWasCut"),
                new(AccessTools.Method(typeof(AntiFail), nameof(NoteWasCutEvent)))
            );

            Logger.Info($"Harmony patching {nameof(GameSongController)}.HandleSongDidFinish");
            _harmony.Patch(
                AccessTools.Method(typeof(GameSongController), "HandleSongDidFinish"),
                new(AccessTools.Method(typeof(AntiFail), nameof(SongDidFinishEvent)))
            );

            Logger.Info($"Harmony patching {nameof(StandardLevelGameplayManager)}.HandleGameEnergyDidReach0");
            _harmony.Patch(
                AccessTools.Method(typeof(StandardLevelGameplayManager), "HandleGameEnergyDidReach0"),
                new(AccessTools.Method(typeof(AntiFail), nameof(HandleGameEnergyDidReach0)))
            );
        }

        private void RemovePatches()
        {
            Logger.Info($"Unpatching fail methods");

            Logger.Info($"Harmony unpatching {nameof(GameEnergyCounter)}.HandleNoteWasMissed");
            _harmony.Unpatch(
                AccessTools.Method(typeof(GameEnergyCounter), "HandleNoteWasMissed"),
                AccessTools.Method(typeof(AntiFail), nameof(NoteWasMissedEvent))
            );

            Logger.Info($"Harmony unpatching {nameof(GameEnergyCounter)}.HandleNoteWasCut");
            _harmony.Unpatch(
                AccessTools.Method(typeof(GameEnergyCounter), "HandleNoteWasCut"),
                AccessTools.Method(typeof(AntiFail), nameof(NoteWasCutEvent))
            );

            Logger.Info($"Harmony unpatching {nameof(GameSongController)}.HandleSongDidFinish");
            _harmony.Unpatch(
                AccessTools.Method(typeof(GameSongController), "HandleSongDidFinish"),
                AccessTools.Method(typeof(AntiFail), nameof(SongDidFinishEvent))
            );

            Logger.Info($"Harmony unpatching {nameof(StandardLevelGameplayManager)}.HandleGameEnergyDidReach0");
            _harmony.Unpatch(
                AccessTools.Method(typeof(StandardLevelGameplayManager), "HandleGameEnergyDidReach0"),
                AccessTools.Method(typeof(AntiFail), nameof(HandleGameEnergyDidReach0))
            );
        }

        private bool NoteWasCutEvent(NoteController noteController, in NoteCutInfo noteCutInfo)
        {
            switch (noteController.noteData.gameplayType)
            {
                case NoteData.GameplayType.Normal:
                case NoteData.GameplayType.BurstSliderHead:
                    _nextFrameEnergyChange += (noteCutInfo.allIsOK ? 0.01f : -0.1f);
                    return false;
                case NoteData.GameplayType.Bomb:
                    _nextFrameEnergyChange -= 0.15f;
                    return false;
                case NoteData.GameplayType.BurstSliderElement:
                    _nextFrameEnergyChange += (noteCutInfo.allIsOK ? 0.002f : -0.025f);
                    return false;
                default:
                    return false;
            }
        }

        private bool NoteWasMissedEvent(NoteController noteController)
        {
            switch (noteController.noteData.gameplayType)
            {
                case NoteData.GameplayType.Normal:
                case NoteData.GameplayType.BurstSliderHead:
                    _nextFrameEnergyChange -= 0.15f;
                    return false;
                case NoteData.GameplayType.Bomb:
                    break;
                case NoteData.GameplayType.BurstSliderElement:
                    _nextFrameEnergyChange -= 0.03f;
                    break;
                default:
                    return false;
            }

            // Don't run original
            return false;
        }

        private bool HandleGameEnergyDidReach0()
        {
            return false;
        }

        //Our custom AddEnergy will pass along the info to the gameEnergyCounter's AddEnergy UNLESS we would have failed, in which case we withhold that information until the end of the level
        private void AddEnergy(float energyChange)
        {
            if (!_wouldHaveFailed)
            {
                var currentEnergy = gameEnergyCounter.energy;

                if (energyChange < 0f)
                {
                    if (currentEnergy <= 0f)
                    {
                        return;
                    }
                    if (gameEnergyCounter.instaFail)
                    {
                        currentEnergy = 0f;
                    }
                    else if (gameEnergyCounter.energyType == GameplayModifiers.EnergyType.Battery)
                    {
                        currentEnergy -= 1f / (float)gameEnergyCounter.GetField<float>("_batteryLives");
                    }
                    else
                    {
                        currentEnergy += energyChange;
                    }
                    if (currentEnergy <= 1E-05f)
                    {
                        _wouldHaveFailed = true;
                        BS_Utils.Gameplay.ScoreSubmission.DisableSubmission(Constants.NAME); //Probably not necessary since we invoke fail anyway on level end, but just to be safe...
                    }
                }

                if (!_wouldHaveFailed) gameEnergyCounter.InvokeMethod("ProcessEnergyChange", energyChange);
            }
        }

        private bool SongDidFinishEvent()
        {
            RemovePatches();

            if (_wouldHaveFailed) standardLevelGameplayManager.InvokeMethod("HandleGameEnergyDidReach0");
            standardLevelGameplayManager.InvokeMethod("HandleSongDidFinish");
            
            // Don't run original
            return false;
        }

        public static void Destroy() => Destroy(Instance);

        void OnDestroy() => Instance = null;
    }
}