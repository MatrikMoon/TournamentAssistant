using BeatSaberMarkupLanguage;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.ViewControllers;
using HMUI;
using Polyglot;
using SongCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

namespace TournamentAssistant.ViewControllers
{
    [ViewDefinition("TournamentAssistant.Views.song-detail-view.bsml")]
    [HotReload(RelativePathToLayout = @"..\Views\song-detail-view.bsml")]
    internal class SongDetailView : BSMLAutomaticViewController
    {
        [Inject]
        protected readonly PlayerDataModel _playerDataModel = null!;

        public event Action<IDifficultyBeatmap>? BeatmapChanged;
        public event Action<IBeatmapLevel, BeatmapCharacteristicSO, BeatmapDifficulty>? ClickedPlay;

        public bool ShowPlayButton { get; set; } = true;
        public bool ShowDifficulties { get; set; } = true;
        public bool ShowCharacteristics { get; set; } = true;

        private IBeatmapLevel? _selectedLevel;
        private IDifficultyBeatmap? _selectedDifficultyBeatmap;
        private readonly List<BeatmapCharacteristicSO> _beatmapCharacteristics = new();

        [UIComponent("level-details-transfom")]
        protected readonly RectTransform _levelDetailsTransform = null!;

        [UIComponent("song-name-text")]
        protected readonly CurvedTextMeshPro _songNameText = null!;

        [UIComponent("duration-text")]
        protected readonly CurvedTextMeshPro _durationText = null!;

        [UIComponent("bpm-text")]
        protected readonly CurvedTextMeshPro _bpmText = null!;

        [UIComponent("nps-text")]
        protected readonly CurvedTextMeshPro _npsText = null!;

        [UIComponent("notes-count-text")]
        protected readonly CurvedTextMeshPro _notesCountText = null!;

        [UIComponent("obstacles-count-text")]
        protected readonly CurvedTextMeshPro _obstaclesCountText = null!;

        [UIComponent("bombs-count-text")]
        protected readonly CurvedTextMeshPro _bombsCountText = null!;

        [UIComponent("level-cover-image")]
        protected readonly RawImage _levelCoverImage = null!;

        [UIComponent("controls-transform")]
        protected readonly RectTransform _controlsTransform = null!;

        [UIComponent("characteristic-control-blocker")]
        protected readonly RawImage _characteristicControlBlocker = null!;

        [UIComponent("characteristic-control")]
        protected readonly IconSegmentedControl _characteristicControl = null!;

        [UIComponent("difficulty-control-blocker")]
        protected readonly RawImage _difficultyControlBlocker = null!;

        [UIComponent("difficulty-control")]
        protected readonly TextSegmentedControl _difficultyControl = null!;

        [UIComponent("play-button")]
        protected readonly Button _playButton = null!;

        [UIComponent("buttons-transform")]
        protected readonly RectTransform _buttonsTransform;

        [UIAction("#post-parse")]
        protected void Parsed()
        {
            _levelCoverImage.color = new Color(0.5f, 0.5f, 0.5f, 0.5f);
            _difficultyControlBlocker.color = new Color(1f, 1f, 1f, 0f);
            _characteristicControlBlocker.color = new Color(1f, 1f, 1f, 0f);

            _levelDetailsTransform.gameObject.AddComponent<Mask>();
            Image mask = _levelDetailsTransform.gameObject.AddComponent<Image>(); // oh god i can only think about Dream's awful song right now
            mask.material = new Material(Utilities.ImageResources.NoGlowMat);
            mask.sprite = Resources.FindObjectsOfTypeAll<Sprite>().First(x => x.name == "RoundRectPanel");
            mask.color = Color.black.ColorWithAlpha(0.5f);
            mask.type = Image.Type.Sliced;
        }

