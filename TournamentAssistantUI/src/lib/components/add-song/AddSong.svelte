<script lang="ts">
  import { getAllLevels, isOstName } from "$lib/services/ostService";
  import {
    GameplayModifiers_GameOptions,
    PlayerSpecificSettings_ArcVisibilityType,
    PlayerSpecificSettings_NoteJumpDurationTypeSettings,
    PlayerSpecificSettings_PlayerOptions,
    type GameplayParameters,
  } from "tournament-assistant-client";
  import { Icon } from "@smui/button";
  import { slide } from "svelte/transition";
  import Autocomplete from "@smui-extra/autocomplete";
  import Switch from "@smui/switch";
  import FormField from "@smui/form-field";
  import CircularProgress from "@smui/circular-progress";
  import Select, { Option } from "@smui/select";
  import { BeatSaverService } from "$lib/services/beatSaver/beatSaverService";
  import type { SongInfo, Version } from "$lib/services/beatSaver/songInfo";
  import Paper from "@smui/paper";
  import Fab, { Label } from "@smui/fab";
  import { Input } from "@smui/textfield";
  import Tooltip, { Wrapper } from "@smui/tooltip";
  import List, {
    Item,
    Graphic,
    Text,
    PrimaryText,
    SecondaryText,
  } from "@smui/list";
  import { PlaylistService } from "$lib/services/bplist/playlistService";
  import type { Playlist } from "$lib/services/bplist/playlist";
  import GameOptionSwitch from "./GameOptionSwitch.svelte";

  export let selectedSongId = "";
  export let downloadError = false;
  export let resultGameplayParameters: GameplayParameters[] | undefined =
    undefined;
  export let onSongsAdded = (result: GameplayParameters[]) => {};

  const onAddClicked = (result: GameplayParameters[]) => {
    // Run a pass through the playlist to be sure we've selected
    // the best option we can for the user's desired characteristic
    // and difficulty
    for (let song of result) {
      const songInfo = songInfoList.find(
        (x) =>
          `custom_level_${BeatSaverService.currentVersion(x)?.hash.toUpperCase()}` ===
          song.beatmap?.levelId,
      )!;

      const characteristics = BeatSaverService.characteristics(songInfo);
      song.beatmap!.characteristic!.serializedName =
        characteristics.find((x) => x === selectedCharacteristic) ??
        characteristics.find((x) => x === "Standard") ??
        characteristics[0];

      // Get the default difficulty. Prefer ExpertPlus
      song.beatmap!.difficulty = BeatSaverService.getDifficultyAsNumber(
        BeatSaverService.getClosestDifficultyPreferLower(
          songInfo,
          song.beatmap!.characteristic!.serializedName,
          selectedDifficulty ?? "ExpertPlus",
        )!,
      );

      // Set the TA settings
      song.attempts = attempts;
      song.showScoreboard = showScoreboard;
      song.disablePause = disablePause;
      song.disableFail = disableFail;
      song.disableScoresaberSubmission = disableScoresaberSubmission;
      song.disableCustomNotesOnStream = disableCustomNotesOnStream;
    }

    onSongsAdded(result);
    onInputChanged();
    selectedSongId = "";
  };

  let fileInput: HTMLInputElement | undefined;
  let playlist: Playlist | undefined;
  let addingPlaylist = false;
  let downloadedPlaylist = false;

  let showScoreboard = false;
  let disablePause = false;
  let disableFail = false;
  let disableScoresaberSubmission = false;
  let disableCustomNotesOnStream = false;
  let attempts = 0;

  let songInfoList: SongInfo[] = [];
  let downloading = false;
  $: expanded =
    songInfoList.length > 0 &&
    (selectedSongId.length > 0 || (addingPlaylist && downloadedPlaylist));

  let selectedCharacteristic: string | undefined;
  let selectedDifficulty: string | undefined;

  const downloadSongAndAddToResults = async (songId: string) => {
    if (isOstName(songId)) {
    } else {
      downloading = true;

      try {
        const songInfo = await BeatSaverService.getSongInfo(songId);
        songInfoList = [...songInfoList, songInfo];
        const currentVersion = BeatSaverService.currentVersion(songInfo);

        // Get the default characteristic. Prefer "Standard", or choose\
        // first if it doesn't exist
        const characteristics = BeatSaverService.characteristics(songInfo);
        selectedCharacteristic =
          characteristics.find((x) => x === "Standard") ?? characteristics[0];

        // Get the default difficulty. Prefer ExpertPlus
        selectedDifficulty = BeatSaverService.getClosestDifficultyPreferLower(
          songInfo,
          selectedCharacteristic,
          "ExpertPlus",
        );

        // Add acquired info to results list
        resultGameplayParameters = [
          ...(resultGameplayParameters ?? []),
          {
            beatmap: {
              name: songInfo.name,
              levelId: `custom_level_${currentVersion!.hash.toUpperCase()}`,
              characteristic: {
                serializedName: selectedCharacteristic ?? "",
                difficulties: [],
              },
              difficulty: BeatSaverService.getDifficultyAsNumber(
                selectedDifficulty!,
              ),
            },
            playerSettings: {
              playerHeight: 0,
              sfxVolume: 0,
              saberTrailIntensity: 0,
              noteJumpStartBeatOffset: 0,
              noteJumpFixedDuration: 0,
              options: PlayerSpecificSettings_PlayerOptions.NoPlayerOptions,
              noteJumpDurationTypeSettings:
                PlayerSpecificSettings_NoteJumpDurationTypeSettings.Dynamic,
              arcVisibilityType: PlayerSpecificSettings_ArcVisibilityType.None,
            },
            gameplayModifiers: {
              options: GameplayModifiers_GameOptions.None,
            },
            attempts,
            showScoreboard,
            disablePause,
            disableFail,
            disableScoresaberSubmission,
            disableCustomNotesOnStream,
          },
        ];

        downloadError = false;
      } catch (e) {
        console.error(e);
        downloadError = true;
      }

      downloading = false;
    }
  };

  const onDownloadClicked = async () => {
    selectedSongId = BeatSaverService.sanitizeSongId(selectedSongId);
    downloadSongAndAddToResults(selectedSongId);
  };

  const onLoadFromPlaylistClicked = async () => {
    fileInput?.addEventListener("change", handleFileChange);
    fileInput?.click();
  };

  const handleFileChange = async (event: Event) => {
    downloadedPlaylist = false;
    addingPlaylist = true;

    try {
      const files = (event.target as HTMLInputElement).files;
      if (files && files.length > 0) {
        playlist = await PlaylistService.loadPlaylist(files[0]);

        for (let song of playlist.songs) {
          await downloadSongAndAddToResults(song.key);
        }
      }

      downloadedPlaylist = true;
    } catch (e) {
      console.error(e);
      addingPlaylist = false;
    }
  };

  const onInputChanged = () => {
    // When the input changes, reset the loaded song info and chosen settings
    songInfoList = [];
    selectedCharacteristic = undefined;
    selectedDifficulty = undefined;
    resultGameplayParameters = undefined;
    playlist = undefined;
    downloadError = false;
    addingPlaylist = false;
  };
