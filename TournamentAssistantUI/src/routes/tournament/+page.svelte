<script>
  import { page } from "$app/stores";
  import LayoutGrid, { Cell } from "@smui/layout-grid";
  import UserList from "$lib/components/UserList.svelte";
  import DebugLog from "$lib/components/DebugLog.svelte";
  import { client } from "$lib/stores";
  import { onDestroy } from "svelte";

  let tournamentId = $page.url.searchParams.get("id");
  let serverAddress = $page.url.searchParams.get("address");
  let serverPort = $page.url.searchParams.get("port");

  let tournament = $client.stateManager.getTournament(tournamentId);

  if (!$client.isConnected) {
    //Join the tournament on connect
    $client.once("connectedToServer", () => {
      $client.joinTournament(tournamentId);
    });

    $client.connect(serverAddress, serverPort);
  } else {
    //Check that we are in the correct tournament
    const self = $client.stateManager.getUser(
      tournamentId,
      $client.stateManager.getSelfGuid()
    );

    //We're connected, but haven't joined the server. Let's do that
    if (!self) {
      $client.joinTournament(tournamentId);
    }
  }

  function onChange() {
    tournament = $client.stateManager.getTournament(tournamentId);
  }

  //If the client joins a tournament after load, refresh the tourney info
  $client.on("joinedTournament", onChange);
  onDestroy(() => {
    $client.removeListener("joinedTournament", onChange);
  });

  console.log(window.location);
  $: console.log({ tournament });
</script>

<div>
  <!-- <div class="tournament-title">{tournament?.settings?.tournamentName}</div> -->
  <div class="tournament-title">Select your players and start a match</div>
  <LayoutGrid>
    <Cell span={4}>
      <div class="grid-cell">
        <UserList {tournamentId} />
      </div>
    </Cell>
    <Cell span={12}>
      <div class="grid-cell">
        <DebugLog />
      </div>
    </Cell>
  </LayoutGrid>
</div>

<style lang="scss">
  .grid-cell {
    background-color: rgba($color: #000000, $alpha: 0.1);
  }

  .tournament-title {
    color: var(--mdc-theme-text-primary-on-background);
    background-color: rgba($color: #000000, $alpha: 0.1);
    border-radius: 2vmin;
    text-align: center;
    font-size: 2rem;
    font-weight: 100;
    line-height: 1.1;
    padding: 2vmin;
  }
</style>