        private void SetControlData(IDifficultyBeatmapSet[] difficultyBeatmapSets, BeatmapCharacteristicSO selectedBeatmapCharacteristic)
        {
            _beatmapCharacteristics.Clear();
            var beatmapSetList = new List<IDifficultyBeatmapSet>(difficultyBeatmapSets);
            beatmapSetList.Sort((IDifficultyBeatmapSet a, IDifficultyBeatmapSet b) => a.beatmapCharacteristic.sortingOrder.CompareTo(b.beatmapCharacteristic.sortingOrder));
            _beatmapCharacteristics.AddRange(beatmapSetList.Select(x => x.beatmapCharacteristic));

            var itemArray = beatmapSetList.Select(beatmapSet => new IconSegmentedControl.DataItem(beatmapSet.beatmapCharacteristic.icon, Localization.Get(beatmapSet.beatmapCharacteristic.descriptionLocalizationKey))).ToArray();
            var selectedIndex = Math.Max(0, beatmapSetList.FindIndex(x => x.beatmapCharacteristic == selectedBeatmapCharacteristic));

            _characteristicControl.SetData(itemArray);
            _characteristicControl.SelectCellWithNumber(selectedIndex);
            SetSelectedCharacteristic(null, selectedIndex);
        }

        private void UpdateContent()
        {
            if (_selectedLevel != null)
            {
                _songNameText.text = _selectedLevel.songName;
                _durationText.text = _selectedLevel.beatmapLevelData.audioClip.length.MinSecDurationText();
                _bpmText.text = Mathf.RoundToInt(_selectedLevel.beatsPerMinute).ToString();
                if (_selectedDifficultyBeatmap != null)
                {
                    _npsText.text = (_selectedDifficultyBeatmap.beatmapData.cuttableNotesType / _selectedLevel.beatmapLevelData.audioClip.length).ToString("0.00");
                    _notesCountText.text = _selectedDifficultyBeatmap.beatmapData.cuttableNotesType.ToString();
                    _obstaclesCountText.text = _selectedDifficultyBeatmap.beatmapData.obstaclesCount.ToString();
                    _bombsCountText.text = _selectedDifficultyBeatmap.beatmapData.bombsCount.ToString();
                }
                else
                {
                    _npsText.text = "--";
                    _notesCountText.text = "--";
                    _obstaclesCountText.text = "--";
                    _bombsCountText.text = "--";
                }
            }
        }

        public void SetSelectedSong(IBeatmapLevel selectedLevel)
        {
            _buttonsTransform.gameObject.SetActive(ShowPlayButton);

            _selectedLevel = selectedLevel;
            _controlsTransform.gameObject.SetActive(true);
            _characteristicControlBlocker.gameObject.SetActive(!ShowCharacteristics);
            _difficultyControlBlocker.gameObject.SetActive(!ShowDifficulties);
            SetBeatmapLevel(_selectedLevel);
        }


        [UIAction("characteristic-selected")]
        protected void SetSelectedCharacteristic(IconSegmentedControl? _, int index)
        {
            if (_selectedLevel == null)
                return;

            _playerDataModel.playerData.SetLastSelectedBeatmapCharacteristic(_beatmapCharacteristics[index]);

            var diffBeatmaps = _selectedLevel.beatmapLevelData.GetDifficultyBeatmapSet(_beatmapCharacteristics[index]).difficultyBeatmaps;
            var closestDifficulty = GetClosestDifficultyPreferLower(_selectedLevel, _playerDataModel.playerData.lastSelectedBeatmapDifficulty, _beatmapCharacteristics[index].serializedName);

            var extraData = Collections.RetrieveExtraSongData(Collections.hashForLevelID(_selectedLevel.levelID));
            if (extraData != null)
            {
                string[] difficultyLabels = new string[diffBeatmaps.Length];

                var extraDifficulties = extraData._difficulties.Where(x => x._beatmapCharacteristicName == _beatmapCharacteristics[index].serializedName || x._beatmapCharacteristicName == _beatmapCharacteristics[index].characteristicNameLocalizationKey);

                for (int i = 0; i < diffBeatmaps.Length; i++)
                {
                    var customDiff = extraDifficulties.FirstOrDefault(x => x._difficulty == diffBeatmaps[i].difficulty);
                    if (customDiff != null && !string.IsNullOrEmpty(customDiff._difficultyLabel)) difficultyLabels[i] = customDiff._difficultyLabel;
                    else difficultyLabels[i] = diffBeatmaps[i].difficulty.ToString().Replace("Plus", "+");
                }

                _difficultyControl.SetTexts(difficultyLabels);
            }
            else _difficultyControl.SetTexts(diffBeatmaps.Select(x => x.difficulty.ToString().Replace("Plus", "+")).ToArray());

            if (closestDifficulty == null)
                return;

            var diffIndex = Array.FindIndex(diffBeatmaps, x => x.difficulty == closestDifficulty.difficulty);

            _difficultyControl.SelectCellWithNumber(diffIndex);
            SetSelectedDifficulty(null, diffIndex);
        }

