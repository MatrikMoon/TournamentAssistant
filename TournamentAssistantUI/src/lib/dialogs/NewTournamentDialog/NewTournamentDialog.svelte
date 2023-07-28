<script lang="ts">
  import { fly } from "svelte/transition";
  import EditTournamentDialog from "./EditTournamentDialog.svelte";
  import AddTeamsDialog from "./AddTeamsDialog.svelte";
  import type { CoreServer, Tournament } from "tournament-assistant-client";
  import { authToken, client, masterServerAddress } from "$lib/stores";
  import ConnectingToNewServerDialog from "../ConnectingToNewServerDialog.svelte";

  export let open = false;
  export let onTournamentCreated = () => {};

  let tournament: Tournament;
  let host: CoreServer;

  let addTeamsDialogOpen = false;
  let connectingToNewServerDialogOpen = false;
  let acceptedNewServerWarning = false;

  const onAddTeams = () => {
    addTeamsDialogOpen = true;
  };

  const onTournamentCreate = () => {
    if (!$client.isConnected) {
      if (!acceptedNewServerWarning && host.address === $masterServerAddress) {
        connectingToNewServerDialogOpen = true;
      } else {
        //If the master server client already has a token, it's probably (TODO: !!) valid for any server
        $client.setAuthToken($authToken);
        $client.connect(host.address, `${host.websocketPort}`);
      }
    }

    $client.once("connectedToServer", () => {
      $client.createTournament(tournament);
    });

    $client.once("createdTournament", () => {
      onTournamentCreated();
      $client.disconnect();
    });

    $client.once("failedToCreateTournament", () => {
      $client.disconnect();
    });
  };
</script>

<div class="dialog-container">
  {#if !addTeamsDialogOpen}
    <div in:fly={{ x: -200, duration: 800 }}>
      <EditTournamentDialog
        bind:open
        bind:tournament
        bind:host
        onCreateClick={onTournamentCreate}
        onAddTeamsClick={onAddTeams}
      />
    </div>
  {:else}
    <div in:fly={{ x: 200, duration: 800 }}>
      <AddTeamsDialog
        bind:open={addTeamsDialogOpen}
        bind:tournament
        onCreateClick={onTournamentCreate}
      />
    </div>
  {/if}
  {#if connectingToNewServerDialogOpen}
    <div in:fly={{ duration: 800 }}>
      <ConnectingToNewServerDialog
        bind:open={connectingToNewServerDialogOpen}
        onContinueClick={() => {
          acceptedNewServerWarning = true;

          //If the dialog popped up, we can assume they already tried to create the tournament.
          //Let's just do it again for them now that we've set the flag
          onTournamentCreate();
        }}
      />
    </div>
  {/if}
</div>

<style lang="scss">
  .dialog-container {
    position: fixed;
    top: 0;
    bottom: 0;
    right: 0;
    left: 0;
    display: flex;
    justify-content: center;
    align-items: center;
    z-index: 1;

    /* allow click-through to backdrop */
    pointer-events: none;
  }
</style>
