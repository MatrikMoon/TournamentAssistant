using System.Collections;
using System.Linq;
using TournamentAssistant.Utilities;
using TournamentAssistantShared;
using UnityEngine;

/**
 * Created by Moon on 6/13/2020
 * ...and then subsequently left empty until 6/16/2020, 2:40 AM
 */

namespace TournamentAssistant.Behaviors
{
    class AntiFail : MonoBehaviour
    {
        public static AntiFail Instance { get; set; }

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
            //Thanks to kObstacleEnergyDrainPerSecond becoming a constant in 1.20, we can no longer prevent the player from dying by sticking their head in a wall, so
            //instead I've chosen to just... Add back the health they lost. It's hacky, but it works.
            if (gameEnergyCounter != null && gameEnergyCounter.GetField<SaberClashChecker>("_saberClashChecker").AreSabersClashing(out var _) && gameEnergyCounter.failOnSaberClash)
            {
                if (_wouldHaveFailed)
                {
                    gameEnergyCounter.ProcessEnergyChange(gameEnergyCounter.energy);
                }

                _nextFrameEnergyChange -= gameEnergyCounter.energy;
            }

            if (gameEnergyCounter != null && gameEnergyCounter.GetField<PlayerHeadAndObstacleInteraction>("_playerHeadAndObstacleInteraction").playerHeadIsInObstacle)
            {
                if (_wouldHaveFailed)
                {
                    gameEnergyCounter.ProcessEnergyChange(Time.deltaTime * _oldObstacleEnergyDrainPerSecond);
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

            gameSongController = standardLevelGameplayManager.GetField<GameSongController>("_gameSongController");
            gameEnergyCounter = Resources.FindObjectsOfTypeAll<GameEnergyCounter>().First();

            //Get the value for obstacle energy drain
            _oldObstacleEnergyDrainPerSecond = gameEnergyCounter.GetField<float>("kObstacleEnergyDrainPerSecond", typeof(GameEnergyCounter));
            //Prevent the gameEnergyCounter from invoking death by obstacle
            var eventInfo = gameEnergyCounter.GetType().GetEvent(nameof(gameEnergyCounter.gameEnergyDidReach0Event));
            standardLevelGameplayManager.SetField("_initData", new StandardLevelGameplayManager.InitData(false));

            //Unhook the functions in the energy counter that watch note events, so we can peek inside the process
            beatmapObjectManager = gameEnergyCounter.GetField<BeatmapObjectManager>("_beatmapObjectManager");

            beatmapObjectManager.noteWasMissedEvent -= gameEnergyCounter.HandleNoteWasMissed;
            beatmapObjectManager.noteWasMissedEvent += beatmapObjectManager_noteWasMissedEvent;

            beatmapObjectManager.noteWasCutEvent -= gameEnergyCounter.HandleNoteWasCut;
            beatmapObjectManager.noteWasCutEvent += beatmapObjectManager_noteWasCutEvent;

            //Unhook the level end event so we can reset everything before the level ends
            gameSongController.songDidFinishEvent -= standardLevelGameplayManager.HandleSongDidFinish;
            gameSongController.songDidFinishEvent += gameSongController_songDidFinishEvent;
        }

        private void beatmapObjectManager_noteWasCutEvent(NoteController noteController, in NoteCutInfo noteCutInfo)
        {
            switch (noteController.noteData.gameplayType)
            {
                case NoteData.GameplayType.Normal:
                case NoteData.GameplayType.BurstSliderHead:
                    _nextFrameEnergyChange += (noteCutInfo.allIsOK ? 0.01f : -0.1f);
                    return;
                case NoteData.GameplayType.Bomb:
                    _nextFrameEnergyChange -= 0.15f;
                    return;
                case NoteData.GameplayType.BurstSliderElement:
                    _nextFrameEnergyChange += (noteCutInfo.allIsOK ? 0.002f : -0.025f);
                    return;
                default:
                    return;
            }
        }

        private void beatmapObjectManager_noteWasMissedEvent(NoteController noteController)
        {
            switch (noteController.noteData.gameplayType)
            {
                case NoteData.GameplayType.Normal:
                case NoteData.GameplayType.BurstSliderHead:
                    _nextFrameEnergyChange -= 0.15f;
                    return;
                case NoteData.GameplayType.Bomb:
                    break;
                case NoteData.GameplayType.BurstSliderElement:
                    _nextFrameEnergyChange -= 0.03f;
                    break;
                default:
                    return;
            }
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

                if (!_wouldHaveFailed) gameEnergyCounter.ProcessEnergyChange(energyChange);
            }
        }

        private void gameSongController_songDidFinishEvent()
        {
            standardLevelGameplayManager.SetField("_initData", new StandardLevelGameplayManager.InitData(true));

            //Rehook the functions in the energy counter that watch note events
            beatmapObjectManager = gameEnergyCounter.GetField<BeatmapObjectManager>("_beatmapObjectManager");

            beatmapObjectManager.noteWasMissedEvent += gameEnergyCounter.HandleNoteWasMissed;
            beatmapObjectManager.noteWasMissedEvent -= beatmapObjectManager_noteWasMissedEvent;

            beatmapObjectManager.noteWasCutEvent += gameEnergyCounter.HandleNoteWasCut;
            beatmapObjectManager.noteWasCutEvent -= beatmapObjectManager_noteWasCutEvent;

            //Rehook the level end event
            gameSongController.songDidFinishEvent += standardLevelGameplayManager.HandleSongDidFinish;
            gameSongController.songDidFinishEvent -= gameSongController_songDidFinishEvent;

            if (_wouldHaveFailed) standardLevelGameplayManager.HandleGameEnergyDidReach0();
            standardLevelGameplayManager.HandleSongDidFinish();
        }

        public static void Destroy() => Destroy(Instance);

        void OnDestroy() => Instance = null;
    }
}