</script>

<div class="add-song">
  <Wrapper>
    <Paper
      class={downloadError ? "text-box-paper invalid" : "text-box-paper"}
      elevation={6}
    >
      <div class="text-box-input-group">
        <div class="search-icon">
          <Icon class="material-icons">search</Icon>
        </div>
        <div class="search-autocomplete">
          <Autocomplete
            bind:text={selectedSongId}
            options={getAllLevels()}
            getOptionLabel={(option) => option?.levelName ?? ""}
            label="Song ID"
            combobox
            textfield$variant="outlined"
          >
            <Input
              bind:value={selectedSongId}
              on:input={onInputChanged}
              placeholder="Song ID"
              class="text-box-input"
            />
          </Autocomplete>
        </div>
        <div class="action-buttons">
          {#if selectedSongId.length === 0 && !downloadedPlaylist}
            <div
              class="add-from-playlist-fab"
              in:slide={{ axis: "x", delay: 250 }}
              out:slide={{ axis: "x" }}
            >
              <Wrapper>
                <input
                  type="file"
                  bind:this={fileInput}
                  accept=".bplist"
                  hidden
                />
                <Fab
                  color="primary"
                  mini
                  on:click={() => {
                    if (!addingPlaylist) {
                      onLoadFromPlaylistClicked();
                    }
                  }}
                >
                  {#if addingPlaylist && !downloadedPlaylist}
                    <CircularProgress
                      style="height: 32px; width: 32px;"
                      indeterminate
                    />
                  {:else}
                    <Icon class="material-icons">playlist_add</Icon>
                  {/if}
                </Fab>
                <Tooltip>Load from Playlist</Tooltip>
              </Wrapper>
            </div>
          {/if}
          {#if selectedSongId.length > 0 && !addingPlaylist}
            <div
              class="download-fab"
              in:slide={{ axis: "x", delay: 250 }}
              out:slide={{ axis: "x" }}
            >
              <Fab
                color="primary"
                mini
                on:click={() => {
                  if (!downloading) {
                    onDownloadClicked();
                  }
                }}
              >
                {#if downloading}
                  <CircularProgress
                    style="height: 32px; width: 32px;"
                    indeterminate
                  />
                {:else}
                  <Icon class="material-icons">arrow_downward</Icon>
                {/if}
              </Fab>
            </div>
          {/if}
        </div>
      </div>

      <!-- expanded implies songInfo, but for svelte to compile the each loop we need to assert it's not undefined-->
      {#if expanded}
        {#if !addingPlaylist}
          <List class="preview-list" twoLine avatarList singleSelection>
            <Item class="preview-item">
              <Graphic
                style="background-image: url({BeatSaverService.currentVersion(
                  songInfoList[songInfoList.length - 1],
                )?.coverURL}); background-size: contain"
              />
              <Text>
                <PrimaryText
                  >{songInfoList[songInfoList.length - 1].name}</PrimaryText
                >
                <SecondaryText>
                  {songInfoList[songInfoList.length - 1].metadata
                    .levelAuthorName}
                </SecondaryText>
              </Text>
              <!-- <Meta class="material-icons">info</Meta> -->
            </Item>
          </List>
        {/if}
        <div class="options" transition:slide>
          {#if addingPlaylist}
            <div class="adding-playlist-title">
              The settings you choose here will apply to all songs in the
              playlist. If a song doesn't have the difficulty you choose, it
              will use the closest difficulty it can
            </div>
          {/if}
          <div class="characteristic-difficulty-dropdowns">
            <div class="characteristic">
              <Select
                bind:value={selectedCharacteristic}
                key={(item) => item}
                label="Characteristic"
                variant="outlined"
              >
                {#each BeatSaverService.characteristics(songInfoList[songInfoList.length - 1]) as characteristic}
                  <Option value={characteristic}>{characteristic}</Option>
                {/each}
              </Select>
            </div>
            {#if selectedCharacteristic}
              <div class="difficulty">
                <Select
                  bind:value={selectedDifficulty}
                  key={(item) => item}
                  label="Difficulty"
                  variant="outlined"
                >
                  {#each BeatSaverService.getDifficultiesAsArray(songInfoList[songInfoList.length - 1], selectedCharacteristic) as difficulty}
                    <Option value={difficulty}>{difficulty}</Option>
                  {/each}
                </Select>
              </div>
            {/if}
          </div>
          <div class="settings">
            <div class="modifiers">
              <FormField>
                <GameOptionSwitch
                  bind:resultGameplayParameters
                  gameOption={GameplayModifiers_GameOptions.NoFail}
                />
                <span slot="label">No Fail</span>
              </FormField>
              <FormField>
                <GameOptionSwitch
                  bind:resultGameplayParameters
                  gameOption={GameplayModifiers_GameOptions.GhostNotes}
                />
                <span slot="label">Ghost Notes</span>
              </FormField>
              <FormField>
                <GameOptionSwitch
                  bind:resultGameplayParameters
                  gameOption={GameplayModifiers_GameOptions.DisappearingArrows}
                />
                <span slot="label">Disappearing Arrows</span>
              </FormField>
              <FormField>
                <GameOptionSwitch
                  bind:resultGameplayParameters
                  gameOption={GameplayModifiers_GameOptions.NoBombs}
                />
                <span slot="label">No Bombs</span>
              </FormField>
              <FormField>
                <GameOptionSwitch
                  bind:resultGameplayParameters
                  gameOption={GameplayModifiers_GameOptions.NoObstacles}
                />
                <span slot="label">No Walls</span>
              </FormField>
              <FormField>
                <GameOptionSwitch
                  bind:resultGameplayParameters
                  gameOption={GameplayModifiers_GameOptions.NoArrows}
                />
                <span slot="label">No Arrows</span>
              </FormField>
              <FormField>
                <GameOptionSwitch
                  bind:resultGameplayParameters
                  gameOption={GameplayModifiers_GameOptions.FastSong}
                />
                <span slot="label">Fast Song</span>
              </FormField>
              <FormField>
                <GameOptionSwitch
                  bind:resultGameplayParameters
                  gameOption={GameplayModifiers_GameOptions.SuperFastSong}
                />
                <span slot="label">Super Fast Song</span>
              </FormField>
              <FormField>
                <GameOptionSwitch
                  bind:resultGameplayParameters
                  gameOption={GameplayModifiers_GameOptions.FastNotes}
                />
                <span slot="label">Fast Notes</span>
              </FormField>
              <FormField>
                <GameOptionSwitch
                  bind:resultGameplayParameters
                  gameOption={GameplayModifiers_GameOptions.SlowSong}
                />
                <span slot="label">Slow Song</span>
              </FormField>
              <FormField>
                <GameOptionSwitch
                  bind:resultGameplayParameters
                  gameOption={GameplayModifiers_GameOptions.InstaFail}
                />
                <span slot="label">InstaFail</span>
              </FormField>
              <FormField>
                <GameOptionSwitch
                  bind:resultGameplayParameters
                  gameOption={GameplayModifiers_GameOptions.FailOnClash}
                />
                <span slot="label">Fail On Saber Clash</span>
              </FormField>
              <FormField>
                <GameOptionSwitch
                  bind:resultGameplayParameters
                  gameOption={GameplayModifiers_GameOptions.BatteryEnergy}
                />
                <span slot="label">Battery Energy</span>
              </FormField>
              <FormField>
                <GameOptionSwitch
                  bind:resultGameplayParameters
                  gameOption={GameplayModifiers_GameOptions.ProMode}
                />
                <span slot="label">Pro Mode</span>
              </FormField>
              <FormField>
                <GameOptionSwitch
                  bind:resultGameplayParameters
                  gameOption={GameplayModifiers_GameOptions.ZenMode}
                />
                <span slot="label">Zen Mode</span>
              </FormField>
              <FormField>
                <GameOptionSwitch
                  bind:resultGameplayParameters
                  gameOption={GameplayModifiers_GameOptions.SmallCubes}
                />
                <span slot="label">Small Cubes</span>
              </FormField>
              <FormField>
                <GameOptionSwitch
                  bind:resultGameplayParameters
                  gameOption={GameplayModifiers_GameOptions.StrictAngles}
                />
                <span slot="label">Strict Angles</span>
              </FormField>
            </div>

            <div class="ta-settings">
              <FormField>
                <Switch
                  checked={showScoreboard}
                  on:SMUISwitch:change={(e) => {
                    showScoreboard = e.detail.selected;
                  }}
                />
                <span slot="label">Show Scoreboard</span>
              </FormField>
              <FormField>
                <Switch
                  checked={disablePause}
                  on:SMUISwitch:change={(e) => {
                    disablePause = e.detail.selected;
                  }}
                />
                <span slot="label">Disable Pause</span>
              </FormField>
              <FormField>
                <Switch
                  checked={disableFail}
                  on:SMUISwitch:change={(e) => {
                    disableFail = e.detail.selected;
                  }}
                />
                <span slot="label">Disable Fail</span>
              </FormField>
              <FormField>
                <Switch
                  checked={disableScoresaberSubmission}
                  on:SMUISwitch:change={(e) => {
                    disableScoresaberSubmission = e.detail.selected;
                  }}
                />
                <span slot="label">Disable Scoresaber Submission</span>
              </FormField>
              <FormField>
                <Switch
                  checked={disableCustomNotesOnStream}
                  on:SMUISwitch:change={(e) => {
                    disableCustomNotesOnStream = e.detail.selected;
                  }}
                />
                <span slot="label">Disable Custom Notes on Stream</span>
              </FormField>
            </div>

            <Wrapper>
              <Fab
                class="add-fab"
                color={selectedDifficulty ? "primary" : "secondary"}
                on:click={() =>
                  resultGameplayParameters &&
                  onAddClicked(resultGameplayParameters)}
                extended
                disabled={!selectedDifficulty}
              >
                <Icon class="material-icons">add</Icon>
                <Label>{addingPlaylist ? "Add Songs" : "Add Song"}</Label>
              </Fab>
              <Tooltip>Select a difficulty first</Tooltip>
            </Wrapper>
          </div>
        </div>
      {/if}
    </Paper>
    <!-- Already tried doing this by removing the element with svelte, and it threw
         a DOM error. So now it's done with a CSS class. It works, don't touch it -->
    <Tooltip class={downloadError ? "" : "tooltip-hidden"}>
      Is that song ID correct?
    </Tooltip>
  </Wrapper>
</div>

<style lang="scss">
  .add-song {
    display: flex;
    justify-content: center;
    align-items: center;

    :global(.text-box-paper.invalid) {
      background-color: rgba($color: red, $alpha: 0.1);
    }

    :global(.text-box-paper) {
      padding: 0;
      background-color: rgba($color: #000000, $alpha: 0.1);
      min-width: min-content;
      width: -webkit-fill-available;

      :global(.text-box-input-group) {
        display: flex;
        align-items: center;
        flex-grow: 1;
        margin: 0 12px;
        padding: 0 12px;
        height: 48px;

        // Remove the shadow we've set for all Autocompletes in app.scss
        :global(.smui-autocomplete) {
          box-shadow: none;
        }

        :global(> *) {
          display: inline-block;
          margin: 0 12px;
        }

        :global(.text-box-input::placeholder) {
          color: var(--mdc-theme-text-secondary-on-background);
          opacity: 0.6;
        }
      }
    }

    .search-icon {
      margin-top: 5px; // Again don't ask
      color: var(--mdc-theme-text-secondary-on-background);
    }

    .search-autocomplete {
      width: -webkit-fill-available;
    }

    .action-buttons {
      // The following two are needed to make the transition look nice
      display: flex;
      align-items: center;
      margin: 0;

      .download-fab,
      .add-from-playlist-fab {
        :global(circle) {
          stroke: white;
        }
      }
    }

    :global(.preview-list) {
      padding: 0;

      :global(.preview-item) {
        background-color: rgba($color: #000000, $alpha: 0.1);
      }
    }

    .adding-playlist-title {
      color: var(--mdc-theme-text-primary-on-background);
      background-color: rgba($color: #000000, $alpha: 0.1);
      border-radius: 2vmin;
      text-align: center;
      font-weight: 100;
      line-height: 1.1;
      padding: 2vmin;
      margin: 2vmin 2vmin 0vmin 2vmin;
    }

    .options {
      display: flex;
      min-width: min-content;
      flex-wrap: wrap;
      background-color: rgba($color: #000000, $alpha: 0.1);

      .characteristic-difficulty-dropdowns {
        width: -webkit-fill-available;
        padding: 15px;

        > div {
          padding: 5px;
        }
      }

      .settings {
        display: flex;
        min-width: min-content;
        width: -webkit-fill-available;
        justify-content: center;
        position: relative;

        > div {
          padding: 5px;
        }

        span {
          text-wrap: nowrap;
        }

        .modifiers,
        .ta-settings {
          max-width: min-content;
          height: fit-content;

          margin: 8px;
          padding: 10px;

          border-radius: 5px;
          background-color: rgba($color: #000000, $alpha: 0.1);
        }

        :global(.add-fab) {
          position: absolute;
          right: 0;
          bottom: 0;
          margin: 5px;
        }
      }
    }
  }

  :global(.tooltip-hidden) {
    display: none;
  }
</style>