        public void SetSelectedDifficulty(int difficulty)
        {
            if (_selectedLevel == null)
                return;

            var difficultyIndex = Array.FindIndex(
                    _selectedLevel
                        .beatmapLevelData
                        .GetDifficultyBeatmapSet(
                            _playerDataModel
                                .playerData
                                .lastSelectedBeatmapCharacteristic
                        )
                        .difficultyBeatmaps,
                    x => x.difficulty == (BeatmapDifficulty)difficulty
                );
            _difficultyControl.SelectCellWithNumber(difficultyIndex);
            SetSelectedDifficulty(null, difficultyIndex);
        }

        private async void SetBeatmapLevel(IBeatmapLevel beatmapLevel)
        {
            if (beatmapLevel.beatmapLevelData.difficultyBeatmapSets.Any(x => x.beatmapCharacteristic == _playerDataModel.playerData.lastSelectedBeatmapCharacteristic))
            {
                _selectedDifficultyBeatmap = GetClosestDifficultyPreferLower(beatmapLevel, _playerDataModel.playerData.lastSelectedBeatmapDifficulty, _playerDataModel.playerData.lastSelectedBeatmapCharacteristic.serializedName);
            }
            else if (beatmapLevel.beatmapLevelData.difficultyBeatmapSets.Length > 0)
            {
                _selectedDifficultyBeatmap = GetClosestDifficultyPreferLower(beatmapLevel, _playerDataModel.playerData.lastSelectedBeatmapDifficulty, "Standard");
            }

            UpdateContent();

            if (_selectedDifficultyBeatmap == null)
                return;

            _playerDataModel.playerData.SetLastSelectedBeatmapCharacteristic(_selectedDifficultyBeatmap.parentDifficultyBeatmapSet.beatmapCharacteristic);
            _playerDataModel.playerData.SetLastSelectedBeatmapDifficulty(_selectedDifficultyBeatmap.difficulty);

            SetControlData(beatmapLevel.beatmapLevelData.difficultyBeatmapSets, _playerDataModel.playerData.lastSelectedBeatmapCharacteristic);

            _levelCoverImage.texture = (await beatmapLevel.GetCoverImageAsync(CancellationToken.None)).texture;
        }


        [UIAction("difficulty-selected")]
        protected void SetSelectedDifficulty(TextSegmentedControl? _, int index)
        {
            if (_selectedLevel == null || _selectedDifficultyBeatmap == null)
                return;

            _selectedDifficultyBeatmap = _selectedLevel.beatmapLevelData.GetDifficultyBeatmapSet(_playerDataModel.playerData.lastSelectedBeatmapCharacteristic).difficultyBeatmaps[index];
            _playerDataModel.playerData.SetLastSelectedBeatmapDifficulty(_selectedDifficultyBeatmap.difficulty);

            UpdateContent();

            BeatmapChanged?.Invoke(_selectedDifficultyBeatmap);
        }

        [UIAction("play-pressed")]
        protected void PlayClicked()
        {
            if (_selectedLevel == null || _selectedDifficultyBeatmap == null)
                return;

            var characteristic = _selectedDifficultyBeatmap.parentDifficultyBeatmapSet.beatmapCharacteristic;
            var difficulty = _selectedDifficultyBeatmap.difficulty;

            ClickedPlay?.Invoke(_selectedLevel, characteristic, difficulty);
        }

