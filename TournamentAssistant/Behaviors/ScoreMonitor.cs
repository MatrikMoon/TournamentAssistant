using System.Collections;
using System.Linq;
using System.Threading.Tasks;
using TournamentAssistantShared.Models;
using TournamentAssistantShared.Utilities;
using UnityEngine;
using Logger = TournamentAssistantShared.Logger;
using Match = TournamentAssistantShared.Models.Match;

namespace TournamentAssistant.Behaviors
{
    class ScoreMonitor : MonoBehaviour
    {
        public static ScoreMonitor Instance { get; set; }

        private PluginClient Client { get; set; }
        private Match Match { get; set; }
        private Tournament Tournament { get; set; }

        private ScoreController _scoreController;
        private GameEnergyCounter _gameEnergyCounter;
        private ComboController _comboController;
        private AudioTimeSyncController _audioTimeSyncController;

        // private RoomCoordinator _roomCoordinator;
        private string[] audience;

        private int _scoreUpdateFrequency = 30;
        private int _timeSinceLastScoreCheck = 0;

        // Trackers
        private RealtimeScore _score = new RealtimeScore();
        private int[] leftTotalCutScores = { 0, 0, 0 };
        private int[] leftTotalCuts = { 0, 0, 0 };
        private int[] rightTotalCutScores = { 0, 0, 0 };
        private int[] rightTotalCuts = { 0, 0, 0 };

        // Trackers as of last time an update was sent to the server
        private RealtimeScore _lastUpdatedScore = new RealtimeScore();

        public void SetClient(PluginClient client)
        {
            Client = client;
        }

        public void SetMatch(Match match)
        {
            Match = match;
        }

        public void SetTournament(Tournament tournament)
        {
            Tournament = tournament;
        }

        void Awake()
        {
            Instance = this;

            //Will actually be destroyed when the main game scene is loaded again, but unfortunately this 
            //object is created before the game scene loads, so we need to do this to prevent the game scene
            //load from destroying it
            DontDestroyOnLoad(this);

            StartCoroutine(WaitForComponentCreation());
        }

        public void Update()
        {
            if (_timeSinceLastScoreCheck > _scoreUpdateFrequency && _scoreController != null)
            {
                _timeSinceLastScoreCheck = 0;

                var accuracy = (float)_scoreController.modifiedScore / _scoreController.immediateMaxPossibleModifiedScore;

                _score.UserGuid = Client.StateManager.GetSelfGuid();
                _score.Score = _scoreController.multipliedScore;
                _score.ScoreWithModifiers = _scoreController.modifiedScore;
                _score.Combo = _comboController.GetField<int>("_combo");
                _score.Accuracy = float.IsNaN(accuracy) ? 0.00f : accuracy;
                _score.SongPosition = _audioTimeSyncController.songTime;
                _score.MaxScore = _scoreController.immediateMaxPossibleMultipliedScore;
                _score.MaxScoreWithModifiers = _scoreController.immediateMaxPossibleModifiedScore;
                _score.PlayerHealth = _gameEnergyCounter.energy;

                if (AreScoresDifferent(_score, _lastUpdatedScore))
                {
                    //NOTE: We don't needa be blasting the entire server
                    //with score updates. This update will only go out to other
                    //players in the current match and the other associated users
                    Client.SendRealtimeScore(audience, _score);

                    _lastUpdatedScore.Score = _score.Score;
                    _lastUpdatedScore.NotesMissed = _score.NotesMissed;
                    _lastUpdatedScore.BadCuts = _score.BadCuts;
                    _lastUpdatedScore.BombHits = _score.BombHits;
                    _lastUpdatedScore.WallHits = _score.WallHits;
                    _lastUpdatedScore.MaxCombo = _score.MaxCombo;
                }
            }

            _timeSinceLastScoreCheck++;
        }

