<script lang="ts">
  import { goto } from "$app/navigation";
  import Button, { Label } from "@smui/button";
  import TournamentList from "$lib/pages/TournamentList.svelte";
  import NewTournamentDialog from "$lib/pages/NewTournamentDialog/NewTournamentDialog.svelte";

  let creationDialogOpen = false;

  //We'll use this below to refresh the tournament list after a tournament is created
  let refreshTournaments: () => void;

  console.log(window.location);
</script>

<NewTournamentDialog
  bind:open={creationDialogOpen}
  onTournamentCreated={refreshTournaments}
/>

<div>
  <div class="list-title">Pick a tournament</div>
  <div class="tournament-list">
    <TournamentList
      onTournamentSelected={(id, address, port) =>
        goto(`/tournament?tournamentId=${id}&address=${address}&port=${port}`)}
      bind:refreshTournaments
    />
  </div>
  <div class="create-tournament-button">
    <Button variant="raised" on:click={() => (creationDialogOpen = true)}>
      <Label>Create tournament</Label>
    </Button>
  </div>
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
