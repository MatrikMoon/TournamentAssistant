<script lang="ts">
  import { goto } from "$app/navigation";
  import { page } from "$app/stores";
  import LayoutGrid, { Cell } from "@smui/layout-grid";
  import UserList from "$lib/components/UserList.svelte";
  import MatchList from "$lib/components/MatchList.svelte";
  import DebugLog from "$lib/components/DebugLog.svelte";
  import { client } from "$lib/stores";
  import { onDestroy } from "svelte";
  import Button, { Label } from "@smui/button";
  import type { Match, Tournament, User } from "tournament-assistant-client";
  import { v4 as uuidv4 } from "uuid";

  let tournamentId = $page.url.searchParams.get("tournamentId")!;
  let serverAddress = $page.url.searchParams.get("address")!;
  let serverPort = $page.url.searchParams.get("port")!;

  let tournament = $client.stateManager.getTournament(tournamentId);

  let selectedPlayers: User[] = [];

  if (!$client.isConnected) {
    //Join the tournament on connect
    $client.once("connectedToServer", () => {
      $client.joinTournament(tournamentId);
    });

    $client.connect(serverAddress, serverPort);
  } else {
    //Check that we are in the correct tournament
    const self = $client.stateManager.getUser(
      tournamentId,
      $client.stateManager.getSelfGuid()
    );

    //We're connected, but haven't joined the tournament. Let's do that
    if (!self) {
      $client.joinTournament(tournamentId);
    }
  }

  function onTournamentJonied() {
    tournament = $client.stateManager.getTournament(tournamentId);
  }

  function onMatchCreated(matchCreatedParams: [Match, Tournament]) {
    if (matchCreatedParams[0].leader === $client.stateManager.getSelfGuid()) {
      goto(
        `/tournament/match?tournamentId=${tournamentId}&address=${serverAddress}&port=${serverPort}&matchId=${matchCreatedParams[0].guid}`
      );
    }
  }

  //If the client joins a tournament after load, refresh the tourney info
  $client.on("joinedTournament", onTournamentJonied);
  $client.stateManager.on("matchCreated", onMatchCreated);
  onDestroy(() => {
    $client.removeListener("joinedTournament", onTournamentJonied);
    $client.stateManager.removeListener("matchCreated", onMatchCreated);
  });

  function onCreateMatchClick() {
    console.log({ selectedPlayers });

    if (selectedPlayers?.length > 0) {
      $client.createMatch(tournamentId, {
        guid: uuidv4(), //Reassigned on server side
        leader: $client.stateManager.getSelfGuid(),
        associatedUsers: [
          $client.stateManager.getSelfGuid(),
          ...selectedPlayers.map((x) => x.guid),
        ],
        selectedLevel: undefined,
        selectedDifficulty: 0,
        selectedCharacteristic: undefined,
        startTime: new Date().toLocaleDateString(),
      });
    }
  }
</script>

<div>
  <!-- <div class="tournament-title">{tournament?.settings?.tournamentName}</div> -->
  <div class="tournament-title">Select your players and create a match</div>
  <LayoutGrid>
    <Cell span={4}>
      <div class="player-list-title">Players</div>
      <div class="grid-cell">
        <UserList {tournamentId} bind:selectedUsers={selectedPlayers} />
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
    <Cell span={12}>
      <div class="grid-cell">
        <DebugLog />
      </div>
    </Cell>
  </LayoutGrid>
</div>

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
