<script lang="ts">
    import { fly } from "svelte/transition";
    import EditTournamentDialog from "./EditTournamentDialog.svelte";
    import AddTeamsDialog from "./AddTeamsDialog.svelte";
    import {
        CoreServer,
        User_ClientTypes,
        type Tournament,
    } from "tournament-assistant-client";
    import { client } from "$lib/stores";

    export let open = false;

    let tournament: Tournament;
    let host: CoreServer;

    let addTeamsDialogOpen = false;

    const onTournamentCreate = () => {
        console.log("[CONNECT] Creating tournament");

        $client.connect(
            host.address,
            `${host.websocketPort}`,
            "TAUI",
            User_ClientTypes.WebsocketConnection
        );

        $client.once("connectedToServer", () => {
            console.log("[CREATE] Creating tournament");
            $client.createTournament(tournament);

            console.log("[DISCONNECT] Creating tournament");
            $client.disconnect();
        });
    };

    const onAddTeams = () => {
        addTeamsDialogOpen = true;
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
