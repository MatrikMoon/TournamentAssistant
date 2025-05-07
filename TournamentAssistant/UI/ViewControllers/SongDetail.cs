#pragma warning disable CS0649
#pragma warning disable IDE0044
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.ViewControllers;
using HMUI;
using IPA.Utilities.Async;
using BGLib.Polyglot;
using SongCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TMPro;
using TournamentAssistant.Utilities;
using UnityEngine;
using UnityEngine.UI;

namespace TournamentAssistant.UI.ViewControllers
{
    internal class SongDetail : BSMLAutomaticViewController
    {
        public event Action<BeatmapKey> KeyChanged;
        public event Action<BeatmapKey, BeatmapLevel> PlayPressed;

        public bool DisableCharacteristicControl { get; set; }
        public bool DisableDifficultyControl { get; set; }

        private bool _disablePlayButton = false;
        public bool DisablePlayButton
        {
            get
            {
                return _disablePlayButton;
            }

            set
            {
                _disablePlayButton = value;

                buttonsRect?.gameObject.SetActive(!value);
            }
        }

        private BeatmapLevel _selectedLevel;
        private BeatmapKey? _selectedKey;

        private BeatmapDifficulty SelectedDifficulty { get { return _selectedKey?.difficulty ?? BeatmapDifficulty.ExpertPlus; } }
        private BeatmapCharacteristicSO SelectedCharacteristic { get { return _selectedKey?.beatmapCharacteristic; } }

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
        [UIComponent("njs-text")]
        public TextMeshProUGUI njsText;
        [UIComponent("jump-distance-text")]
        public TextMeshProUGUI jumpDistanceText;

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

        public void SetControlData(BeatmapKey[] keys, BeatmapCharacteristicSO selectedBeatmapCharacteristic)
        {
            _beatmapCharacteristics.Clear();
            var keyList = new List<BeatmapKey>(keys);
            keyList.Sort((BeatmapKey a, BeatmapKey b) => a.beatmapCharacteristic.sortingOrder.CompareTo(b.beatmapCharacteristic.sortingOrder));
            _beatmapCharacteristics.AddRange(keyList.Select(x => x.beatmapCharacteristic));

            var itemArray = keyList.Select(key => new IconSegmentedControl.DataItem(key.beatmapCharacteristic.icon, Localization.Get(key.beatmapCharacteristic.descriptionLocalizationKey))).ToArray();
            var selectedIndex = Math.Max(0, keyList.FindIndex(x => x.beatmapCharacteristic == selectedBeatmapCharacteristic));

            characteristicControl.SetData(itemArray);
            characteristicControl.SelectCellWithNumber(selectedIndex);
            SetSelectedCharacteristic(null, selectedIndex);
        }

        public void UpdateContent()
        {
            if (_selectedLevel != null)
            {
                songNameText.text = _selectedLevel.songName;

                var duration = TimeSpan.FromSeconds(_selectedLevel.songDuration);

                static string FormatDuration(TimeSpan duration)
                {
                    if (duration.Hours > 0)
                    {
                        return duration.ToString(@"hh\:mm\:ss");
                    }
                    else if (duration.Minutes > 0)
                    {
                        return duration.ToString(@"mm\:ss");
                    }
                    else
                    {
                        return duration.ToString(@"ss");
                    }
                }

                durationText.text = FormatDuration(duration);
                bpmText.text = Mathf.RoundToInt(_selectedLevel.beatsPerMinute).ToString();

                if (_selectedKey != null)
                {
                    var beatmapData = _selectedLevel.beatmapBasicData[(_selectedKey.Value.beatmapCharacteristic, _selectedKey.Value.difficulty)];

                    var njs = 0f;
                    if (beatmapData.noteJumpMovementSpeed != 0)
                    {
                        njs = beatmapData.noteJumpMovementSpeed;
                    }
                    else
                    {
                        njs = BeatmapDifficultyMethods.NoteJumpMovementSpeed(SelectedDifficulty);
                    }

                    var jumpDistance = SongUtils.GetJumpDistance(_selectedLevel.beatsPerMinute, njs, beatmapData.noteJumpStartBeatOffset);
                    npsText.text = (beatmapData.notesCount / _selectedLevel.songDuration).ToString("0.00");
                    njsText.text = njs.ToString("0.0#");
                    jumpDistanceText.text = jumpDistance.ToString("0.0#");
                    notesCountText.text = beatmapData.notesCount.ToString();
                    obstaclesCountText.text = beatmapData.obstaclesCount.ToString();
                    bombsCountText.text = beatmapData.bombsCount.ToString();
                }
                else
                {
                    npsText.text = "--";
                    njsText.text = "--";
                    jumpDistanceText.text = "--";
                    notesCountText.text = "--";
                    obstaclesCountText.text = "--";
                    bombsCountText.text = "--";
                }
            }
        }

