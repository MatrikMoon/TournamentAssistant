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
  import { taService } from "$lib/stores";
  import { onDestroy, onMount } from "svelte";

  export let onTournamentSelected = async (
    id: string,
    address: string,
    port: string
  ) => {};

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
          {#if item.settings?.isBkTournament}
            <span class="bk-badge">BeatKhana</span>
          {/if}
        </PrimaryText>
        <SecondaryText>
          {`${item.server?.address}:${item.server?.websocketPort}`}
        </SecondaryText>
      </Text>
      {#if item.settings?.isBkTournament && item.settings?.beatKhanaTournamentGuid}
        <a
          class="external-button material-icons"
          href={`https://beatkhana.com/tournaments/${encodeURIComponent(item.settings.beatKhanaTournamentGuid)}/general`}
          target="_blank"
          rel="noopener noreferrer"
          aria-label={`View ${item.settings?.tournamentName || "tournament"} on BeatKhana`}
          title="View external"
          on:click|stopPropagation
        >
          open_in_new
        </a>
      {/if}
      {#if item.settings?.myPermissions.includes("tournament:settings:delete")}
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

  .bk-badge {
    display: inline-block;
    margin-left: 0.5rem;
    padding: 0.1rem 0.4rem;
    border-radius: 999px;
    background: rgba(103, 58, 183, 0.18);
    color: #b39ddb;
    font-size: 0.68rem;
    font-weight: 700;
    line-height: 1.5;
    text-transform: uppercase;
    vertical-align: middle;
  }

  .external-button {
    display: inline-grid;
    place-items: center;
    width: 2.25rem;
    height: 2.25rem;
    border-radius: 50%;
    color: var(--mdc-theme-text-secondary-on-background);
    text-decoration: none;
  }

  .external-button:hover,
  .external-button:focus-visible {
    background: rgba(103, 58, 183, 0.16);
    color: var(--mdc-theme-primary);
    outline: none;
  }
</style>
