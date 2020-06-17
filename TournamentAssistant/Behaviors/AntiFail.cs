using System.Collections;
using System.Linq;
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

        public virtual void Update()
        {
            if (!_wouldHaveFailed)
            {
                if (gameEnergyCounter != null && gameEnergyCounter.GetField<PlayerHeadAndObstacleInteraction>("_playerHeadAndObstacleInteraction").intersectingObstacles.Count > 0)
                {
                    AddEnergy(Time.deltaTime * -_oldObstacleEnergyDrainPerSecond);
                }
                if (gameEnergyCounter != null && gameEnergyCounter.GetField<SaberClashChecker>("_saberClashChecker") && gameEnergyCounter.failOnSaberClash)
                {
                    AddEnergy(-gameEnergyCounter.energy);
                }
            }
        }

        public IEnumerator DoOnLevelStart()
        {
            yield return new WaitUntil(() => standardLevelGameplayManager.GetField<StandardLevelGameplayManager.GameState>("_gameState") == StandardLevelGameplayManager.GameState.Playing);
            yield return new WaitUntil(() => Resources.FindObjectsOfTypeAll<GameEnergyCounter>().Any());

            gameSongController = standardLevelGameplayManager.GetField<GameSongController>("_gameSongController");
            gameEnergyCounter = Resources.FindObjectsOfTypeAll<GameEnergyCounter>().First();

            //Prevent the gameEnergyCounter from invoking death by obstacle
            _oldObstacleEnergyDrainPerSecond = gameEnergyCounter.GetField<float>("_obstacleEnergyDrainPerSecond");
            gameEnergyCounter.SetField("_obstacleEnergyDrainPerSecond", 0f);

            //Unhook the functions in the energy counter that watch note events, so we can peek inside the process
            beatmapObjectManager = gameEnergyCounter.GetField<BeatmapObjectManager>("_beatmapObjectManager");
            
            beatmapObjectManager.noteWasMissedEvent -= gameEnergyCounter.HandleNoteWasMissedEvent;
            beatmapObjectManager.noteWasMissedEvent += beatmapObjectManager_noteWasMissedEvent;

            beatmapObjectManager.noteWasCutEvent -= gameEnergyCounter.HandleNoteWasCutEvent;
            beatmapObjectManager.noteWasCutEvent += beatmapObjectManager_noteWasCutEvent;

            //Unhook the level end event so we can reset everything before the level ends
            gameSongController.songDidFinishEvent -= standardLevelGameplayManager.HandleSongDidFinish;
            gameSongController.songDidFinishEvent += gameSongController_songDidFinishEvent;
        }

        private void beatmapObjectManager_noteWasCutEvent(INoteController noteController, NoteCutInfo noteCutInfo)
        {
            NoteType noteType = noteController.noteData.noteType;
            if (noteType != NoteType.Bomb && noteType != NoteType.NoteA && noteType != NoteType.NoteB)
            {
                return;
            }
            if (noteCutInfo.allIsOK)
            {
                AddEnergy(gameEnergyCounter.GetField<float>("_goodNoteEnergyCharge"));
                return;
            }
            if (noteType == NoteType.Bomb)
            {
                AddEnergy(-gameEnergyCounter.GetField<float>("_hitBombEnergyDrain"));
                return;
            }
            AddEnergy(-gameEnergyCounter.GetField<float>("_badNoteEnergyDrain"));
        }

        private void beatmapObjectManager_noteWasMissedEvent(INoteController noteController)
        {
            NoteType noteType = noteController.noteData.noteType;
            if (noteType == NoteType.NoteA || noteType == NoteType.NoteB)
            {
                AddEnergy(-gameEnergyCounter.GetField<float>("_missNoteEnergyDrain"));
            }
        }

        //Our custom AddEnergy will pass along the info to the gameEnergyCounter's AddEnergy UNLESS we would have failed, in which case we withhold that information until the end of the level
        private void AddEnergy(float value)
        {
            if (!_wouldHaveFailed)
            {
                var currentEnergy = gameEnergyCounter.energy;

                if (value < 0f)
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
                        currentEnergy += value;
                    }
                    if (currentEnergy <= 1E-05f)
                    {
                        _wouldHaveFailed = true;
                        BS_Utils.Gameplay.ScoreSubmission.DisableSubmission(SharedConstructs.Name); //Probably not necessary since we invoke fail anyway on level end, but just to be safe...
                    }
                }

                if (!_wouldHaveFailed) gameEnergyCounter.AddEnergy(value);
            }
        }

        private void gameSongController_songDidFinishEvent()
        {
            //Reset the gameEnergyCounter death by obstacle value
            _oldObstacleEnergyDrainPerSecond = gameEnergyCounter.GetField<float>("_obstacleEnergyDrainPerSecond");
            gameEnergyCounter.SetField("_obstacleEnergyDrainPerSecond", 0f);

            //Rehook the functions in the energy counter that watch note events
            beatmapObjectManager = gameEnergyCounter.GetField<BeatmapObjectManager>("_beatmapObjectManager");

            beatmapObjectManager.noteWasMissedEvent += gameEnergyCounter.HandleNoteWasMissedEvent;
            beatmapObjectManager.noteWasMissedEvent -= beatmapObjectManager_noteWasMissedEvent;

            beatmapObjectManager.noteWasCutEvent += gameEnergyCounter.HandleNoteWasCutEvent;
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
