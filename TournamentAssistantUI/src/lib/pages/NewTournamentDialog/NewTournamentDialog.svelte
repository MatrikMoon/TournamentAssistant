<script lang="ts">
    import { client } from "$lib/stores";
    import DataTable, {
        Head,
        Body,
        Row,
        Cell as DataTableCell,
    } from "@smui/data-table";
    import { fly } from "svelte/transition";
    import EditTournamentDialog from "./EditTournamentDialog.svelte";
    import AddTeamsDialog from "./AddTeamsDialog.svelte";
    import type { Tournament } from "tournament-assistant-client";

    export let open = false;

    let tournament: Tournament;

    let addTeamsDialogOpen = false;
</script>

<div class="dialog-container">
    {#if !addTeamsDialogOpen}
        <div transition:fly={{ x: -200, duration: 800 }}>
            <EditTournamentDialog
                bind:open
                bind:shouldCreateTeams={addTeamsDialogOpen}
                bind:tournament
            />
        </div>
    {:else}
        <div transition:fly={{ x: 200, duration: 800 }}>
            <AddTeamsDialog bind:open={addTeamsDialogOpen} bind:tournament />
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
