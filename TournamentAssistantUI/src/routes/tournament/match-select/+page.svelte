<script lang="ts">
  import { goto } from "$app/navigation";
  import { page } from "$app/stores";
  import LayoutGrid, { Cell } from "@smui/layout-grid";
  import UserList from "$lib/components/UserList.svelte";
  import MatchList from "$lib/components/MatchList.svelte";
  import DebugLog from "$lib/components/DebugLog.svelte";
  import { onDestroy, onMount } from "svelte";
  import Button, { Label } from "@smui/button";
  import type { Match, Tournament, User } from "tournament-assistant-client";
  import { v4 as uuidv4 } from "uuid";
  import { taService } from "$lib/stores";
  import { BeatSaverService } from "$lib/services/beatSaver/beatSaverService";

  let serverAddress = $page.url.searchParams.get("address")!;
  let serverPort = $page.url.searchParams.get("port")!;
  let tournamentId = $page.url.searchParams.get("tournamentId")!;

  let tournament: Tournament;

  let selectedPlayers: User[] = [];

  onMount(async () => {
    console.log("onMount joinTournament/getTournament");

    await $taService.joinTournament(serverAddress, serverPort, tournamentId);

    tournament = (await $taService.getTournament(
      serverAddress,
      serverPort,
      tournamentId
    ))!;
  });

  async function onTournamentJonied() {
    tournament = (await $taService.getTournament(
      serverAddress,
      serverPort,
      tournamentId
    ))!;
  }

  async function onMatchCreated(matchCreatedParams: [Match, Tournament]) {
    // If we create a match, go to the match page
    if (
      matchCreatedParams[0].leader ===
      $taService.client.stateManager.getSelfGuid()
    ) {
      goto(
        `/tournament/match?tournamentId=${tournamentId}&address=${serverAddress}&port=${serverPort}&matchId=${matchCreatedParams[0].guid}`
      );
    }
  }

  //If the client joins a tournament after load, refresh the tourney info
  $taService.client.on("joinedTournament", onTournamentJonied);
  $taService.subscribeToMatchUpdates(onMatchCreated);
  onDestroy(() => {
    $taService.client.removeListener("joinedTournament", onTournamentJonied);
    $taService.unsubscribeFromMatchUpdates(onMatchCreated);
  });

  async function onCreateMatchClick() {
    console.log({ selectedPlayers });

    if (selectedPlayers?.length > 0) {
      await $taService.createMatch(serverAddress, serverPort, tournamentId, {
        guid: uuidv4(), //Reassigned on server side
        leader: $taService.client.stateManager.getSelfGuid(),
        associatedUsers: [
          $taService.client.stateManager.getSelfGuid(),
          ...selectedPlayers.map((x) => x.guid),
        ],
        selectedLevel: undefined,
        selectedDifficulty: 0,
        selectedCharacteristic: undefined,
        startTime: new Date().toLocaleDateString(),
      });
    }
  }

  async function onCreateQuailfierClick() {
    await $taService.createQualifier(serverAddress, serverPort, tournamentId, {
      guid: uuidv4(), //Reassigned on server side
      name: "Test Qualifier",
      guild: {
        id: BigInt(0),
        name: "dummy",
      },
      infoChannel: {
        id: BigInt(0),
        name: "dummy",
      },
      qualifierMaps: [
        {
          guid: uuidv4(), //Reassigned on server side
          gameplayParameters: {
            beatmap: {
              name: "Big Girl (You Are Beautiful) - MIKA",
              levelId: "custom_level_6C4F86A126CD7465EC536837F3E73874E07068EF",
              characteristic: {
                serializedName: "Standard",
                difficulties: [],
              },
              difficulty: 3,
            },
            playerSettings: {
              playerHeight: 0,
              sfxVolume: 0,
              saberTrailIntensity: 0,
              noteJumpStartBeatOffset: 0,
              noteJumpFixedDuration: 0,
              options: 0,
              noteJumpDurationTypeSettings: 0,
              arcVisibilityType: 0,
            },
            gameplayModifiers: {
              options: 0,
            },
          },
          disablePause: false,
          attempts: 3,
        },
      ],
      sendScoresToInfoChannel: false,
      flags: 0,
    });
  }
</script>

<div class="tournament-title">Select your players and create a match</div>
<LayoutGrid>
  <Cell span={4}>
    <div class="player-list-title">Players</div>
    <div class="grid-cell">
      <UserList
        {serverAddress}
        {serverPort}
        {tournamentId}
        bind:selectedUsers={selectedPlayers}
      />
      <div class="button">
        <Button variant="raised" on:click={onCreateMatchClick}>
          <Label>Create Match</Label>
        </Button>
      </div>
    </div>
  </Cell>
  <Cell span={4}>
    <div class="match-list-title">Active Matches</div>
    <div class="grid-cell">
      <MatchList {tournamentId} />
    </div>
  </Cell>
  <Cell>
    <Button
      variant="raised"
      on:click={() => BeatSaverService.getSongInfo("E44")}
    >
      <Label>Create Test Qualifier</Label>
    </Button>
  </Cell>
  <Cell span={12}>
    <div class="grid-cell">
      <DebugLog />
    </div>
  </Cell>
</LayoutGrid>

<style lang="scss">
  .grid-cell {
    background-color: rgba($color: #000000, $alpha: 0.1);

    .button {
      text-align: center;
      padding-bottom: 2vmin;
    }
  }

  .tournament-title {
    color: var(--mdc-theme-text-primary-on-background);
    background-color: rgba($color: #000000, $alpha: 0.1);
    border-radius: 2vmin;
    text-align: center;
    font-size: 2rem;
    font-weight: 100;
    line-height: 1.1;
    padding: 2vmin;
  }

  .player-list-title,
  .match-list-title {
    color: var(--mdc-theme-text-primary-on-background);
    background-color: rgba($color: #000000, $alpha: 0.1);
    border-radius: 2vmin 2vmin 0 0;
    text-align: center;
    font-size: 2rem;
    font-weight: 100;
    line-height: 1.1;
    padding: 2vmin;
  }
</style>
