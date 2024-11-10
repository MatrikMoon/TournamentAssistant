#pragma warning disable CS0649
#pragma warning disable IDE0044
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.ViewControllers;
using BGLib.Polyglot;
using HMUI;
using IPA.Utilities.Async;
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
        public event Action<BeatmapKey> DifficultyBeatmapChanged;
        public event Action<BeatmapLevel, BeatmapKey> PlayPressed;

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
        private BeatmapKey? _selectedDifficultyBeatmapKey;

        private BeatmapDifficulty SelectedDifficulty { get { return _selectedDifficultyBeatmapKey?.difficulty ?? BeatmapDifficulty.ExpertPlus; } }
        private BeatmapCharacteristicSO SelectedCharacteristic { get { return _selectedDifficultyBeatmapKey?.beatmapCharacteristic; } }

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

        public void SetControlData(IEnumerable<BeatmapKey> difficultyBeatmapSets, BeatmapCharacteristicSO selectedBeatmapCharacteristic)
        {
            throw new NotImplementedException();

            _beatmapCharacteristics.Clear();
            //var beatmapSetList = new List<difficultyBeatmapSets>(difficultyBeatmapSets);
            //beatmapSetList.Sort((IDifficultyBeatmapSet a, IDifficultyBeatmapSet b) => a.beatmapCharacteristic.sortingOrder.CompareTo(b.beatmapCharacteristic.sortingOrder));

            foreach(var key in difficultyBeatmapSets)
            {
                if(!_beatmapCharacteristics.Contains(key.beatmapCharacteristic))
                    _beatmapCharacteristics.Add(key.beatmapCharacteristic);
            }

            var itemArray = difficultyBeatmapSets.Select(beatmapSet => new IconSegmentedControl.DataItem(beatmapSet.beatmapCharacteristic.icon, Localization.Get(beatmapSet.beatmapCharacteristic.descriptionLocalizationKey))).ToArray();
            //var selectedIndex = Math.Max(0, beatmapSetList.FindIndex(x => x.beatmapCharacteristic == selectedBeatmapCharacteristic));
            var selectedIndex = 0;
            //Literally no fucking clue

            characteristicControl.SetData(itemArray);
            characteristicControl.SelectCellWithNumber(selectedIndex);
            SetSelectedCharacteristic(null, selectedIndex);
        }

        public void UpdateContent()
        {
            if (_selectedLevel != null)
            {
                songNameText.text = _selectedLevel.songName;

                var duration = TimeSpan.FromSeconds(_selectedLevel.audioClip.length);

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

                if (_selectedDifficultyBeatmapKey != null)
                {
                    Task.Run(async () =>
                    {
                        var beatmapData = _selectedLevel.GetDifficultyBeatmapData(_selectedDifficultyBeatmapKey.Value.beatmapCharacteristic, _selectedDifficultyBeatmapKey.Value.difficulty);

                        await UnityMainThreadTaskScheduler.Factory.StartNew(() =>
                        {
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
                            npsText.text = (beatmapData.notesCount / _selectedLevel.beatmapLevelData.audioClip.length).ToString("0.00");
                            njsText.text = njs.ToString("0.0#");
                            jumpDistanceText.text = jumpDistance.ToString("0.0#");
                            notesCountText.text = beatmapData.notesCount.ToString();
                            obstaclesCountText.text = beatmapData.obstaclesCount.ToString();
                            bombsCountText.text = beatmapData.bombsCount.ToString();
                        });
                    });
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

            _selectedLevel = selectedLevel;
            controlsRect.gameObject.SetActive(true);
            charactertisticControlBlocker.gameObject.SetActive(DisableCharacteristicControl);
            difficultyControlBlocker.gameObject.SetActive(DisableDifficultyControl);

            SetBeatmapLevel(_selectedLevel);
        }

        private void SetBeatmapLevel(BeatmapLevel beatmapLevel)
        {
            var beatmapKeys = beatmapLevel.GetBeatmapKeys();
            if (beatmapKeys.Any(x => x.beatmapCharacteristic == _playerDataModel.playerData.lastSelectedBeatmapCharacteristic))
            {
                _selectedDifficultyBeatmapKey = SongUtils.GetClosestDifficultyPreferLower(beatmapLevel, _playerDataModel.playerData.lastSelectedBeatmapDifficulty, _playerDataModel.playerData.lastSelectedBeatmapCharacteristic.serializedName);
            }
            else if (beatmapKeys.Count() > 0)
            {
                _selectedDifficultyBeatmapKey = SongUtils.GetClosestDifficultyPreferLower(beatmapLevel, _playerDataModel.playerData.lastSelectedBeatmapDifficulty, beatmapKeys.FirstOrDefault().beatmapCharacteristic.serializedName);
            }

            UpdateContent();

            _playerDataModel.playerData.SetLastSelectedBeatmapCharacteristic(_selectedDifficultyBeatmapKey.Value.beatmapCharacteristic);
            _playerDataModel.playerData.SetLastSelectedBeatmapDifficulty(_selectedDifficultyBeatmapKey.Value.difficulty);

            SetControlData(beatmapKeys, _playerDataModel.playerData.lastSelectedBeatmapCharacteristic);

            Task.Run(async () =>
            {
                await UnityMainThreadTaskScheduler.Factory.StartNew(async () =>
                {
                    var coverImage = await beatmapLevel.GetCoverImageAsync(cancellationToken.Token);
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
            var characteristic = _beatmapCharacteristics[index];
            _playerDataModel.playerData.SetLastSelectedBeatmapCharacteristic(characteristic);

            var diffs = _selectedLevel.GetDifficulties(characteristic);
            var diffArray = diffs.ToArray();
            var diffCount = diffs.Count();
            var closestDifficulty = SongUtils.GetClosestDifficultyPreferLower(_selectedLevel, _playerDataModel.playerData.lastSelectedBeatmapDifficulty, _beatmapCharacteristics[index].serializedName);

            var extraData = Collections.RetrieveExtraSongData(Collections.hashForLevelID(_selectedLevel.levelID));
            if (extraData != null)
            {
                string[] difficultyLabels = new string[diffCount];

                var extraDifficulties = extraData._difficulties.Where(x => x._beatmapCharacteristicName == _beatmapCharacteristics[index].serializedName || x._beatmapCharacteristicName == _beatmapCharacteristics[index].characteristicNameLocalizationKey);

                for (int i = 0; i < diffCount; i++)
                {
                    var customDiff = extraDifficulties.FirstOrDefault(x => x._difficulty == diffArray[i]);
                    if (customDiff != null && !string.IsNullOrEmpty(customDiff._difficultyLabel)) difficultyLabels[i] = customDiff._difficultyLabel;
                    else difficultyLabels[i] = diffArray[i].ToString().Replace("Plus", "+");
                }

                difficultyControl.SetTexts(difficultyLabels);
            }
            else difficultyControl.SetTexts(diffs.Select(x => x.ToString().Replace("Plus", "+")).ToArray());

            var diffIndex = Array.FindIndex(diffArray, x => x == closestDifficulty.difficulty);

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
                        .difficultyBeatmaps.ToArray(),
                    x => x.difficulty == (BeatmapDifficulty)difficulty
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
            var diffs = _selectedLevel.GetDifficulties(_playerDataModel.playerData.lastSelectedBeatmapCharacteristic).ToArray();

            if (index >= 0 && index < diffs.Count())
            {
                _selectedDifficultyBeatmapKey = _selectedLevel.GetBeatmapKeys().Where(x => x.beatmapCharacteristic == _playerDataModel.playerData.lastSelectedBeatmapCharacteristic && x.difficulty == diffs[index]).FirstOrDefault();
                _playerDataModel.playerData.SetLastSelectedBeatmapDifficulty(_selectedDifficultyBeatmapKey.Value.difficulty);

                UpdateContent();

                DifficultyBeatmapChanged?.Invoke(_selectedDifficultyBeatmapKey.Value);
            }
        }

        [UIAction("play-pressed")]
        public void PlayClicked()
        {
            PlayPressed?.Invoke(_selectedLevel, _selectedDifficultyBeatmapKey.Value);
        }
    }
}
