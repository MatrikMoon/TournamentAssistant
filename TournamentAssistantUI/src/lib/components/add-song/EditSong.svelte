<script lang="ts">
  import {
    GameplayModifiers_GameOptions,
    type GameplayParameters,
  } from "tournament-assistant-client";
  import { Icon } from "@smui/button";
  import { slide } from "svelte/transition";
  import Switch from "@smui/switch";
  import FormField from "@smui/form-field";
  import Select, { Option } from "@smui/select";
  import { BeatSaverService } from "$lib/services/beatSaver/beatSaverService";
  import type { SongInfo } from "$lib/services/beatSaver/songInfo";
  import Fab, { Label } from "@smui/fab";
  import Textfield from "@smui/textfield";
  import Tooltip, { Wrapper } from "@smui/tooltip";
  import List, {
    Item,
    Graphic,
    Text,
    PrimaryText,
    SecondaryText,
  } from "@smui/list";
  import GameOptionSwitch from "./GameOptionSwitch.svelte";

  export let edit = false;
  export let showMatchOnlyOptions = true;
  export let showQualifierOnlyOptions = true;
  export let showTargetTextbox = false;
  export let gameplayParameters: GameplayParameters[] | undefined = undefined;
  export let songInfoList: SongInfo[] = [];
  export let addingPlaylistOrPool = false;
  export let onSongsAdded = (result: GameplayParameters[]) => {};

  const _allCharacteristics = ["Standard"];
  const _allDifficulties = ["Easy", "Normal", "Hard", "Expert", "ExpertPlus"];

  let selectedCharacteristic: string | undefined;
  let selectedDifficulty: string | undefined;

  $: attempts = gameplayParameters?.[0].attempts ?? "0"; // Has to be string since it's bound to a textbox
  $: target = gameplayParameters?.[0].target ?? "0"; // Has to be string since it's bound to a textbox
  $: showAttemptTextbox = gameplayParameters?.some((x) => x.attempts > 0);

  const onAttemptsTextChanged = (event: CustomEvent<InputEvent>) => {
    const newValue = Number((event.target as HTMLTextAreaElement)?.value);
    if (newValue) {
      gameplayParameters?.forEach((x) => (x.attempts = newValue));
      attempts = newValue; // roundabout way of updating this value because we need to change all instances of gameplayParameters when it's changed
    }
  };

  const onTargetTextChanged = (event: CustomEvent<InputEvent>) => {
    const newValue = Number((event.target as HTMLTextAreaElement)?.value);
    if (newValue) {
      gameplayParameters?.forEach((x) => (x.target = newValue));
      target = newValue; // roundabout way of updating this value because we need to change all instances of gameplayParameters when it's changed
    }
  };

  const resetComponent = () => {
    selectedCharacteristic = undefined;
    selectedDifficulty = undefined;
    gameplayParameters = undefined;
    songInfoList = [];
  };

  const onAddClicked = (result: GameplayParameters[]) => {
    // Run a pass through the playlist to be sure we've selected
    // the best option we can for the user's desired characteristic
    // and difficulty
    for (let song of result) {
      const songInfo = songInfoList.find(
        (x) =>
          `custom_level_${BeatSaverService.currentVersion(x)?.hash.toUpperCase()}` ===
          song.beatmap?.levelId
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
          selectedDifficulty ?? "ExpertPlus"
        )!
      );

      // Set the TA settings
      song.attempts = showAttemptTextbox ? Number(attempts) : song.attempts;
      song.target = showTargetTextbox ? Number(target) : song.target;
    }

    resetComponent();
    onSongsAdded(result);
  };

  $: if (!selectedCharacteristic) {
    if (gameplayParameters?.length === 1) {
      // Finding acceptable defaults was done in AddSong when downloading the song,
      // or has alerady been set by the user if we're editing a map
      selectedCharacteristic =
        gameplayParameters[0].beatmap!.characteristic!.serializedName;
    } else if ((gameplayParameters?.length ?? 0) > 1) {
      selectedCharacteristic =
        _allCharacteristics[_allCharacteristics.length - 1];
    }
  }

  $: if (!selectedDifficulty && selectedCharacteristic) {
    if (gameplayParameters?.length === 1) {
      // Finding acceptable defaults was done in AddSong when downloading the song,
      // or has alerady been set by the user if we're editing a map
      selectedDifficulty = BeatSaverService.getDifficultyAsString(
        gameplayParameters[0].beatmap!.difficulty
      );
    } else if ((gameplayParameters?.length ?? 0) > 1) {
      selectedDifficulty = _allDifficulties[_allDifficulties.length - 1];
    }
  }
</script>

