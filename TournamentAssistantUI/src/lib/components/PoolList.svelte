<script lang="ts">
  import List, { Item, Text, PrimaryText, Meta } from "@smui/list";
  import {
    masterAddress,
    masterApiPort,
    type Tournament_TournamentSettings_Pool,
  } from "tournament-assistant-client";
  import defaultLogo from "../assets/icon.png";
  import { taService } from "$lib/stores";
  import { onDestroy } from "svelte";

  export let tournamentId: string;
  export let showRemoveButton = false;
  export let onPoolClicked: (
    pool: Tournament_TournamentSettings_Pool
  ) => Promise<void>;
  export let onRemoveClicked: (
    pool: Tournament_TournamentSettings_Pool
  ) => Promise<void> = async (p) => {};

  // TAService now includes a getTournament wrapper, but I'm leaving this here for now since it's
  // extremely unlikely that we're still not connected to the server by the time we're showing this list
  let localPoolsInstance =
    $taService.client.stateManager.getTournament(tournamentId)?.settings
      ?.pools ?? [];

  function onChange() {
    localPoolsInstance =
      $taService.client.stateManager.getTournament(tournamentId)!.settings!
        .pools;
  }

  // When changes happen, re-render
  $taService.client.on("joinedTournament", onChange);
  $taService.subscribeToTournamentUpdates(onChange);
  onDestroy(() => {
    $taService.client.removeListener("joinedTournament", onChange);
    $taService.unsubscribeFromTournamentUpdates(onChange);
  });

  $: pools = localPoolsInstance ?? [];
</script>

<List twoLine avatarList>
  {#each pools as pool}
    <Item
      on:SMUI:action={async () => await onPoolClicked(pool)}
      selected={false}
    >
      <img
        alt=""
        class={"pool-image"}
        src={pool.image?.length > 0
          ? `https://${masterAddress}:${masterApiPort}/api/file/${pool.image}`
          : defaultLogo}
      />
      <Text>
        <PrimaryText>{pool.name}</PrimaryText>
        <!-- <SecondaryText>test</SecondaryText> -->
      </Text>
      {#if showRemoveButton}
        <Meta
          class="material-icons"
          on:click$stopPropagation={() => onRemoveClicked(pool)}
        >
          close
        </Meta>
      {/if}
    </Item>
  {/each}
</List>

<style lang="scss">
  .pool-image {
    width: 55px;
    height: 55px;
    border-radius: 50%;
    margin: 1vmin;
    object-fit: cover;
  }
</style>
