<script lang="ts">
  import { goto } from "$app/navigation";
  import { fly, slide } from "svelte/transition";
  import Fab, { Icon, Label } from "@smui/fab";
  import TournamentList from "$lib/components/TournamentList.svelte";
  import TaDrawer from "$lib/components/TADrawer.svelte";
  import NewTournamentDialog from "$lib/dialogs/NewTournamentDialog/NewTournamentDialog.svelte";
  import ConnectingToNewServerDialog from "$lib/dialogs/ConnectingToNewServerDialog.svelte";
  import { masterAddress } from "tournament-assistant-client";

  let creationDialogOpen = false;
  let connectingToNewServerDialogOpen = false;
  let acceptedNewServerWarning = false;

  let lastTriedId: string;
  let lastTriedAddress: string;
  let lastTriedPort: string;

  const onTournamentSelected = (id: string, address: string, port: string) => {
    lastTriedId = id;
    lastTriedAddress = address;
    lastTriedPort = port;

    if (!acceptedNewServerWarning && address !== masterAddress) {
      connectingToNewServerDialogOpen = true;
    } else {
      goto(`/tournament?tournamentId=${id}&address=${address}&port=${port}`);
    }
  };
</script>

<TaDrawer items={[]}>
  <NewTournamentDialog bind:open={creationDialogOpen} />

  <div in:fly={{ duration: 800 }}>
    <ConnectingToNewServerDialog
      bind:open={connectingToNewServerDialogOpen}
      onContinueClick={() => {
        acceptedNewServerWarning = true;

        //If the dialog popped up, we can assume they already tried to join the tournament.
        //Let's just do it again for them now that we've set the flag
        onTournamentSelected(lastTriedId, lastTriedAddress, lastTriedPort);
      }}
    />
  </div>

  <div class="list-title">Pick a tournament</div>

  <div class="tournament-list">
    <TournamentList {onTournamentSelected} />
  </div>
</TaDrawer>

<div class="create-tournament-button-container" transition:slide>
  <Fab
    color="primary"
    on:click={() => {
      creationDialogOpen = true;
    }}
    extended
  >
    <Icon class="material-icons">add</Icon>
    <Label>Create Tournament</Label>
  </Fab>
</div>

<style lang="scss">
  .list-title {
    color: var(--mdc-theme-text-primary-on-background);
    background-color: rgba($color: #000000, $alpha: 0.1);
    border-radius: 2vmin;
    text-align: center;
    font-size: 2rem;
    font-weight: 100;
    line-height: 1.1;
    padding: 2vmin;
  }

  .tournament-list {
    background-color: rgba($color: #000000, $alpha: 0.1);
    border-radius: 2vmin;
    width: fit-content;
    text-align: -webkit-center;
    overflow-y: auto;
    max-height: 70vh;
    margin: 2vmin auto;
  }

  .create-tournament-button-container {
    position: fixed;
    bottom: 2vmin;
    right: 2vmin;
  }
</style>
