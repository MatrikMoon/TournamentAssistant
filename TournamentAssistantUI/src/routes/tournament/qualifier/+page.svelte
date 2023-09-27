<script lang="ts">
  import { onDestroy } from "svelte";
  import { page } from "$app/stores";
  import LayoutGrid, { Cell } from "@smui/layout-grid";
  import AddSong from "$lib/components/AddSong.svelte";
  import FormField from "@smui/form-field";
  import Textfield from "@smui/textfield";
  import FileDrop from "$lib/components/FileDrop.svelte";
  import { taService } from "$lib/stores";
  import {
    QualifierEvent_EventSettings,
    type QualifierEvent,
    GameplayParameters,
  } from "tournament-assistant-client";
  import Switch from "@smui/switch";
  import { onMount } from "svelte";
  import { Icon, Label } from "@smui/button";
  import Fab from "@smui/fab";
  import { v4 as uuidv4 } from "uuid";

  let serverAddress = $page.url.searchParams.get("address")!;
  let serverPort = $page.url.searchParams.get("port")!;
  let tournamentId = $page.url.searchParams.get("tournamentId")!;
  let qualifierId = $page.url.searchParams.get("qualifierId")!;

  let resultGameplayParameters: GameplayParameters | undefined = undefined;

  let editDisabled = false;

  let qualifier: QualifierEvent = {
    guid: "",
    name: "",
    guild: {
      id: "0",
      name: "dummy",
    },
    infoChannel: {
      id: "0",
      name: "dummy",
    },
    qualifierMaps: [],
    flags: 0,
    image: new Uint8Array([1]),
  };

  onMount(async () => {
    console.log("onMount joinTournament/getQualifier");

    await $taService.joinTournament(serverAddress, serverPort, tournamentId);

    await onQualifierChanged();
  });

  async function onQualifierChanged() {
    if (qualifierId != null) {
      qualifier = (await $taService.getQualifier(
        serverAddress,
        serverPort,
        tournamentId,
        qualifierId
      ))!;
    }
  }

  //When changes happen, re-render
  $taService.subscribeToQualifierUpdates(onQualifierChanged);
  onDestroy(() => {
    $taService.unsubscribeFromQualifierUpdates(onQualifierChanged);
  });

  //Don't allow creation unless we have all the required fields
  let canCreate = false;
  $: if (qualifier.name.length > 0) {
    canCreate = true;
  }

  const updateQualifier = async (qualifier: QualifierEvent) => {
    // We only want realtime updates on qualifiers that already exist, so if there's
    // no qualifierId in the path, we'll hold off on this
    if (qualifierId) {
      await $taService.updateQualifier(
        serverAddress,
        serverPort,
        tournamentId,
        qualifier
      );
    }
  };

  const onCreateClicked = async () => {
    await $taService.createQualifier(
      serverAddress,
      serverPort,
      tournamentId,
      qualifier
    );
  };

  const onAddClicked = async () => {
    console.log({ resultGameplayParameters });

    qualifier.qualifierMaps = [
      ...qualifier.qualifierMaps,
      {
        guid: uuidv4(),
        gameplayParameters: resultGameplayParameters,
        disablePause: true,
        attempts: 0,
      },
    ];

    await updateQualifier(qualifier);
  };
</script>

