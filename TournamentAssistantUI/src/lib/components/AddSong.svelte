<script lang="ts">
  import { onDestroy } from "svelte";
  import { Cell, InnerGrid } from "@smui/layout-grid";
  import DebugLog from "./DebugLog.svelte";
  import { getAllLevels, type Song } from "$lib/services/ostService";
  import Autocomplete from "@smui-extra/autocomplete";
  import { taService } from "$lib/stores";
  import type { Match, QualifierEvent } from "tournament-assistant-client";
  import { onMount } from "svelte";

  export let serverAddress: string;
  export let serverPort: string;
  export let tournamentId: string;
  export let matchId: string | undefined = undefined;
  export let qualifierId: string | undefined = undefined;

  export let selectedSongId = "";

  let expanded = false;

  let localMatchInstance: Match;
  let localQualifierInstance: QualifierEvent;

  onMount(async () => {
    console.log("onMount getMatch/getQualifier");
    await onChange();
  });

  async function onChange() {
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
  $taService.client.on("joinedTournament", onChange);
  $taService.subscribeToUserUpdates(onChange);
  $taService.subscribeToMatchUpdates(onChange);
  $taService.subscribeToQualifierUpdates(onChange);
  onDestroy(() => {
    $taService.client.removeListener("joinedTournament", onChange);
    $taService.unsubscribeFromUserUpdates(onChange);
    $taService.unsubscribeFromMatchUpdates(onChange);
    $taService.unsubscribeFromQualifierUpdates(onChange);
  });

  function getOptionLabel(option: Song) {
    if (option) {
      return option.levelName;
    }
    return "";
  }
</script>

<InnerGrid>
  <Cell span={8}>
    <Autocomplete
      bind:value={selectedSongId}
      options={getAllLevels()}
      {getOptionLabel}
      label="Song ID"
      combobox
      textfield$variant="outlined"
    />
  </Cell>
  <Cell span={4} />
  <Cell span={12}>
    <div class="grid-cell">
      <DebugLog />
    </div>
  </Cell>
</InnerGrid>

<style lang="scss">
  .grid-cell {
    background-color: rgba($color: #000000, $alpha: 0.1);
  }
</style>
