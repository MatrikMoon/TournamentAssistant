#pragma warning disable CS0649
#pragma warning disable IDE0044
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.ViewControllers;
using HMUI;
using Polyglot;
using SongCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using TMPro;
using TournamentAssistant.UI.FlowCoordinators;
using TournamentAssistant.Utilities;
using UnityEngine;
using UnityEngine.UI;

namespace TournamentAssistant.UI.ViewControllers
{
    internal class SongDetail : BSMLResourceViewController
    {
        // For this method of setting the ResourceName, this class must be the first class in the file.
        public override string ResourceName => string.Join(".", GetType().Namespace, GetType().Name);

        public event Action<IDifficultyBeatmap> DifficultyBeatmapChanged;
        public event Action<IBeatmapLevel, BeatmapCharacteristicSO, BeatmapDifficulty> PlayPressed;

        public bool DisableCharacteristicControl { get; set; }
        public bool DisableDifficultyControl { get; set; }
        public bool DisablePlayButton { get; set; }

        private IBeatmapLevel _selectedLevel;
        private IDifficultyBeatmap _selectedDifficultyBeatmap;

        private BeatmapDifficulty SelectedDifficulty { get { return _selectedDifficultyBeatmap?.difficulty ?? BeatmapDifficulty.ExpertPlus; } }
        private BeatmapCharacteristicSO SelectedCharacteristic { get { return _selectedDifficultyBeatmap?.parentDifficultyBeatmapSet.beatmapCharacteristic; } }

        private PlayerDataModel _playerDataModel;
        private List<BeatmapCharacteristicSO> _beatmapCharacteristics = new();
        private CancellationTokenSource cancellationToken;

        [UIComponent("level-details-rect")]
        public RectTransform levelDetailsRect;

        [UIComponent("song-name-text")]
        public TextMeshProUGUI songNameText;
        [UIComponent("duration-text")]
        public TextMeshProUGUI durationText;
        [UIComponent("bpm-text")]
        public TextMeshProUGUI bpmText;
        [UIComponent("nps-text")]
        public TextMeshProUGUI npsText;
        [UIComponent("notes-count-text")]
        public TextMeshProUGUI notesCountText;
        [UIComponent("obstacles-count-text")]
        public TextMeshProUGUI obstaclesCountText;
        [UIComponent("bombs-count-text")]
        public TextMeshProUGUI bombsCountText;

        [UIComponent("level-cover-image")]
        public RawImage levelCoverImage;

        [UIComponent("controls-rect")]
        public RectTransform controlsRect;
        [UIComponent("characteristic-control-blocker")]
        public RawImage charactertisticControlBlocker;
        [UIComponent("characteristic-control")]
        public IconSegmentedControl characteristicControl;
        [UIComponent("difficulty-control-blocker")]
        public RawImage difficultyControlBlocker;
        [UIComponent("difficulty-control")]
        public TextSegmentedControl difficultyControl;

        [UIComponent("play-button")]
        public Button playButton;

        [UIComponent("buttons-rect")]
        public RectTransform buttonsRect;

        protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
        {
            base.DidActivate(firstActivation, addedToHierarchy, screenSystemEnabling);
            if (RoomCoordinator.tempStat != null) SetSelectedSong(RoomCoordinator.tempStat);
        }

        [UIAction("#post-parse")]
        public void SetupViewController()
        {
            _playerDataModel = Resources.FindObjectsOfTypeAll<PlayerDataModel>().First();
            levelCoverImage.color = new Color(0.5f, 0.5f, 0.5f, 0.5f);
            charactertisticControlBlocker.color = new Color(1f, 1f, 1f, 0f);
            //charactertisticControlBlocker.material = Resources.FindObjectsOfTypeAll<Material>().FirstOrDefault(x => x.name == "UINoGlow");
            difficultyControlBlocker.color = new Color(1f, 1f, 1f, 0f);
            //difficultyControlBlocker.material = Resources.FindObjectsOfTypeAll<Material>().FirstOrDefault(x => x.name == "UINoGlow");
            cancellationToken = new CancellationTokenSource();

            levelDetailsRect.gameObject.AddComponent<Mask>();
            Image maskImage = levelDetailsRect.gameObject.AddComponent<Image>();
            maskImage.material = new Material(Resources.FindObjectsOfTypeAll<Material>().Where(m => m.name == "UINoGlow").First());
            maskImage.sprite = Resources.FindObjectsOfTypeAll<Sprite>().First(x => x.name == "RoundRectPanel");
            maskImage.type = Image.Type.Sliced;
            maskImage.color = new Color(0f, 0f, 0f, 0.25f);
        }

        public void SetControlData(IDifficultyBeatmapSet[] difficultyBeatmapSets, BeatmapCharacteristicSO selectedBeatmapCharacteristic)
        {
            _beatmapCharacteristics.Clear();
            var beatmapSetList = new List<IDifficultyBeatmapSet>(difficultyBeatmapSets);
            beatmapSetList.Sort((IDifficultyBeatmapSet a, IDifficultyBeatmapSet b) => a.beatmapCharacteristic.sortingOrder.CompareTo(b.beatmapCharacteristic.sortingOrder));
            _beatmapCharacteristics.AddRange(beatmapSetList.Select(x => x.beatmapCharacteristic));

            var itemArray = beatmapSetList.Select(beatmapSet => new IconSegmentedControl.DataItem(beatmapSet.beatmapCharacteristic.icon, Localization.Get(beatmapSet.beatmapCharacteristic.descriptionLocalizationKey))).ToArray();
            var selectedIndex = Math.Max(0, beatmapSetList.FindIndex(x => x.beatmapCharacteristic == selectedBeatmapCharacteristic));

            characteristicControl.SetData(itemArray);
            characteristicControl.SelectCellWithNumber(selectedIndex);
            SetSelectedCharacteristic(null, selectedIndex);
        }