{#if gameplayParameters && songInfoList.length > 0}
  {#if !addingPlaylistOrPool}
    <List class="preview-list" twoLine avatarList singleSelection>
      <Item class="preview-item">
        <Graphic
          style="background-image: url({BeatSaverService.currentVersion(
            songInfoList[songInfoList.length - 1]
          )?.coverURL}); background-size: contain"
        />
        <Text>
          <PrimaryText>
            {songInfoList[songInfoList.length - 1].name}
          </PrimaryText>
          <SecondaryText>
            {songInfoList[songInfoList.length - 1].metadata.levelAuthorName}
          </SecondaryText>
        </Text>
        <!-- <Meta class="material-icons">info</Meta> -->
      </Item>
    </List>
  {/if}

  <div class="options" transition:slide>
    {#if addingPlaylistOrPool}
      <div class="adding-playlist-title">
        The settings you choose here will apply to all songs in the playlist. If
        a song doesn't have the difficulty you choose, it will use the closest
        difficulty it can
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
          {#each addingPlaylistOrPool ? _allCharacteristics : BeatSaverService.characteristics(songInfoList[songInfoList.length - 1]) as characteristic}
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
            {#each addingPlaylistOrPool ? _allDifficulties : BeatSaverService.getDifficultiesAsArray(songInfoList[songInfoList.length - 1], selectedCharacteristic) as difficulty}
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
            bind:gameplayParameters
            disabled={gameplayParameters.some((x) => x.disableFail)}
            gameOption={GameplayModifiers_GameOptions.NoFail}
          />
          <span slot="label">No Fail</span>
        </FormField>
        <FormField>
          <GameOptionSwitch
            bind:gameplayParameters
            gameOption={GameplayModifiers_GameOptions.GhostNotes}
          />
          <span slot="label">Ghost Notes</span>
        </FormField>
        <FormField>
          <GameOptionSwitch
            bind:gameplayParameters
            gameOption={GameplayModifiers_GameOptions.DisappearingArrows}
          />
          <span slot="label">Disappearing Arrows</span>
        </FormField>
        <FormField>
          <GameOptionSwitch
            bind:gameplayParameters
            gameOption={GameplayModifiers_GameOptions.NoBombs}
          />
          <span slot="label">No Bombs</span>
        </FormField>
        <FormField>
          <GameOptionSwitch
            bind:gameplayParameters
            gameOption={GameplayModifiers_GameOptions.NoObstacles}
          />
          <span slot="label">No Walls</span>
        </FormField>
        <FormField>
          <GameOptionSwitch
            bind:gameplayParameters
            gameOption={GameplayModifiers_GameOptions.NoArrows}
          />
          <span slot="label">No Arrows</span>
        </FormField>
        <FormField>
          <GameOptionSwitch
            bind:gameplayParameters
            gameOption={GameplayModifiers_GameOptions.FastSong}
          />
          <span slot="label">Fast Song</span>
        </FormField>
        <FormField>
          <GameOptionSwitch
            bind:gameplayParameters
            gameOption={GameplayModifiers_GameOptions.SuperFastSong}
          />
          <span slot="label">Super Fast Song</span>
        </FormField>
        <FormField>
          <GameOptionSwitch
            bind:gameplayParameters
            gameOption={GameplayModifiers_GameOptions.FastNotes}
          />
          <span slot="label">Fast Notes</span>
        </FormField>
        <FormField>
          <GameOptionSwitch
            bind:gameplayParameters
            gameOption={GameplayModifiers_GameOptions.SlowSong}
          />
          <span slot="label">Slow Song</span>
        </FormField>
        <FormField>
          <GameOptionSwitch
            bind:gameplayParameters
            gameOption={GameplayModifiers_GameOptions.InstaFail}
          />
          <span slot="label">InstaFail</span>
        </FormField>
        <FormField>
          <GameOptionSwitch
            bind:gameplayParameters
            gameOption={GameplayModifiers_GameOptions.FailOnClash}
          />
          <span slot="label">Fail On Saber Clash</span>
        </FormField>
        <FormField>
          <GameOptionSwitch
            bind:gameplayParameters
            gameOption={GameplayModifiers_GameOptions.BatteryEnergy}
          />
          <span slot="label">Battery Energy</span>
        </FormField>
        <FormField>
          <GameOptionSwitch
            bind:gameplayParameters
            gameOption={GameplayModifiers_GameOptions.ProMode}
          />
          <span slot="label">Pro Mode</span>
        </FormField>
        <FormField>
          <GameOptionSwitch
            bind:gameplayParameters
            gameOption={GameplayModifiers_GameOptions.ZenMode}
          />
          <span slot="label">Zen Mode</span>
        </FormField>
        <FormField>
          <GameOptionSwitch
            bind:gameplayParameters
            gameOption={GameplayModifiers_GameOptions.SmallCubes}
          />
          <span slot="label">Small Cubes</span>
        </FormField>
        <FormField>
          <GameOptionSwitch
            bind:gameplayParameters
            gameOption={GameplayModifiers_GameOptions.StrictAngles}
          />
          <span slot="label">Strict Angles</span>
        </FormField>
      </div>

      <div>
        <div class="ta-settings">
          {#if showQualifierOnlyOptions}
            <FormField>
              <Switch
                checked={showAttemptTextbox}
                on:SMUISwitch:change={(e) => {
                  showAttemptTextbox = e.detail.selected;

                  if (!showAttemptTextbox && gameplayParameters) {
                    gameplayParameters.forEach((x) => (x.attempts = 0));
                  }
                }}
              />
              <span slot="label">Limited Attempts</span>
            </FormField>
          {/if}
          <!-- {#if showMatchOnlyOptions}
            <FormField>
              <Switch
                checked={gameplayParameters.some((x) => x.showScoreboard)}
                on:SMUISwitch:change={(e) => {
                  if (gameplayParameters) {
                    gameplayParameters.forEach(
                      (x) => (x.showScoreboard = e.detail.selected),
                    );
                  }
                }}
              />
              <span slot="label">Show Scoreboard</span>
            </FormField>
          {/if} -->
          <FormField>
            <Switch
              checked={gameplayParameters.some((x) => x.disablePause)}
              on:SMUISwitch:change={(e) => {
                if (gameplayParameters) {
                  gameplayParameters.forEach(
                    (x) => (x.disablePause = e.detail.selected)
                  );
                }
              }}
            />
            <span slot="label">Disable Pause</span>
          </FormField>
          {#if showMatchOnlyOptions}
            <FormField>
              <Switch
                checked={gameplayParameters.some((x) => x.disableFail)}
                on:SMUISwitch:change={(e) => {
                  if (gameplayParameters) {
                    gameplayParameters.forEach(
                      (x) => (x.disableFail = e.detail.selected)
                    );
                  }
                }}
              />
              <span slot="label">Disable Fail</span>
            </FormField>
          {/if}
          <FormField>
            <Switch
              checked={gameplayParameters.some(
                (x) => x.disableScoresaberSubmission
              )}
              on:SMUISwitch:change={(e) => {
                if (gameplayParameters) {
                  gameplayParameters.forEach(
                    (x) => (x.disableScoresaberSubmission = e.detail.selected)
                  );
                }
              }}
            />
            <span slot="label">Disable Scoresaber Submission</span>
          </FormField>
          {#if showMatchOnlyOptions}
            <FormField>
              <Switch
                checked={gameplayParameters.some(
                  (x) => x.disableCustomNotesOnStream
                )}
                on:SMUISwitch:change={(e) => {
                  if (gameplayParameters) {
                    gameplayParameters.forEach(
                      (x) => (x.disableCustomNotesOnStream = e.detail.selected)
                    );
                  }
                }}
              />
              <span slot="label">Disable Custom Notes on Stream</span>
            </FormField>
          {/if}
        </div>
        {#if showAttemptTextbox}
          <div class="limited-attempts-textbox" transition:slide>
            <Textfield
              value={attempts}
              on:input={onAttemptsTextChanged}
              variant="outlined"
              label="Number of attempts"
            />
          </div>
        {/if}
        {#if showTargetTextbox}
          <div class="target-textbox" transition:slide>
            <Textfield
              value={target}
              on:input={onTargetTextChanged}
              variant="outlined"
              label="Targeted Score (based on your leaderboard sort settings)"
            />
          </div>
        {/if}
      </div>

      <Wrapper>
        <Fab
          class="add-fab"
          color={selectedDifficulty ? "primary" : "secondary"}
          on:click={() =>
            gameplayParameters && onAddClicked(gameplayParameters)}
          extended
          disabled={!selectedDifficulty}
        >
          <Icon class="material-icons">add</Icon>
          <Label>
            {edit
              ? "Update Song"
              : addingPlaylistOrPool
                ? "Add Songs"
                : "Add Song"}
          </Label>
        </Fab>
        <Tooltip>Select a difficulty first</Tooltip>
      </Wrapper>
    </div>
  </div>
{/if}

<style lang="scss">
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
      width: -moz-available;
      padding: 15px;

      > div {
        padding: 5px;
      }
    }

    .settings {
      display: flex;
      min-width: min-content;
      width: -webkit-fill-available;
      width: -moz-available;
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

      .limited-attempts-textbox,
      .target-textbox {
        margin: 8px;
      }

      :global(.add-fab) {
        position: absolute;
        right: 0;
        bottom: 0;
        margin: 5px;
      }
    }
  }
</style>
