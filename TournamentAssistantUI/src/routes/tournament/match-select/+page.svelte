<script lang="ts">
  import { page } from "$app/stores";
  import UserList from "$lib/components/UserList.svelte";
  import MatchList from "$lib/components/MatchList.svelte";
  import { onMount } from "svelte";
  import Button, { Icon, Label } from "@smui/button";
  import {
    Response_ResponseType,
    User_ClientTypes,
    User_PlayStates,
    type Tournament,
    type User,
  } from "tournament-assistant-client";
  import { v4 as uuidv4 } from "uuid";
  import { taService } from "$lib/stores";
  import { goto } from "$app/navigation";
  import Tooltip, { Wrapper } from "@smui/tooltip";

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
      tournamentId,
    ))!;
  });

  async function onCreateMatchWithAllClick() {
    const tournamentUsers = tournament!.users;

    const matches = await $taService.getMatches(
      serverAddress,
      serverPort,
      tournamentId,
    );

    const usersNotInMatches = tournamentUsers.filter(
      (x) =>
        x.clientType === User_ClientTypes.Player &&
        x.playState === User_PlayStates.WaitingForCoordinator &&
        !matches?.find((y) => y.associatedUsers.includes(x.guid)),
    );

    const result = await $taService.createMatch(
      serverAddress,
      serverPort,
      tournamentId,
      {
        guid: uuidv4(), // Reassigned on server side
        leader: $taService.client.stateManager.getSelfGuid(),
        associatedUsers: [
          $taService.client.stateManager.getSelfGuid(),
          ...usersNotInMatches.map((x) => x.guid),
        ],
        selectedMap: undefined,
      },
    );

    if (
      result.type === Response_ResponseType.Success &&
      result.details.oneofKind === "createMatch"
    ) {
      goto(
        `/tournament/match?tournamentId=${tournamentId}&address=${serverAddress}&port=${serverPort}&matchId=${result.details.createMatch.match!.guid}`,
      );
    }
  }

  async function onCreateMatchClick() {
    if (selectedPlayers?.length > 0) {
      const result = await $taService.createMatch(
        serverAddress,
        serverPort,
        tournamentId,
        {
          guid: uuidv4(), // Reassigned on server side
          leader: $taService.client.stateManager.getSelfGuid(),
          associatedUsers: [
            $taService.client.stateManager.getSelfGuid(),
            ...selectedPlayers.map((x) => x.guid),
          ],
          selectedMap: undefined,
        },
      );

      if (
        result.type === Response_ResponseType.Success &&
        result.details.oneofKind === "createMatch"
      ) {
        goto(
          `/tournament/match?tournamentId=${tournamentId}&address=${serverAddress}&port=${serverPort}&matchId=${result.details.createMatch.match!.guid}`,
        );
      }
    }
  }
</script>

<div class="page">
  <div class="tournament-title">Select your players and create a match</div>
  <div class="grid">
    <div class="column">
      <div class="cell">
        <div class="player-list-title">Players</div>
        <div class="shaded-box">
          <UserList
            {serverAddress}
            {serverPort}
            {tournamentId}
            bind:selectedUsers={selectedPlayers}
          />
          <div class="controls">
            <Button variant="raised" on:click={onCreateMatchClick}>
              <Label>Create Match</Label>
            </Button>
            <Wrapper>
              <Button variant="raised" on:click={onCreateMatchWithAllClick}>
                <Icon class="material-icons" style={"margin: 0"}>warning</Icon>
              </Button>
              <Tooltip>Add ALL users to a match</Tooltip>
            </Wrapper>
          </div>
        </div>
      </div>
    </div>
    <div class="column">
      <div class="cell">
        <div class="match-list-title">Active Matches</div>
        <div class="shaded-box">
          <MatchList {serverAddress} {serverPort} {tournamentId} />
        </div>
      </div>
    </div>
  </div>
</div>

<style lang="scss">
  .page {
    display: flex;
    flex-direction: column;
    align-items: center;
    margin-bottom: 70px;

    .tournament-title {
      color: var(--mdc-theme-text-primary-on-background);
      background-color: rgba($color: #000000, $alpha: 0.1);
      border-radius: 2vmin;
      text-align: center;
      font-size: 2rem;
      font-weight: 100;
      line-height: 1.1;
      padding: 2vmin;
      width: -webkit-fill-available;
    }

    .grid {
      display: flex;
      width: -webkit-fill-available;
      max-width: 700px;
      min-width: none;
      margin-top: 5px;

      .column {
        width: -webkit-fill-available;
        max-width: 350px;

        .cell {
          padding: 5px;
        }
      }
    }

    .shaded-box {
      background-color: rgba($color: #000000, $alpha: 0.1);

      .controls {
        text-align: center;
        padding-bottom: 2vmin;
      }
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
  }
</style>
