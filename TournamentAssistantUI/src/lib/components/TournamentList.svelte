<script lang="ts">
  import List, {
    Item,
    Text,
    PrimaryText,
    SecondaryText,
    Meta,
  } from "@smui/list";
  import defaultLogo from "../assets/icon.png";
  import {
    masterAddress,
    masterApiPort,
    type Tournament,
  } from "tournament-assistant-client";
  import { authToken, taService } from "$lib/stores";
  import { onDestroy, onMount } from "svelte";
  import { getUserIdFromToken } from "$lib/services/jwtService";

  export let onTournamentSelected = async (
    id: string,
    address: string,
    port: string
  ) => {};

  let showRemoveButton =
    getUserIdFromToken($authToken) === "229408465787944970";

  let tournaments: Tournament[] = [];

  onMount(async () => {
    console.log("onMount getTournaments");
    await onChange();
  });

  async function onChange() {
    tournaments = await $taService.getTournaments();
  }

  // When changes happen to the user list, re-render
  $taService.subscribeToMasterTournamentUpdates(onChange);
  onDestroy(() => {
    $taService.unsubscribeFromMasterTournamentUpdates(onChange);
  });
</script>

<List twoLine avatarList singleSelection>
  {#each tournaments as item}
    <Item
      on:SMUI:action={async () => {
        const address = item.server?.address;
        const port = `${item.server?.websocketPort}`;

        console.log(
          `selected: ${item.settings?.tournamentName} ${address}:${port}`
        );

        if (!address || !port) {
          return;
        }

        await onTournamentSelected(item.guid, address, port);
      }}
    >
      <img
        alt=""
        class={"tournament-image"}
        src={item.settings?.tournamentImage
          ? `https://${masterAddress}:${masterApiPort}/api/file/${item.settings?.tournamentImage}`
          : defaultLogo}
      />

      <Text>
        <PrimaryText>
          {item.settings?.tournamentName}
        </PrimaryText>
        <SecondaryText>
          {`${item.server?.address}:${item.server?.websocketPort}`}
        </SecondaryText>
      </Text>
      {#if showRemoveButton}
        <Meta
          class="material-icons"
          on:click$stopPropagation={() => {
            const address = item.server?.address;
            const port = `${item.server?.websocketPort}`;

            if (!address || !port || !item.guid) {
              return;
            }

            $taService.deleteTournament(address, port, item.guid);
          }}
        >
          close
        </Meta>
      {/if}
    </Item>
  {/each}
</List>

<style lang="scss">
  .tournament-image {
    width: 55px;
    height: 55px;
    border-radius: 50%;
    margin: 1vmin;
    object-fit: cover;
  }
</style>
