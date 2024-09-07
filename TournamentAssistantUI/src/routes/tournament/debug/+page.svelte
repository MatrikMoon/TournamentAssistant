<script lang="ts">
  import { page } from "$app/stores";
  import type { User } from "tournament-assistant-client";
  import UserList from "$lib/components/UserList.svelte";

  let serverAddress = $page.url.searchParams.get("address")!;
  let serverPort = $page.url.searchParams.get("port")!;
  let tournamentId = $page.url.searchParams.get("tournamentId")!;

  let selectedPlayers: User[] = [];
</script>

<div class="player-list-title">Players</div>
<div class="shaded-box">
  <UserList
    {serverAddress}
    {serverPort}
    {tournamentId}
    userFilter={(users) => users}
    bind:selectedUsers={selectedPlayers}
  />
</div>

<style lang="scss">
  .shaded-box {
    background-color: rgba($color: #000000, $alpha: 0.1);

    .button {
      text-align: center;
      padding-bottom: 2vmin;
    }
  }

  .player-list-title {
    color: var(--mdc-theme-text-primary-on-background);
    background-color: rgba($color: #000000, $alpha: 0.1);
    border-radius: 2vmin 2vmin 0 0;
    text-align: center;
    font-size: 2rem;
    font-weight: 100;
    line-height: 1.1;
    padding: 2vmin;
  }
</style>