<div>
  <div class="qualifier-title">
    Select a song, difficulty, and characteristic
  </div>
  <LayoutGrid>
    <Cell span={4}>
      <Textfield
        bind:value={qualifier.name}
        variant="outlined"
        label="Qualifier Name"
        disabled={editDisabled}
      />
    </Cell>
    {#if qualifier.infoChannel}
      <Cell span={4}>
        <Textfield
          bind:value={qualifier.infoChannel.id}
          variant="outlined"
          label="Leaderboard Channel ID"
          disabled={editDisabled}
        />
      </Cell>
    {/if}
    <Cell span={4}>
      <FileDrop
        onFileSelected={async (file) => {
          const loadedImage = await file?.arrayBuffer();

          qualifier.image = loadedImage
            ? new Uint8Array(loadedImage)
            : new Uint8Array([1]);
        }}
        disabled={editDisabled}
      />
    </Cell>
    <Cell span={4}>
      <FormField>
        <Switch
          checked={(qualifier.flags &
            QualifierEvent_EventSettings.HideScoresFromPlayers) ===
            QualifierEvent_EventSettings.HideScoresFromPlayers}
          on:SMUISwitch:change={(e) => {
            if (e.detail.selected) {
              qualifier.flags |=
                QualifierEvent_EventSettings.HideScoresFromPlayers;
            } else {
              qualifier.flags &=
                ~QualifierEvent_EventSettings.HideScoresFromPlayers;
            }

            updateQualifier(qualifier);
          }}
        />
        <span slot="label">Hide scores from players</span>
      </FormField>
      <FormField>
        <Switch
          checked={(qualifier.flags &
            QualifierEvent_EventSettings.DisableScoresaberSubmission) ===
            QualifierEvent_EventSettings.DisableScoresaberSubmission}
          on:SMUISwitch:change={(e) => {
            if (e.detail.selected) {
              qualifier.flags |=
                QualifierEvent_EventSettings.DisableScoresaberSubmission;
            } else {
              qualifier.flags &=
                ~QualifierEvent_EventSettings.DisableScoresaberSubmission;
            }

            updateQualifier(qualifier);
          }}
        />
        <span slot="label">Disable Scoresaber submission</span>
      </FormField>
      <FormField>
        <Switch
          checked={(qualifier.flags &
            QualifierEvent_EventSettings.EnableDiscordLeaderboard) ===
            QualifierEvent_EventSettings.EnableDiscordLeaderboard}
          on:SMUISwitch:change={(e) => {
            if (e.detail.selected) {
              qualifier.flags |=
                QualifierEvent_EventSettings.EnableDiscordLeaderboard;
            } else {
              qualifier.flags &=
                ~QualifierEvent_EventSettings.EnableDiscordLeaderboard;
            }

            updateQualifier(qualifier);
          }}
        />
        <span slot="label">Enable discord bot leaderboard</span>
      </FormField>
      <FormField>
        <Switch
          checked={(qualifier.flags &
            QualifierEvent_EventSettings.EnableDiscordScoreFeed) ===
            QualifierEvent_EventSettings.EnableDiscordScoreFeed}
          on:SMUISwitch:change={(e) => {
            if (e.detail.selected) {
              qualifier.flags |=
                QualifierEvent_EventSettings.EnableDiscordScoreFeed;
            } else {
              qualifier.flags &=
                ~QualifierEvent_EventSettings.EnableDiscordScoreFeed;
            }

            updateQualifier(qualifier);
          }}
        />
        <span slot="label">Enable discord bot score feed</span>
      </FormField>
    </Cell>
    <Cell span={4}>
      <AddSong
        {serverAddress}
        {serverPort}
        {tournamentId}
        bind:resultGameplayParameters
        {onAddClicked}
      />
    </Cell>
  </LayoutGrid>

  <div class="create-qualifier-button-container">
    <Fab color="primary" on:click={onCreateClicked} extended>
      <Icon class="material-icons">add</Icon>
      <Label>Create Qualifier</Label>
    </Fab>
  </div>
</div>

<style lang="scss">
  .grid-cell {
    background-color: rgba($color: #000000, $alpha: 0.1);
  }

  .qualifier-title {
    color: var(--mdc-theme-text-primary-on-background);
    background-color: rgba($color: #000000, $alpha: 0.1);
    border-radius: 2vmin;
    text-align: center;
    font-size: 2rem;
    font-weight: 100;
    line-height: 1.1;
    padding: 2vmin;
  }

  .create-qualifier-button-container {
    position: fixed;
    bottom: 2vmin;
    right: 2vmin;
  }
</style>