        public void UpdateContent()
        {
            if (_selectedLevel != null)
            {
                songNameText.text = _selectedLevel.songName;
                durationText.text = _selectedLevel.beatmapLevelData.audioClip.length.MinSecDurationText();
                bpmText.text = Mathf.RoundToInt(_selectedLevel.beatsPerMinute).ToString();
                if (_selectedDifficultyBeatmap != null)
                {
                    npsText.text = (_selectedDifficultyBeatmap.beatmapData.cuttableNotesType / _selectedLevel.beatmapLevelData.audioClip.length).ToString("0.00");
                    notesCountText.text = _selectedDifficultyBeatmap.beatmapData.cuttableNotesType.ToString();
                    obstaclesCountText.text = _selectedDifficultyBeatmap.beatmapData.obstaclesCount.ToString();
                    bombsCountText.text = _selectedDifficultyBeatmap.beatmapData.bombsCount.ToString();
                }
                else
                {
                    npsText.text = "--";
                    notesCountText.text = "--";
                    obstaclesCountText.text = "--";
                    bombsCountText.text = "--";
                }
            }
        }

        public void SetSelectedSong(IBeatmapLevel selectedLevel)
        {
            buttonsRect.gameObject.SetActive(!DisablePlayButton);

            _selectedLevel = selectedLevel;
            controlsRect.gameObject.SetActive(true);
            charactertisticControlBlocker.gameObject.SetActive(DisableCharacteristicControl);
            difficultyControlBlocker.gameObject.SetActive(DisableDifficultyControl);
            SetBeatmapLevel(_selectedLevel);
        }

        private async void SetBeatmapLevel(IBeatmapLevel beatmapLevel)
        {
            if (beatmapLevel.beatmapLevelData.difficultyBeatmapSets.Any(x => x.beatmapCharacteristic == _playerDataModel.playerData.lastSelectedBeatmapCharacteristic))
            {
                _selectedDifficultyBeatmap = SongUtils.GetClosestDifficultyPreferLower(beatmapLevel, _playerDataModel.playerData.lastSelectedBeatmapDifficulty, _playerDataModel.playerData.lastSelectedBeatmapCharacteristic.serializedName);
            }
            else if (beatmapLevel.beatmapLevelData.difficultyBeatmapSets.Length > 0)
            {
                _selectedDifficultyBeatmap = SongUtils.GetClosestDifficultyPreferLower(beatmapLevel, _playerDataModel.playerData.lastSelectedBeatmapDifficulty, "Standard");
            }

            UpdateContent();

            _playerDataModel.playerData.SetLastSelectedBeatmapCharacteristic(_selectedDifficultyBeatmap.parentDifficultyBeatmapSet.beatmapCharacteristic);
            _playerDataModel.playerData.SetLastSelectedBeatmapDifficulty(_selectedDifficultyBeatmap.difficulty);

            SetControlData(_selectedLevel.beatmapLevelData.difficultyBeatmapSets, _playerDataModel.playerData.lastSelectedBeatmapCharacteristic);

            levelCoverImage.texture = (await beatmapLevel.GetCoverImageAsync(cancellationToken.Token)).texture;
        }

        public void SetSelectedCharacteristic(string serializedName)
        {
            var characteristicIndex = _beatmapCharacteristics.FindIndex(x => x.serializedName == serializedName);
            characteristicControl.SelectCellWithNumber(characteristicIndex);
            SetSelectedCharacteristic(null, characteristicIndex);
        }

        [UIAction("characteristic-selected")]
        public void SetSelectedCharacteristic(IconSegmentedControl _, int index)
        {
            _playerDataModel.playerData.SetLastSelectedBeatmapCharacteristic(_beatmapCharacteristics[index]);

            var diffBeatmaps = _selectedLevel.beatmapLevelData.GetDifficultyBeatmapSet(_beatmapCharacteristics[index]).difficultyBeatmaps;
            var closestDifficulty = SongUtils.GetClosestDifficultyPreferLower(_selectedLevel, _playerDataModel.playerData.lastSelectedBeatmapDifficulty, _beatmapCharacteristics[index].serializedName);

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

                difficultyControl.SetTexts(difficultyLabels);
            }
            else difficultyControl.SetTexts(diffBeatmaps.Select(x => x.difficulty.ToString().Replace("Plus", "+")).ToArray());

            var diffIndex = Array.FindIndex(diffBeatmaps, x => x.difficulty == closestDifficulty.difficulty);

            difficultyControl.SelectCellWithNumber(diffIndex);
            SetSelectedDifficulty(null, diffIndex);
        }

        public void SetSelectedDifficulty(int difficulty)
        {
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
            difficultyControl.SelectCellWithNumber(difficultyIndex);
            SetSelectedDifficulty(null, difficultyIndex);
        }

        [UIAction("difficulty-selected")]
        public void SetSelectedDifficulty(TextSegmentedControl _, int index)
        {
            _selectedDifficultyBeatmap = _selectedLevel.beatmapLevelData.GetDifficultyBeatmapSet(_playerDataModel.playerData.lastSelectedBeatmapCharacteristic).difficultyBeatmaps[index];
            _playerDataModel.playerData.SetLastSelectedBeatmapDifficulty(_selectedDifficultyBeatmap.difficulty);

            UpdateContent();

            DifficultyBeatmapChanged?.Invoke(_selectedDifficultyBeatmap);
        }

        [UIAction("play-pressed")]
        public void PlayClicked()
        {
            PlayPressed?.Invoke(_selectedLevel, SelectedCharacteristic, SelectedDifficulty);
        }
    }
}
