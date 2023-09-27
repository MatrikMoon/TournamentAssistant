<script lang="ts">
  import { onDestroy } from "svelte";
  import { getAllLevels, isOstName, type Song } from "$lib/services/ostService";
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
  import Button from "@smui/button";
  import { slide } from "svelte/transition";
  import Autocomplete from "@smui-extra/autocomplete";
  import Switch from "@smui/switch";
  import FormField from "@smui/form-field";
  import CircularProgress from "@smui/circular-progress";
  import Select, { Option } from "@smui/select";
  import { BeatSaverService } from "$lib/services/beatSaver/beatSaverService";
  import type { SongInfo } from "$lib/services/beatSaver/songInfo";

  export let serverAddress: string;
  export let serverPort: string;
  export let tournamentId: string;
  export let matchId: string | undefined = undefined;
  export let qualifierId: string | undefined = undefined;

  export let selectedSongId = "";
  export let resultGameplayParameters: GameplayParameters | undefined =
    undefined;
  export let onAddClicked = () => {};

  let localMatchInstance: Match;
  let localQualifierInstance: QualifierEvent;

  let songInfo: SongInfo | undefined = undefined;
  let downloading = false;
  $: expanded = songInfo && selectedSongId.length > 0;

  // If there's only one characteristic, select it by default
  $: selectedCharacteristic =
    songInfo && BeatSaverService.characteristics(songInfo).length == 1
      ? BeatSaverService.characteristics(songInfo)[0]
      : undefined;

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

  $: showCharacteristicDropdown = !selectedCharacteristic;
  // $: showCharacteristicDropdown = true;

  $: if (songInfo && selectedCharacteristic && selectedDifficulty) {
    resultGameplayParameters = {
      beatmap: {
        name: songInfo.name,
        levelId: `custom_level_${songInfo.versions[0].hash.toUpperCase()}`,
        characteristic: {
          serializedName: selectedCharacteristic,
          difficulties: [],
        },
        difficulty: BeatSaverService.getDifficultyAsNumber(selectedDifficulty),
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
        options: GameplayModifiers_GameOptions.NoFail,
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
        matchId
      ))!;
    }

    if (qualifierId) {
      localQualifierInstance = (await $taService.getQualifier(
        serverAddress,
        serverPort,
        tournamentId,
        qualifierId
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
      onMatchOrQualifierChange
    );
    $taService.unsubscribeFromUserUpdates(onMatchOrQualifierChange);
    $taService.unsubscribeFromMatchUpdates(onMatchOrQualifierChange);
    $taService.unsubscribeFromQualifierUpdates(onMatchOrQualifierChange);
  });

  const onLoadClicked = async () => {
    if (isOstName(selectedSongId)) {
    } else {
      downloading = true;
      songInfo = await BeatSaverService.getSongInfo(selectedSongId);
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

  const getOptionLabel = (option: Song) => {
    if (option) {
      return option.levelName;
    }
    return "";
  };
</script>

<div class="add-song">
  <div class="text-box">
    <Autocomplete
      bind:value={selectedSongId}
      on:input={onInputChanged}
      options={getAllLevels()}
      {getOptionLabel}
      label="Song ID"
      combobox
      textfield$variant="outlined"
    />
    {#if !songInfo && !downloading && selectedSongId.length > 0}
      <div class="load-song-button" transition:slide={{ axis: "x" }}>
        <Button variant="raised" on:click={onLoadClicked}>Download</Button>
      </div>
    {/if}
    {#if downloading}
      <div class="progress-indicator" transition:slide={{ axis: "x" }}>
        <CircularProgress style="height: 32px; width: 32px;" indeterminate />
      </div>
    {/if}
    {#if songInfo && !downloading && selectedSongId.length > 0}
      <div class="load-song-button" transition:slide={{ axis: "x" }}>
        <Button variant="raised" on:click={onAddClicked}>Add</Button>
      </div>
    {/if}
  </div>
  <!-- expanded implies songInfo, but for svelte to compile the each loop we need to assert it's not undefined-->
  {#if expanded && songInfo}
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
        <div class="modifiers">
          <FormField>
            <Switch />
            <span slot="label">No Fail</span>
          </FormField>
          <FormField>
            <Switch />
            <span slot="label">Ghost Notes</span>
          </FormField>
          <FormField>
            <Switch />
            <span slot="label">Disappearing Arrows</span>
          </FormField>
          <FormField>
            <Switch />
            <span slot="label">No Bombs</span>
          </FormField>
          <FormField>
            <Switch />
            <span slot="label">No Walls</span>
          </FormField>
          <FormField>
            <Switch />
            <span slot="label">No Arrows</span>
          </FormField>
          <FormField>
            <Switch />
            <span slot="label">Fast Song</span>
          </FormField>
          <FormField>
            <Switch />
            <span slot="label">Super Fast Song</span>
          </FormField>
          <FormField>
            <Switch />
            <span slot="label">Fast Notes</span>
          </FormField>
          <FormField>
            <Switch />
            <span slot="label">Slow Song</span>
          </FormField>
          <FormField>
            <Switch />
            <span slot="label">InstaFail</span>
          </FormField>
          <FormField>
            <Switch />
            <span slot="label">Fail On Saber Clash</span>
          </FormField>
          <FormField>
            <Switch />
            <span slot="label">Battery Energy</span>
          </FormField>
          <FormField>
            <Switch />
            <span slot="label">Pro Mode</span>
          </FormField>
          <FormField>
            <Switch />
            <span slot="label">Zen Mode</span>
          </FormField>
          <FormField>
            <Switch />
            <span slot="label">Small Cubes</span>
          </FormField>
          <FormField>
            <Switch />
            <span slot="label">Strict Angles</span>
          </FormField>
        </div>
        <div class="ta-settings">
          <FormField>
            <Switch />
            <span slot="label">Show Scoreboard</span>
          </FormField>
          <FormField>
            <Switch />
            <span slot="label">Disable Pause</span>
          </FormField>
          <FormField>
            <Switch />
            <span slot="label">Disable Fail</span>
          </FormField>
          <FormField>
            <Switch />
            <span slot="label">Disable Scoresaber Submission</span>
          </FormField>
          <FormField>
            <Switch />
            <span slot="label">Disable Custom Notes on Stream</span>
          </FormField>
        </div>
      </div>
    </div>
  {/if}
</div>

<style lang="scss">
  .add-song {
    .text-box {
      display: flex;
      width: -webkit-fill-available;
    }

    .progress-indicator {
      display: flex;
      align-items: center;
      padding-left: 2vmin;
    }

    .load-song-button {
      padding-left: 2vmin;
      align-self: center;
    }

    .options {
      display: flex;
      flex-wrap: wrap;
      background-color: rgba($color: #000000, $alpha: 0.1);
      border-radius: 0 0 2vmin 2vmin;

      .settings {
        display: flex;

        .modifiers {
          max-width: min-content;
        }

        .ta-settings {
          max-width: min-content;

          span {
            text-wrap: nowrap;
          }
        }
      }
    }
  }
</style>
