<script lang="ts">
  import List, {
    Item,
    Text,
    PrimaryText,
    SecondaryText,
    Meta,
  } from "@smui/list";
  import type { Tournament_TournamentSettings_Pool } from "tournament-assistant-client";
  import defaultLogo from "../assets/icon.png";
  import { taService } from "$lib/stores";
  import { onDestroy } from "svelte";

  export let tournamentId: string;
  export let showRemoveButton = false;
  export let onPoolClicked: (
    pool: Tournament_TournamentSettings_Pool,
  ) => Promise<void>;
  export let onRemoveClicked: (
    pool: Tournament_TournamentSettings_Pool,
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
    $taService.subscribeToTournamentUpdates(onChange);
  });

  $: pools =
    localPoolsInstance.map((x) => {
      let byteArray = x.image;

      // Only make the blob url if there is actually image data
      if ((byteArray?.length ?? 0) > 1) {
        // Sometimes it's not parsed as a Uint8Array for some reason? So we'll shunt it back into one
        if (!(x.image instanceof Uint8Array)) {
          byteArray = new Uint8Array(Object.values(x.image!));
        }

        var blob = new Blob([byteArray!], {
          type: "image/jpeg",
        });

        var urlCreator = window.URL || window.webkitURL;
        var imageUrl = urlCreator.createObjectURL(blob);

        return {
          ...x,
          imageUrl,
        };
      }

      // Set the image to undefined if we couldn't make a blob of it
      return { ...x, imageUrl: undefined };
    }) ?? [];
</script>

<List twoLine avatarList>
  {#each pools as pool}
    <Item
      on:SMUI:action={async () => await onPoolClicked(pool)}
      selected={false}
    >
      <img alt="" class={"pool-image"} src={pool.imageUrl ?? defaultLogo} />
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
