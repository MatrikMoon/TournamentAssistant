using IPA.Utilities;
using SiraUtil.Services;
using System;
using TournamentAssistantShared;
using UnityEngine;
using Zenject;

/**
 * Created by Moon on 6/13/2020
 * ...and then subsequently left empty until 6/16/2020, 2:40 AM
 */

namespace TournamentAssistant.Behaviours
{
    public class AntiFail : IInitializable, ILateTickable, IDisposable
    {
        private readonly Submission _submission;
        private readonly SaberClashChecker _saberClashChecker;
        private readonly GameEnergyCounter _gameEnergyCounter;
        private readonly LevelStateManager _levelStateManager;
        private readonly GameSongController _gameSongController;
        private readonly BeatmapObjectManager _beatmapObjectManager;
        private readonly StandardLevelGameplayManager _standardLevelGameplayManager;
        private readonly PlayerHeadAndObstacleInteraction _playerHeadAndObstacleInteraction;

        private float _nextFrameEnergyChange;
        private float _oldObstacleEnergyDrainPerSecond;
        private bool _wouldHaveFailed = false;

        public AntiFail(Submission submission, SaberClashChecker saberClashChecker, GameEnergyCounter gameEnergyCounter, LevelStateManager levelStateManager, GameSongController gameSongController,
            BeatmapObjectManager beatmapObjectManager, ILevelEndActions standardLevelGameplayManager, PlayerHeadAndObstacleInteraction playerHeadAndObstacleInteraction)
        {
            _submission = submission;
            _saberClashChecker = saberClashChecker;
            _gameEnergyCounter = gameEnergyCounter;
            _levelStateManager = levelStateManager;
            _gameSongController = gameSongController;
            _beatmapObjectManager = beatmapObjectManager;
            _playerHeadAndObstacleInteraction = playerHeadAndObstacleInteraction;
            _standardLevelGameplayManager = (standardLevelGameplayManager as StandardLevelGameplayManager)!;
        }

        public void Initialize()
        {
            _levelStateManager.LevelFullyStarted += LevelStateManager_LevelFullyStarted;
            _beatmapObjectManager.noteWasCutEvent += BeatmapObjectManager_noteWasCutEvent;
            _beatmapObjectManager.noteWasMissedEvent += BeatmapObjectManager_noteWasMissedEvent;

            _beatmapObjectManager.noteWasCutEvent -= _gameEnergyCounter.HandleNoteWasCut;
            _beatmapObjectManager.noteWasMissedEvent -= _gameEnergyCounter.HandleNoteWasMissed;
            _gameSongController.songDidFinishEvent -= _standardLevelGameplayManager.HandleSongDidFinish;
        }

        private void LevelStateManager_LevelFullyStarted()
        {
            _oldObstacleEnergyDrainPerSecond = _gameEnergyCounter.GetField<float, GameEnergyCounter>("_obstacleEnergyDrainPerSecond");
            _gameEnergyCounter.SetField("_obstacleEnergyDrainPerSecond", 0f);
        }

        public void LateTick()
        {
            if (!_wouldHaveFailed)
            {
                if (_gameEnergyCounter != null && _playerHeadAndObstacleInteraction.intersectingObstacles.Count > 0)
                {
                    AddEnergy(Time.deltaTime * -_oldObstacleEnergyDrainPerSecond);
                }
                if (_gameEnergyCounter != null && _saberClashChecker.AreSabersClashing(out var _) && _gameEnergyCounter.failOnSaberClash)
                {
                    AddEnergy(-_gameEnergyCounter.energy);
                }
                if (!Mathf.Approximately(_nextFrameEnergyChange, 0f))
                {
                    AddEnergy(_nextFrameEnergyChange);
                    _nextFrameEnergyChange = 0f;
                }
            }
        }

        public void Dispose()
        {
            _levelStateManager.LevelFullyStarted -= LevelStateManager_LevelFullyStarted;
            _beatmapObjectManager.noteWasCutEvent -= BeatmapObjectManager_noteWasCutEvent;
            _beatmapObjectManager.noteWasMissedEvent -= BeatmapObjectManager_noteWasMissedEvent;

            _beatmapObjectManager.noteWasCutEvent += _gameEnergyCounter.HandleNoteWasCut;
            _beatmapObjectManager.noteWasMissedEvent += _gameEnergyCounter.HandleNoteWasMissed;
            _gameSongController.songDidFinishEvent += _standardLevelGameplayManager.HandleSongDidFinish;


            // Reset the gameEnergyCounter death by obstacle value
            _oldObstacleEnergyDrainPerSecond = _gameEnergyCounter.GetField<float, GameEnergyCounter>("_obstacleEnergyDrainPerSecond");
            _gameEnergyCounter.SetField("_obstacleEnergyDrainPerSecond", 0f);

            if (_wouldHaveFailed) _standardLevelGameplayManager.HandleGameEnergyDidReach0();
            _standardLevelGameplayManager.HandleSongDidFinish();
        }

        private void BeatmapObjectManager_noteWasCutEvent(NoteController noteController, in NoteCutInfo noteCutInfo)
        {
            if (noteController.noteData.colorType == ColorType.None)
            {
                _nextFrameEnergyChange -= _gameEnergyCounter.GetField<float, GameEnergyCounter>("_hitBombEnergyDrain");
                return;
            }
            _nextFrameEnergyChange += (noteCutInfo.allIsOK ? _gameEnergyCounter.GetField<float, GameEnergyCounter>("_goodNoteEnergyCharge") : (-_gameEnergyCounter.GetField<float, GameEnergyCounter>("_badNoteEnergyDrain")));
        }

        private void BeatmapObjectManager_noteWasMissedEvent(NoteController noteController)
        {
            if (noteController.noteData.colorType == ColorType.None)
            {
                return;
            }
            _nextFrameEnergyChange -= _gameEnergyCounter.GetField<float, GameEnergyCounter>("_missNoteEnergyDrain");
        }

        // Our custom AddEnergy will pass along the info to the gameEnergyCounter's AddEnergy UNLESS we would have failed, in which case we withhold that information until the end of the level
        private void AddEnergy(float value)
        {
            if (!_wouldHaveFailed)
            {
                var currentEnergy = _gameEnergyCounter.energy;

                if (value < 0f)
                {
                    if (currentEnergy <= 0f)
                    {
                        return;
                    }
                    if (_gameEnergyCounter.instaFail)
                    {
                        currentEnergy = 0f;
                    }
                    else if (_gameEnergyCounter.energyType == GameplayModifiers.EnergyType.Battery)
                    {
                        currentEnergy -= 1f / (float)_gameEnergyCounter.GetField<float, GameEnergyCounter>("_batteryLives");
                    }
                    else
                    {
                        currentEnergy += value;
                    }
                    if (currentEnergy <= 1E-05f)
                    {
                        _wouldHaveFailed = true;
                        _submission.DisableScoreSubmission(SharedConstructs.Name); //Probably not necessary since we invoke fail anyway on level end, but just to be safe...
                    }
                }
                if (!_wouldHaveFailed) _gameEnergyCounter.ProcessEnergyChange(value);
            }
        }
    }
}