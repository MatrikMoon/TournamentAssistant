<script lang="ts">
  import { goto } from "$app/navigation";
  import Button, { Label } from "@smui/button";
  import TournamentSelection from "$lib/pages/TournamentSelection.svelte";
  import NewTournamentDialog from "$lib/pages/NewTournamentDialog/NewTournamentDialog.svelte";

  let creationDialogOpen = false;

  //We'll use this below to refresh the tournament list after a tournament is created
  let refreshTournaments: () => void;
</script>

<NewTournamentDialog
  bind:open={creationDialogOpen}
  onTournamentCreated={refreshTournaments}
/>

<div>
  <div class="tournament-list">
    <TournamentSelection
      onTournamentSelected={(id) => goto(`/tournament?id=${id}`)}
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
  .tournament-list {
    background-color: rgba($color: #000000, $alpha: 0.1);
    border-radius: 2vmin;
    width: fit-content;
    text-align: -webkit-center;
    margin: 0 auto;
    overflow-y: auto;
    max-height: 80vmin;
  }

  .create-tournament-button {
    background-color: rgba($color: #000000, $alpha: 0.1);
    border-radius: 2vmin;
    width: fit-content;
    text-align: -webkit-center;
    margin: 2vmin auto;
  }
</style>
