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
    Response_ResponseType,
  } from "tournament-assistant-client";
  import Switch from "@smui/switch";
  import { onMount } from "svelte";
  import Button, { Icon, Label } from "@smui/button";
  import Fab from "@smui/fab";
  import { v4 as uuidv4 } from "uuid";
  import { slide } from "svelte/transition";
  import { goto } from "$app/navigation";
  import { Workbook } from "exceljs";
  import { saveAs } from "file-saver";

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
    if (qualifierId) {
      const newQualifier = await $taService.getQualifier(
        serverAddress,
        serverPort,
        tournamentId,
        qualifierId
      );

      // If the change was deleting the qualifier, throw us back out of this page
      if (newQualifier) {
        qualifier = newQualifier;
      } else {
        returnToQualifierSelection();
      }
    }
  }

  //When changes happen, re-render
  $taService.subscribeToQualifierUpdates(onQualifierChanged);
  onDestroy(() => {
    $taService.unsubscribeFromQualifierUpdates(onQualifierChanged);
  });

  //Don't allow creation unless we have all the required fields
  // let canCreate = false;
  // $: if (qualifier.name.length > 0) {
  //   canCreate = true;
  // }

  const returnToQualifierSelection = () => {
    goto(
      `/tournament/qualifier-select?tournamentId=${tournamentId}&address=${serverAddress}&port=${serverPort}`
    );
  };

  const createQualifier = async () => {
    await $taService.createQualifier(
      serverAddress,
      serverPort,
      tournamentId,
      qualifier
    );

    // Bounce back out to selection so that when it's clicked again, we have the right query params
    // TODO: can probably do history rewriting instead of this
    returnToQualifierSelection();
  };

  const updateQualifier = async () => {
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

  const deleteQualifier = async () => {
    // We only want realtime updates on qualifiers that already exist, so if there's
    // no qualifierId in the path, we'll hold off on this
    if (qualifierId) {
      await $taService.deleteQualifier(
        serverAddress,
        serverPort,
        tournamentId,
        qualifier
      );
    }
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

    await updateQualifier();
  };

  const onGetScoresClicked = async () => {
    const workbook = new Workbook();

    for (let map of qualifier.qualifierMaps) {
      //let sanitizationRegex = new RegExp("[\[/\?'\]\*:]");
      // TODO: Revisit this with regex. Regex was being dumb
      const sanitizedWorksheetName = map.gameplayParameters?.beatmap?.name
        .replace("[", "")
        .replace("]", "")
        .replace("?", "")
        .replace(":", "")
        .replace("*", "")
        .replace("/", "")
        .replace("\\", "");
      const worksheet = workbook.addWorksheet(sanitizedWorksheetName);

      const scoresResponse = await $taService.getLeaderboard(
        serverAddress,
        serverPort,
        qualifier.guid,
        map.guid
      );

      if (
        scoresResponse.type === Response_ResponseType.Success &&
        scoresResponse.details.oneofKind === "leaderboardScores"
      ) {
        for (let score of scoresResponse.details.leaderboardScores.scores) {
          worksheet.addRow([
            score.score,
            score.username,
            score.fullCombo ? "FC" : "",
          ]);
        }
      }
    }

    const buffer = await workbook.xlsx.writeBuffer();

    saveAs(new Blob([buffer]), "Leaderboards.xlsx");
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
        on:input={updateQualifier}
        variant="outlined"
        label="Qualifier Name"
        disabled={editDisabled}
      />
    </Cell>
    {#if qualifier.infoChannel}
      <Cell span={4}>
        <Textfield
          bind:value={qualifier.infoChannel.id}
          on:input={updateQualifier}
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

            updateQualifier();
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

            updateQualifier();
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

            updateQualifier();
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

            updateQualifier();
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
    <Cell span={4}>
      <Button on:click={deleteQualifier}>End Qualifier</Button>
    </Cell>
    <Cell span={4}>
      <Button on:click={onGetScoresClicked}>Get Qualifier Scores</Button>
    </Cell>
  </LayoutGrid>

  {#if !qualifierId}
    <div class="create-qualifier-button-container" transition:slide>
      <Fab color="primary" on:click={createQualifier} extended>
        <Icon class="material-icons">add</Icon>
        <Label>Create Qualifier</Label>
      </Fab>
    </div>
  {/if}
</div>

<style lang="scss">
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