        public IEnumerator WaitForComponentCreation()
        {
            // Register handler so we can listen for any joining coordinators or overlays during the match
            Client.StateManager.MatchInfoUpdated += Client_MatchInfoUpdated;

            // Load inital Audience from the current state of the Match
            UpdateAudience();

            // Load settings from Tournament settings
            _scoreUpdateFrequency = Tournament.Settings.ScoreUpdateFrequency;

            // Wait for needed controllers to laod
            yield return new WaitUntil(() => Resources.FindObjectsOfTypeAll<ScoreController>().Any());
            yield return new WaitUntil(() => Resources.FindObjectsOfTypeAll<ComboController>().Any());
            yield return new WaitUntil(() => Resources.FindObjectsOfTypeAll<AudioTimeSyncController>().Any());

            _scoreController = Resources.FindObjectsOfTypeAll<ScoreController>().First();
            _gameEnergyCounter = Resources.FindObjectsOfTypeAll<GameEnergyCounter>().First();
            _comboController = Resources.FindObjectsOfTypeAll<ComboController>().First();
            _audioTimeSyncController = Resources.FindObjectsOfTypeAll<AudioTimeSyncController>().First();

            yield return new WaitUntil(() => _scoreController.GetField<BeatmapObjectManager>("_beatmapObjectManager") != null);

            var beatmapObjectManager = _scoreController.GetField<BeatmapObjectManager>("_beatmapObjectManager");
            var headObstacleInteration = _scoreController.GetField<PlayerHeadAndObstacleInteraction>("_playerHeadAndObstacleInteraction");
            beatmapObjectManager.noteWasMissedEvent += BeatmapObjectManager_noteWasMissedEvent;
            beatmapObjectManager.noteWasCutEvent += BeatmapObjectManager_noteWasCutEvent;
            _scoreController.scoringForNoteFinishedEvent += ScoreController_scoringForNoteFinishedEvent;
            headObstacleInteration.headDidEnterObstaclesEvent += HeadObstacleInteration_enterObstacle;

            // Set inital tracker values
            _score.LeftHand = new ScoreTrackerHand();
            _score.LeftHand.AvgCuts = new float[3] { 0, 0, 0 };
            _score.RightHand = new ScoreTrackerHand();
            _score.RightHand.AvgCuts = new float[3] { 0, 0, 0 };
        }

        private bool AreScoresDifferent(RealtimeScore score1, RealtimeScore score2)
        {
            return score1.Score != score2.Score
                    || score1.NotesMissed != score2.NotesMissed
                    || score1.BadCuts != score2.BadCuts
                    || score1.BombHits != score2.BombHits
                    || score1.WallHits != score2.WallHits
                    || score1.MaxCombo != score2.MaxCombo;
        }

        private void UpdateAudience()
        {
            Logger.Info($"Updating audience for match: {Match.Guid}");
            audience = Match.AssociatedUsers.Where(x => Client.StateManager.GetUser(Tournament.Guid, x).ClientType != User.ClientTypes.Player).ToArray();
        }

        private Task Client_MatchInfoUpdated(Match match)
        {
            Logger.Info($"Match update received: {match.Guid}, current match guid: {Match.Guid}");
            if (match.Guid == Match.Guid)
            {
                UpdateAudience();
            }
            return Task.CompletedTask;
        }

        private void BeatmapObjectManager_noteWasMissedEvent(NoteController noteController)
        {
            if (noteController.noteData.gameplayType == NoteData.GameplayType.Bomb)
            {
                return;
            }
            _score.NotesMissed++;
            if (noteController.noteData.colorType == ColorType.ColorA)
            {
                _score.LeftHand.Miss++;
            }
            else if (noteController.noteData.colorType == ColorType.ColorB)
            {
                _score.RightHand.Miss++;
            }
        }

