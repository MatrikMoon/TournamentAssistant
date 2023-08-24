<script lang="ts">
  import { goto } from "$app/navigation";
  import { page } from "$app/stores";
  import LayoutGrid, { Cell } from "@smui/layout-grid";
  import UserList from "$lib/components/UserList.svelte";
  import MatchList from "$lib/components/MatchList.svelte";
  import DebugLog from "$lib/components/DebugLog.svelte";
  import { onDestroy, onMount } from "svelte";
  import Button, { Label } from "@smui/button";
  import type {
    Match,
    QualifierEvent,
    Tournament,
    User,
  } from "tournament-assistant-client";
  import { v4 as uuidv4 } from "uuid";
  import { taService } from "$lib/stores";
  import { BeatSaverService } from "$lib/services/beatSaver/beatSaverService";

  let serverAddress = $page.url.searchParams.get("address")!;
  let serverPort = $page.url.searchParams.get("port")!;
  let tournamentId = $page.url.searchParams.get("tournamentId")!;

  let tournament: Tournament;

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

  async function onQualifierCreated(
    qualifierCreatedParams: [QualifierEvent, Tournament]
  ) {
    // If we create a qualifier, go to the qualifier page
    // TODO: do id checking like when we create matches
    goto(
      `/tournament/qualifier?tournamentId=${tournamentId}&address=${serverAddress}&port=${serverPort}&qualifierId=${qualifierCreatedParams[0].guid}`
    );
  }

  //If the client joins a tournament after load, refresh the tourney info
  $taService.client.on("joinedTournament", onTournamentJonied);
  $taService.subscribeToQualifierUpdates(onQualifierCreated);
  onDestroy(() => {
    $taService.client.removeListener("joinedTournament", onTournamentJonied);
    $taService.unsubscribeFromQualifierUpdates(onQualifierCreated);
  });

  async function onCreateQualifierClick() {
    // await $taService.createQualifier(serverAddress, serverPort, tournamentId, {
    //   guid: uuidv4(), //Reassigned on server side
    //   leader: $taService.client.stateManager.getSelfGuid(),
    //   associatedUsers: [
    //     $taService.client.stateManager.getSelfGuid(),
    //     ...selectedPlayers.map((x) => x.guid),
    //   ],
    //   selectedLevel: undefined,
    //   selectedDifficulty: 0,
    //   selectedCharacteristic: undefined,
    //   startTime: new Date().toLocaleDateString(),
    // });
  }
</script>

<div class="qualifier-title">Build your qualifier</div>
<LayoutGrid>
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
</style>
