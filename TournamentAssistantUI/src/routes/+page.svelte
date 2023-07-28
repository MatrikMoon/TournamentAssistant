<script lang="ts">
  import { goto } from "$app/navigation";
  import Button, { Label } from "@smui/button";
  import TournamentList from "$lib/components/TournamentList.svelte";
  import NewTournamentDialog from "$lib/dialogs/NewTournamentDialog/NewTournamentDialog.svelte";
  import { fly } from "svelte/transition";
  import ConnectingToNewServerDialog from "$lib/dialogs/ConnectingToNewServerDialog.svelte";
  import { masterServerAddress } from "$lib/stores";

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

    if (!acceptedNewServerWarning && address === $masterServerAddress) {
      connectingToNewServerDialogOpen = true;
    } else {
      goto(`/tournament?tournamentId=${id}&address=${address}&port=${port}`);
    }
  };
</script>

<NewTournamentDialog bind:open={creationDialogOpen} />

<div class="list-title">Pick a tournament</div>

<div class="tournament-list">
  <TournamentList {onTournamentSelected} />
</div>

<div class="create-tournament-button">
  <Button variant="raised" on:click={() => (creationDialogOpen = true)}>
    <Label>Create tournament</Label>
  </Button>
</div>

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
    margin: 0 auto;
    overflow-y: auto;
    max-height: 70vh;
    margin: 2vmin auto;
  }

  .create-tournament-button {
    background-color: rgba($color: #000000, $alpha: 0.1);
    border-radius: 2vmin;
    width: fit-content;
    text-align: -webkit-center;
    margin: 2vmin auto;
  }
</style>