        private void ScoreController_scoringForNoteFinishedEvent(ScoringElement scoringElement)
        {
            // Handle good cuts
            if (scoringElement is GoodCutScoringElement goodCut)
            {
                var cutScoreBuffer = goodCut.cutScoreBuffer;

                var beforeCut = cutScoreBuffer.beforeCutScore;
                var afterCut = cutScoreBuffer.afterCutScore;
                var cutDistance = cutScoreBuffer.centerDistanceCutScore;

                var totalScoresForHand = goodCut.noteData.colorType == ColorType.ColorA ? leftTotalCutScores : rightTotalCutScores;

                var cutCountForHand = goodCut.noteData.colorType == ColorType.ColorA ? leftTotalCuts : rightTotalCuts;

                switch (goodCut.noteData.scoringType)
                {
                    case NoteData.ScoringType.Normal:
                        totalScoresForHand[0] += beforeCut;
                        totalScoresForHand[1] += afterCut;
                        totalScoresForHand[2] += cutDistance;

                        cutCountForHand[0]++;
                        cutCountForHand[1]++;
                        cutCountForHand[2]++;
                        break;
                    case NoteData.ScoringType.SliderHead:
                        totalScoresForHand[0] += beforeCut;
                        totalScoresForHand[2] += cutDistance;

                        cutCountForHand[0]++;
                        cutCountForHand[2]++;
                        break;
                    case NoteData.ScoringType.SliderTail:
                        totalScoresForHand[1] += afterCut;
                        totalScoresForHand[2] += cutDistance;

                        cutCountForHand[1]++;
                        cutCountForHand[2]++;
                        break;
                    case NoteData.ScoringType.BurstSliderHead:
                        totalScoresForHand[0] += beforeCut;
                        totalScoresForHand[2] += cutDistance;

                        cutCountForHand[0]++;
                        cutCountForHand[2]++;
                        break;
                }

                if (goodCut.noteData.colorType == ColorType.ColorA)
                {
                    // We have to check if this is greater than 0 because of sliders
                    if (cutCountForHand[0] > 0)
                    {
                        _score.LeftHand.AvgCuts[0] = totalScoresForHand[0] / cutCountForHand[0];
                    }
                    if (cutCountForHand[1] > 0)
                    {
                        _score.LeftHand.AvgCuts[1] = totalScoresForHand[1] / cutCountForHand[1];
                    }
                    if (cutCountForHand[2] > 0)
                    {
                        _score.LeftHand.AvgCuts[2] = totalScoresForHand[2] / cutCountForHand[2];
                    }
                }
                else if (goodCut.noteData.colorType == ColorType.ColorB)
                {
                    if (cutCountForHand[0] > 0)
                    {
                        _score.RightHand.AvgCuts[0] = totalScoresForHand[0] / cutCountForHand[0];
                    }
                    if (cutCountForHand[1] > 0)
                    {
                        _score.RightHand.AvgCuts[1] = totalScoresForHand[1] / cutCountForHand[1];
                    }
                    if (cutCountForHand[2] > 0)
                    {
                        _score.RightHand.AvgCuts[2] = totalScoresForHand[2] / cutCountForHand[2];
                    }
                }

                var combo = _comboController.GetField<int>("_combo");
                if (combo > _score.MaxCombo)
                {
                    _score.MaxCombo = combo;
                }
            }
        }

        private void BeatmapObjectManager_noteWasCutEvent(NoteController noteController, in NoteCutInfo noteCutInfo)
        {
            //Ignore notes that aren't scoring-relevant
            if (noteCutInfo.noteData.scoringType == NoteData.ScoringType.Ignore)
            {
                return;
            }

            //If the note was hit successfully
            if (noteCutInfo.allIsOK)
            {
                if (noteController.noteData.colorType == ColorType.ColorA)
                {
                    _score.LeftHand.Hit++;
                }
                else if (noteController.noteData.colorType == ColorType.ColorB)
                {
                    _score.RightHand.Hit++;
                }
            }

            //If the note was a bad hit or we hit a bomb
            else if (!noteCutInfo.allIsOK && noteCutInfo.noteData.gameplayType != NoteData.GameplayType.Bomb)
            {
                _score.BadCuts++;
                if (noteController.noteData.colorType == ColorType.ColorA)
                {
                    _score.LeftHand.BadCut++;
                }
                else if (noteController.noteData.colorType == ColorType.ColorB)
                {
                    _score.RightHand.BadCut++;
                }
            }
            else if (noteCutInfo.noteData.gameplayType == NoteData.GameplayType.Bomb)
            {
                _score.BombHits++;
            }
        }

        private void HeadObstacleInteration_enterObstacle()
        {
            _score.WallHits++;
        }

        public static void Destroy() => Destroy(Instance);

        void OnDestroy()
        {
            // Unsubscribe from BG events
            var beatmapObjectManager = _scoreController.GetField<BeatmapObjectManager>("_beatmapObjectManager");
            var headObstacleInteration = _scoreController.GetField<PlayerHeadAndObstacleInteraction>("_playerHeadAndObstacleInteraction");
            beatmapObjectManager.noteWasMissedEvent -= BeatmapObjectManager_noteWasMissedEvent;
            beatmapObjectManager.noteWasCutEvent -= BeatmapObjectManager_noteWasCutEvent;
            headObstacleInteration.headDidEnterObstaclesEvent -= HeadObstacleInteration_enterObstacle;

            // Unregister MatchInfo listener
            Client.StateManager.MatchInfoUpdated -= Client_MatchInfoUpdated;

            // We no longer exist
            Instance = null;
        }
    }
}