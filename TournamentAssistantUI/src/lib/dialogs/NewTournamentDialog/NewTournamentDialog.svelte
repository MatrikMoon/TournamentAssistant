<script lang="ts">
  import { fly } from "svelte/transition";
  import EditTournamentDialog from "./EditTournamentDialog.svelte";
  import {
    masterAddress,
    type CoreServer,
    type Tournament,
    Response_ResponseType,
  } from "tournament-assistant-client";
  import { taService } from "$lib/stores";
  import ConnectingToNewServerDialog from "../ConnectingToNewServerDialog.svelte";
  import { goto } from "$app/navigation";

  export let open = false;

  let tournament: Tournament;
  let host: CoreServer;

  let connectingToNewServerDialogOpen = false;
  let acceptedNewServerWarning = false;

  const onTournamentCreate = async () => {
    if (
      !acceptedNewServerWarning &&
      !$taService.client.isConnected &&
      host.address !== masterAddress
    ) {
      connectingToNewServerDialogOpen = true;
    } else {
      const response = await $taService.createTournament(
        host.address,
        `${host.websocketPort}`,
        tournament,
      );

      if (
        response.type === Response_ResponseType.Success &&
        response.details.oneofKind === "createTournament"
      ) {
        goto(
          `/tournament/edit?tournamentId=${response.details.createTournament.tournament!.guid}&address=${host.address}&port=${host.websocketPort}`,
        );
      }
    }
  };
</script>

<div class="dialog-container">
  <div in:fly={{ x: -200, duration: 800 }}>
    <EditTournamentDialog
      bind:open
      bind:tournament
      bind:host
      onCreateClick={onTournamentCreate}
    />
  </div>
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
