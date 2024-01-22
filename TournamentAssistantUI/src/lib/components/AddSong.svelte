<script lang="ts">
  import { onDestroy } from "svelte";
  import { getAllLevels, isOstName } from "$lib/services/ostService";
  import { taService } from "$lib/stores";
  import {
    GameplayModifiers_GameOptions,
    PlayerSpecificSettings_ArcVisibilityType,
    PlayerSpecificSettings_NoteJumpDurationTypeSettings,
    PlayerSpecificSettings_PlayerOptions,
    type GameplayParameters,
    type Match,
    type QualifierEvent,
  } from "tournament-assistant-client";
  import { onMount } from "svelte";
  import { Icon } from "@smui/button";
  import { slide } from "svelte/transition";
  import Autocomplete from "@smui-extra/autocomplete";
  import Switch from "@smui/switch";
  import FormField from "@smui/form-field";
  import CircularProgress from "@smui/circular-progress";
  import Select, { Option } from "@smui/select";
  import { BeatSaverService } from "$lib/services/beatSaver/beatSaverService";
  import type { SongInfo } from "$lib/services/beatSaver/songInfo";
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

  export let serverAddress: string;
  export let serverPort: string;
  export let tournamentId: string;
  export let matchId: string | undefined = undefined;
  export let qualifierId: string | undefined = undefined;

  export let selectedSongId = "";
  export let downloadError = false;
  export let resultGameplayParameters: GameplayParameters | undefined =
    undefined;
  export let onAddClicked = (
    showScoreboard: boolean,
    disablePause: boolean,
    disableFail: boolean,
    disableScoresaberSubmission: boolean,
    disableCustomNotesOnStream: boolean,
    attempts: number,
  ) => {};

  let showScoreboard = false;
  let disablePause = false;
  let disableFail = false;
  let disableScoresaberSubmission = false;
  let disableCustomNotesOnStream = false;
  let attempts = 0;

  let localMatchInstance: Match;
  let localQualifierInstance: QualifierEvent;

  let songInfo: SongInfo | undefined = undefined;
  $: currentVersion = songInfo
    ? BeatSaverService.currentVersion(songInfo)
    : undefined;
  let downloading = false;
  $: expanded = songInfo && selectedSongId.length > 0;

  // If there's only one characteristic, select it by default
  // $: selectedCharacteristic =
  //   songInfo && BeatSaverService.characteristics(songInfo).length == 1
  //     ? BeatSaverService.characteristics(songInfo)[0]
  //     : undefined;

  let selectedCharacteristic: string | undefined = undefined;

  // $: showCharacteristicDropdown = !selectedCharacteristic;

  let showCharacteristicDropdown = true;

  // If there's a characteristic selected, auto-select the highest difficulty
  // $: selectedDifficulty =
  //   songInfo && selectedCharacteristic
  //     ? getClosestDifficultyPreferLower(
  //         songInfo,
  //         selectedCharacteristic,
  //         "ExpertPlus"
  //       )
  //     : undefined;

  let selectedDifficulty: string | undefined = undefined;

  $: if (songInfo) {
    resultGameplayParameters = {
      beatmap: {
        name: songInfo.name,
        levelId: `custom_level_${currentVersion!.hash.toUpperCase()}`,
        characteristic: {
          serializedName: selectedCharacteristic ?? "",
          difficulties: [],
        },
        difficulty: BeatSaverService.getDifficultyAsNumber(
          selectedDifficulty ?? "ExpertPlus",
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
        options:
          resultGameplayParameters?.gameplayModifiers?.options ??
          GameplayModifiers_GameOptions.None,
      },
    };
  }

  onMount(async () => {
    console.log("onMount getMatch/getQualifier");
    await onMatchOrQualifierChange();
  });

  async function onMatchOrQualifierChange() {
    if (matchId) {
      localMatchInstance = (await $taService.getMatch(
        serverAddress,
        serverPort,
        tournamentId,
        matchId,
      ))!;
    }

    if (qualifierId) {
      localQualifierInstance = (await $taService.getQualifier(
        serverAddress,
        serverPort,
        tournamentId,
        qualifierId,
      ))!;
    }
  }

  //When changes happen, re-render
  $taService.client.on("joinedTournament", onMatchOrQualifierChange);
  $taService.subscribeToUserUpdates(onMatchOrQualifierChange);
  $taService.subscribeToMatchUpdates(onMatchOrQualifierChange);
  $taService.subscribeToQualifierUpdates(onMatchOrQualifierChange);
  onDestroy(() => {
    $taService.client.removeListener(
      "joinedTournament",
      onMatchOrQualifierChange,
    );
    $taService.unsubscribeFromUserUpdates(onMatchOrQualifierChange);
    $taService.unsubscribeFromMatchUpdates(onMatchOrQualifierChange);
    $taService.unsubscribeFromQualifierUpdates(onMatchOrQualifierChange);
  });

  const onLoadClicked = async () => {
    if (isOstName(selectedSongId)) {
    } else {
      downloading = true;

      try {
        songInfo = await BeatSaverService.getSongInfo(selectedSongId);
        downloadError = false;
      } catch {
        downloadError = true;
      }

      downloading = false;

      console.log({ songInfo });
    }
  };

  const onInputChanged = () => {
    // When the input changes, reset the loaded song info and chosen settings
    songInfo = undefined;
    selectedCharacteristic = undefined;
    selectedDifficulty = undefined;
    resultGameplayParameters = undefined;
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

      <!-- expanded implies songInfo, but for svelte to compile the each loop we need to assert it's not undefined-->
      {#if expanded && songInfo && currentVersion}
        <List class="preview-list" twoLine avatarList singleSelection>
          <Item class="preview-item">
            <Graphic
              style="background-image: url({currentVersion.coverURL}); background-size: contain"
            />
            <Text>
              <PrimaryText>{songInfo.name}</PrimaryText>
              <SecondaryText>{songInfo.metadata.levelAuthorName}</SecondaryText>
            </Text>
            <!-- <Meta class="material-icons">info</Meta> -->
          </Item>
        </List>
        <div class="options" transition:slide>
          <div class="characteristic-difficulty-dropdowns">
            {#if showCharacteristicDropdown}
              <div class="characteristic">
                <Select
                  bind:value={selectedCharacteristic}
                  key={(item) => item}
                  label="Characteristic"
                  variant="outlined"
                >
                  {#each BeatSaverService.characteristics(songInfo) as characteristic}
                    <Option value={characteristic}>{characteristic}</Option>
                  {/each}
                </Select>
              </div>
            {/if}
            {#if selectedCharacteristic}
              <div class="difficulty">
                <Select
                  bind:value={selectedDifficulty}
                  key={(item) => item}
                  label="Difficulty"
                  variant="outlined"
                >
                  {#each BeatSaverService.getDifficultiesAsArray(songInfo, selectedCharacteristic) as difficulty}
                    <Option value={difficulty}>{difficulty}</Option>
                  {/each}
                </Select>
              </div>
            {/if}
          </div>
          <div class="settings">
            {#if resultGameplayParameters && resultGameplayParameters.gameplayModifiers}
              <div class="modifiers">
                <FormField>
                  <Switch
                    checked={(resultGameplayParameters.gameplayModifiers
                      .options &
                      GameplayModifiers_GameOptions.NoFail) ===
                      GameplayModifiers_GameOptions.NoFail}
                    on:SMUISwitch:change={(e) => {
                      if (
                        resultGameplayParameters &&
                        resultGameplayParameters.gameplayModifiers
                      ) {
                        if (e.detail.selected) {
                          resultGameplayParameters.gameplayModifiers.options |=
                            GameplayModifiers_GameOptions.NoFail;
                        } else {
                          resultGameplayParameters.gameplayModifiers.options &=
                            ~GameplayModifiers_GameOptions.NoFail;
                        }
                      }
                    }}
                  />
                  <span slot="label">No Fail</span>
                </FormField>
                <FormField>
                  <Switch
                    checked={(resultGameplayParameters.gameplayModifiers
                      .options &
                      GameplayModifiers_GameOptions.GhostNotes) ===
                      GameplayModifiers_GameOptions.GhostNotes}
                    on:SMUISwitch:change={(e) => {
                      if (
                        resultGameplayParameters &&
                        resultGameplayParameters.gameplayModifiers
                      ) {
                        if (e.detail.selected) {
                          resultGameplayParameters.gameplayModifiers.options |=
                            GameplayModifiers_GameOptions.GhostNotes;
                        } else {
                          resultGameplayParameters.gameplayModifiers.options &=
                            ~GameplayModifiers_GameOptions.GhostNotes;
                        }
                      }
                    }}
                  />
                  <span slot="label">Ghost Notes</span>
                </FormField>
                <FormField>
                  <Switch
                    checked={(resultGameplayParameters.gameplayModifiers
                      .options &
                      GameplayModifiers_GameOptions.DisappearingArrows) ===
                      GameplayModifiers_GameOptions.DisappearingArrows}
                    on:SMUISwitch:change={(e) => {
                      if (
                        resultGameplayParameters &&
                        resultGameplayParameters.gameplayModifiers
                      ) {
                        if (e.detail.selected) {
                          resultGameplayParameters.gameplayModifiers.options |=
                            GameplayModifiers_GameOptions.DisappearingArrows;
                        } else {
                          resultGameplayParameters.gameplayModifiers.options &=
                            ~GameplayModifiers_GameOptions.DisappearingArrows;
                        }
                      }
                    }}
                  />
                  <span slot="label">Disappearing Arrows</span>
                </FormField>
                <FormField>
                  <Switch
                    checked={(resultGameplayParameters.gameplayModifiers
                      .options &
                      GameplayModifiers_GameOptions.NoBombs) ===
                      GameplayModifiers_GameOptions.NoBombs}
                    on:SMUISwitch:change={(e) => {
                      if (
                        resultGameplayParameters &&
                        resultGameplayParameters.gameplayModifiers
                      ) {
                        if (e.detail.selected) {
                          resultGameplayParameters.gameplayModifiers.options |=
                            GameplayModifiers_GameOptions.NoBombs;
                        } else {
                          resultGameplayParameters.gameplayModifiers.options &=
                            ~GameplayModifiers_GameOptions.NoBombs;
                        }
                      }
                    }}
                  />
                  <span slot="label">No Bombs</span>
                </FormField>
                <FormField>
                  <Switch
                    checked={(resultGameplayParameters.gameplayModifiers
                      .options &
                      GameplayModifiers_GameOptions.NoObstacles) ===
                      GameplayModifiers_GameOptions.NoObstacles}
                    on:SMUISwitch:change={(e) => {
                      if (
                        resultGameplayParameters &&
                        resultGameplayParameters.gameplayModifiers
                      ) {
                        if (e.detail.selected) {
                          resultGameplayParameters.gameplayModifiers.options |=
                            GameplayModifiers_GameOptions.NoObstacles;
                        } else {
                          resultGameplayParameters.gameplayModifiers.options &=
                            ~GameplayModifiers_GameOptions.NoObstacles;
                        }
                      }
                    }}
                  />
                  <span slot="label">No Walls</span>
                </FormField>
                <FormField>
                  <Switch
                    checked={(resultGameplayParameters.gameplayModifiers
                      .options &
                      GameplayModifiers_GameOptions.NoArrows) ===
                      GameplayModifiers_GameOptions.NoArrows}
                    on:SMUISwitch:change={(e) => {
                      if (
                        resultGameplayParameters &&
                        resultGameplayParameters.gameplayModifiers
                      ) {
                        if (e.detail.selected) {
                          resultGameplayParameters.gameplayModifiers.options |=
                            GameplayModifiers_GameOptions.NoArrows;
                        } else {
                          resultGameplayParameters.gameplayModifiers.options &=
                            ~GameplayModifiers_GameOptions.NoArrows;
                        }
                      }
                    }}
                  />
                  <span slot="label">No Arrows</span>
                </FormField>
                <FormField>
                  <Switch
                    checked={(resultGameplayParameters.gameplayModifiers
                      .options &
                      GameplayModifiers_GameOptions.FastSong) ===
                      GameplayModifiers_GameOptions.FastSong}
                    on:SMUISwitch:change={(e) => {
                      if (
                        resultGameplayParameters &&
                        resultGameplayParameters.gameplayModifiers
                      ) {
                        if (e.detail.selected) {
                          resultGameplayParameters.gameplayModifiers.options |=
                            GameplayModifiers_GameOptions.FastSong;
                        } else {
                          resultGameplayParameters.gameplayModifiers.options &=
                            ~GameplayModifiers_GameOptions.FastSong;
                        }
                      }
                    }}
                  />
                  <span slot="label">Fast Song</span>
                </FormField>
                <FormField>
                  <Switch
                    checked={(resultGameplayParameters.gameplayModifiers
                      .options &
                      GameplayModifiers_GameOptions.SuperFastSong) ===
                      GameplayModifiers_GameOptions.SuperFastSong}
                    on:SMUISwitch:change={(e) => {
                      if (
                        resultGameplayParameters &&
                        resultGameplayParameters.gameplayModifiers
                      ) {
                        if (e.detail.selected) {
                          resultGameplayParameters.gameplayModifiers.options |=
                            GameplayModifiers_GameOptions.SuperFastSong;
                        } else {
                          resultGameplayParameters.gameplayModifiers.options &=
                            ~GameplayModifiers_GameOptions.SuperFastSong;
                        }
                      }
                    }}
                  />
                  <span slot="label">Super Fast Song</span>
                </FormField>
                <FormField>
                  <Switch
                    checked={(resultGameplayParameters.gameplayModifiers
                      .options &
                      GameplayModifiers_GameOptions.FastNotes) ===
                      GameplayModifiers_GameOptions.FastNotes}
                    on:SMUISwitch:change={(e) => {
                      if (
                        resultGameplayParameters &&
                        resultGameplayParameters.gameplayModifiers
                      ) {
                        if (e.detail.selected) {
                          resultGameplayParameters.gameplayModifiers.options |=
                            GameplayModifiers_GameOptions.FastNotes;
                        } else {
                          resultGameplayParameters.gameplayModifiers.options &=
                            ~GameplayModifiers_GameOptions.FastNotes;
                        }
                      }
                    }}
                  />
                  <span slot="label">Fast Notes</span>
                </FormField>
                <FormField>
                  <Switch
                    checked={(resultGameplayParameters.gameplayModifiers
                      .options &
                      GameplayModifiers_GameOptions.SlowSong) ===
                      GameplayModifiers_GameOptions.SlowSong}
                    on:SMUISwitch:change={(e) => {
                      if (
                        resultGameplayParameters &&
                        resultGameplayParameters.gameplayModifiers
                      ) {
                        if (e.detail.selected) {
                          resultGameplayParameters.gameplayModifiers.options |=
                            GameplayModifiers_GameOptions.SlowSong;
                        } else {
                          resultGameplayParameters.gameplayModifiers.options &=
                            ~GameplayModifiers_GameOptions.SlowSong;
                        }
                      }
                    }}
                  />
                  <span slot="label">Slow Song</span>
                </FormField>
                <FormField>
                  <Switch
                    checked={(resultGameplayParameters.gameplayModifiers
                      .options &
                      GameplayModifiers_GameOptions.InstaFail) ===
                      GameplayModifiers_GameOptions.InstaFail}
                    on:SMUISwitch:change={(e) => {
                      if (
                        resultGameplayParameters &&
                        resultGameplayParameters.gameplayModifiers
                      ) {
                        if (e.detail.selected) {
                          resultGameplayParameters.gameplayModifiers.options |=
                            GameplayModifiers_GameOptions.InstaFail;
                        } else {
                          resultGameplayParameters.gameplayModifiers.options &=
                            ~GameplayModifiers_GameOptions.InstaFail;
                        }
                      }
                    }}
                  />
                  <span slot="label">InstaFail</span>
                </FormField>
                <FormField>
                  <Switch
                    checked={(resultGameplayParameters.gameplayModifiers
                      .options &
                      GameplayModifiers_GameOptions.FailOnClash) ===
                      GameplayModifiers_GameOptions.FailOnClash}
                    on:SMUISwitch:change={(e) => {
                      if (
                        resultGameplayParameters &&
                        resultGameplayParameters.gameplayModifiers
                      ) {
                        if (e.detail.selected) {
                          resultGameplayParameters.gameplayModifiers.options |=
                            GameplayModifiers_GameOptions.FailOnClash;
                        } else {
                          resultGameplayParameters.gameplayModifiers.options &=
                            ~GameplayModifiers_GameOptions.FailOnClash;
                        }
                      }
                    }}
                  />
                  <span slot="label">Fail On Saber Clash</span>
                </FormField>
                <FormField>
                  <Switch
                    checked={(resultGameplayParameters.gameplayModifiers
                      .options &
                      GameplayModifiers_GameOptions.BatteryEnergy) ===
                      GameplayModifiers_GameOptions.BatteryEnergy}
                    on:SMUISwitch:change={(e) => {
                      if (
                        resultGameplayParameters &&
                        resultGameplayParameters.gameplayModifiers
                      ) {
                        if (e.detail.selected) {
                          resultGameplayParameters.gameplayModifiers.options |=
                            GameplayModifiers_GameOptions.BatteryEnergy;
                        } else {
                          resultGameplayParameters.gameplayModifiers.options &=
                            ~GameplayModifiers_GameOptions.BatteryEnergy;
                        }
                      }
                    }}
                  />
                  <span slot="label">Battery Energy</span>
                </FormField>
                <FormField>
                  <Switch
                    checked={(resultGameplayParameters.gameplayModifiers
                      .options &
                      GameplayModifiers_GameOptions.ProMode) ===
                      GameplayModifiers_GameOptions.ProMode}
                    on:SMUISwitch:change={(e) => {
                      if (
                        resultGameplayParameters &&
                        resultGameplayParameters.gameplayModifiers
                      ) {
                        if (e.detail.selected) {
                          resultGameplayParameters.gameplayModifiers.options |=
                            GameplayModifiers_GameOptions.ProMode;
                        } else {
                          resultGameplayParameters.gameplayModifiers.options &=
                            ~GameplayModifiers_GameOptions.ProMode;
                        }
                      }
                    }}
                  />
                  <span slot="label">Pro Mode</span>
                </FormField>
                <FormField>
                  <Switch
                    checked={(resultGameplayParameters.gameplayModifiers
                      .options &
                      GameplayModifiers_GameOptions.ZenMode) ===
                      GameplayModifiers_GameOptions.ZenMode}
                    on:SMUISwitch:change={(e) => {
                      if (
                        resultGameplayParameters &&
                        resultGameplayParameters.gameplayModifiers
                      ) {
                        if (e.detail.selected) {
                          resultGameplayParameters.gameplayModifiers.options |=
                            GameplayModifiers_GameOptions.ZenMode;
                        } else {
                          resultGameplayParameters.gameplayModifiers.options &=
                            ~GameplayModifiers_GameOptions.ZenMode;
                        }
                      }
                    }}
                  />
                  <span slot="label">Zen Mode</span>
                </FormField>
                <FormField>
                  <Switch
                    checked={(resultGameplayParameters.gameplayModifiers
                      .options &
                      GameplayModifiers_GameOptions.SmallCubes) ===
                      GameplayModifiers_GameOptions.SmallCubes}
                    on:SMUISwitch:change={(e) => {
                      if (
                        resultGameplayParameters &&
                        resultGameplayParameters.gameplayModifiers
                      ) {
                        if (e.detail.selected) {
                          resultGameplayParameters.gameplayModifiers.options |=
                            GameplayModifiers_GameOptions.SmallCubes;
                        } else {
                          resultGameplayParameters.gameplayModifiers.options &=
                            ~GameplayModifiers_GameOptions.SmallCubes;
                        }
                      }
                    }}
                  />
                  <span slot="label">Small Cubes</span>
                </FormField>
                <FormField>
                  <Switch
                    checked={(resultGameplayParameters.gameplayModifiers
                      .options &
                      GameplayModifiers_GameOptions.StrictAngles) ===
                      GameplayModifiers_GameOptions.StrictAngles}
                    on:SMUISwitch:change={(e) => {
                      if (
                        resultGameplayParameters &&
                        resultGameplayParameters.gameplayModifiers
                      ) {
                        if (e.detail.selected) {
                          resultGameplayParameters.gameplayModifiers.options |=
                            GameplayModifiers_GameOptions.StrictAngles;
                        } else {
                          resultGameplayParameters.gameplayModifiers.options &=
                            ~GameplayModifiers_GameOptions.StrictAngles;
                        }
                      }
                    }}
                  />
                  <span slot="label">Strict Angles</span>
                </FormField>
              </div>
            {/if}

            {#if resultGameplayParameters && resultGameplayParameters.playerSettings}
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
            {/if}

            <Wrapper>
              <Fab
                class="add-fab"
                color={selectedDifficulty ? "primary" : "secondary"}
                on:click={() =>
                  onAddClicked(
                    showScoreboard,
                    disablePause,
                    disableFail,
                    disableScoresaberSubmission,
                    disableCustomNotesOnStream,
                    attempts,
                  )}
                extended
                disabled={!selectedDifficulty}
              >
                <Icon class="material-icons">add</Icon>
                <Label>{localMatchInstance ? "Load Song" : "Add Song"}</Label>
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

  {#if !songInfo && selectedSongId.length > 0}
    <div class="download-fab" transition:slide={{ axis: "x" }}>
      <Fab color="primary" mini on:click={onLoadClicked}>
        {#if downloading}
          <CircularProgress style="height: 32px; width: 32px;" indeterminate />
        {:else}
          <Icon class="material-icons">arrow_downward</Icon>
        {/if}
      </Fab>
    </div>
  {/if}
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

    .download-fab {
      margin-left: 10px;

      :global(circle) {
        stroke: white;
      }
    }

    :global(.preview-list) {
      padding: 0;

      :global(.preview-item) {
        background-color: rgba($color: #000000, $alpha: 0.1);
      }
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