        public void SetSelectedCharacteristic(string serializedName)
        {
            var characteristicIndex = _beatmapCharacteristics.FindIndex(x => x.serializedName == serializedName);
            _characteristicControl.SelectCellWithNumber(characteristicIndex);
            SetSelectedCharacteristic(null, characteristicIndex);
        }

        //Returns the closest difficulty to the one provided, preferring lower difficulties first if any exist
        private static IDifficultyBeatmap? GetClosestDifficultyPreferLower(IBeatmapLevel level, BeatmapDifficulty difficulty, string characteristic)
        {
            //First, look at the characteristic parameter. If there's something useful in there, we try to use it, but fall back to Standard
            var desiredCharacteristic = level.previewDifficultyBeatmapSets.FirstOrDefault(x => x.beatmapCharacteristic.serializedName == characteristic).beatmapCharacteristic ?? level.previewDifficultyBeatmapSets.First().beatmapCharacteristic;

            IDifficultyBeatmap[] availableMaps =
                level
                .beatmapLevelData
                .difficultyBeatmapSets
                .FirstOrDefault(x => x.beatmapCharacteristic.serializedName == desiredCharacteristic.serializedName)
                .difficultyBeatmaps
                .OrderBy(x => x.difficulty)
                .ToArray();

            IDifficultyBeatmap? ret = availableMaps.FirstOrDefault(x => x.difficulty == difficulty);
            if (ret is CustomDifficultyBeatmap)
            {
                var extras = Collections.RetrieveExtraSongData(ret.level.levelID);
                var requirements = extras?._difficulties.First(x => x._difficulty == ret.difficulty).additionalDifficultyData._requirements;
                if (
                    (requirements?.Count() > 0) &&
                    (!requirements?.ToList().All(x => Collections.capabilities.Contains(x)) ?? false)
                ) ret = null;
            }

            if (ret == null)
            {
                ret = GetLowerDifficulty(availableMaps, difficulty, desiredCharacteristic);
            }
            if (ret == null)
            {
                ret = GetHigherDifficulty(availableMaps, difficulty, desiredCharacteristic);
            }

            return ret;
        }

        //Returns the next-lowest difficulty to the one provided
        private static IDifficultyBeatmap? GetLowerDifficulty(IDifficultyBeatmap[] availableMaps, BeatmapDifficulty difficulty, BeatmapCharacteristicSO characteristic)
        {
            var ret = availableMaps.TakeWhile(x => x.difficulty < difficulty).LastOrDefault();
            if (ret is CustomDifficultyBeatmap)
            {
                var extras = Collections.RetrieveExtraSongData(ret.level.levelID);
                var requirements = extras?._difficulties.First(x => x._difficulty == ret.difficulty).additionalDifficultyData._requirements;
                if (
                    (requirements?.Count() > 0) &&
                    (!requirements?.ToList().All(x => Collections.capabilities.Contains(x)) ?? false)
                ) ret = null;
            }
            return ret;
        }

        //Returns the next-highest difficulty to the one provided
        private static IDifficultyBeatmap? GetHigherDifficulty(IDifficultyBeatmap[] availableMaps, BeatmapDifficulty difficulty, BeatmapCharacteristicSO characteristic)
        {
            var ret = availableMaps.SkipWhile(x => x.difficulty < difficulty).FirstOrDefault();
            if (ret is CustomDifficultyBeatmap)
            {
                var extras = Collections.RetrieveExtraSongData(ret.level.levelID);
                var requirements = extras?._difficulties.First(x => x._difficulty == ret.difficulty).additionalDifficultyData._requirements;
                if (
                    (requirements?.Count() > 0) &&
                    (!requirements?.ToList().All(x => Collections.capabilities.Contains(x)) ?? false)
                ) ret = null;
            }
            return ret;
        }
    }
}