        public void SetSelectedSong(BeatmapLevel selectedLevel)
        {
            buttonsRect.gameObject.SetActive(!DisablePlayButton);

            controlsRect.gameObject.SetActive(true);
            charactertisticControlBlocker.gameObject.SetActive(DisableCharacteristicControl);
            difficultyControlBlocker.gameObject.SetActive(DisableDifficultyControl);

            SetBeatmapLevel(selectedLevel);
        }

        private void SetBeatmapLevel(BeatmapLevel beatmapLevel)
        {
            _selectedLevel = beatmapLevel;

            if (beatmapLevel.GetBeatmapKeys().Any(x => x.beatmapCharacteristic == _playerDataModel.playerData.lastSelectedBeatmapCharacteristic))
            {
                _selectedKey = SongUtils.GetClosestDifficultyPreferLower(beatmapLevel, _playerDataModel.playerData.lastSelectedBeatmapDifficulty, _playerDataModel.playerData.lastSelectedBeatmapCharacteristic.serializedName);
            }
            else if (beatmapLevel.GetBeatmapKeys().Count() > 0)
            {
                _selectedKey = SongUtils.GetClosestDifficultyPreferLower(beatmapLevel, _playerDataModel.playerData.lastSelectedBeatmapDifficulty, beatmapLevel.GetBeatmapKeys().First().beatmapCharacteristic.serializedName);
            }

            UpdateContent();

            _playerDataModel.playerData.SetLastSelectedBeatmapCharacteristic(_selectedKey.Value.beatmapCharacteristic);
            _playerDataModel.playerData.SetLastSelectedBeatmapDifficulty(_selectedKey.Value.difficulty);

            SetControlData(_selectedLevel.GetBeatmapKeys().ToArray(), _playerDataModel.playerData.lastSelectedBeatmapCharacteristic);

            Task.Run(async () =>
            {
                await UnityMainThreadTaskScheduler.Factory.StartNew(async () =>
                {
                    var coverImage = await beatmapLevel.previewMediaData.GetCoverSpriteAsync(cancellationToken.Token);
                    levelCoverImage.texture = coverImage.texture;
                });
            });
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

            var difficulties = _selectedLevel.GetDifficulties(_beatmapCharacteristics[index]);
            var closestDifficulty = SongUtils.GetClosestDifficultyPreferLower(_selectedLevel, _playerDataModel.playerData.lastSelectedBeatmapDifficulty, _beatmapCharacteristics[index].serializedName);

            var extraData = Collections.RetrieveExtraSongData(Collections.hashForLevelID(_selectedLevel.levelID));
            if (extraData != null)
            {
                string[] difficultyLabels = new string[difficulties.Count()];

                var extraDifficulties = extraData._difficulties.Where(x => x._beatmapCharacteristicName == _beatmapCharacteristics[index].serializedName || x._beatmapCharacteristicName == _beatmapCharacteristics[index].characteristicNameLocalizationKey);

                for (int i = 0; i < difficulties.Count(); i++)
                {
                    var customDiff = extraDifficulties.FirstOrDefault(x => x._difficulty == difficulties.ElementAt(i));
                    if (customDiff != null && !string.IsNullOrEmpty(customDiff._difficultyLabel)) difficultyLabels[i] = customDiff._difficultyLabel;
                    else difficultyLabels[i] = difficulties.ElementAt(i).ToString().Replace("Plus", "+");
                }

                difficultyControl.SetTexts(difficultyLabels);
            }
            else difficultyControl.SetTexts(difficulties.Select(x => x.ToString().Replace("Plus", "+")).ToArray());

            var diffIndex = Array.FindIndex(difficulties.ToArray(), x => x == closestDifficulty.difficulty);

            difficultyControl.SelectCellWithNumber(diffIndex);
            SetSelectedDifficulty(null, diffIndex);
        }

        public void SetSelectedDifficulty(int difficulty)
        {
            var difficultyIndex = Array.FindIndex(
                    _selectedLevel
                        .GetDifficulties(
                            _playerDataModel
                                .playerData
                                .lastSelectedBeatmapCharacteristic
                        )
                        .ToArray(),
                    x => x == (BeatmapDifficulty)difficulty
                );

            if (difficultyIndex > -1)
            {
                difficultyControl.SelectCellWithNumber(difficultyIndex);
                SetSelectedDifficulty(null, difficultyIndex);
            }
        }

        [UIAction("difficulty-selected")]
        public void SetSelectedDifficulty(TextSegmentedControl _, int index)
        {
            var difficulties = _selectedLevel.GetDifficulties(_playerDataModel.playerData.lastSelectedBeatmapCharacteristic);

            if (index >= 0 && index < difficulties.Count())
            {
                _selectedKey = _selectedLevel.GetBeatmapKeys().FirstOrDefault(x => x.difficulty == difficulties.ElementAt(index));
                _playerDataModel.playerData.SetLastSelectedBeatmapDifficulty(_selectedKey.Value.difficulty);

                UpdateContent();

                KeyChanged?.Invoke(_selectedKey.Value);
            }
        }

        [UIAction("play-pressed")]
        public void PlayClicked()
        {
            PlayPressed?.Invoke(_selectedKey.Value, _selectedLevel);
        }
    }
}
