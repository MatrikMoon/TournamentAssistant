<script lang="ts">
    import type { Tournament } from "tournament-assistant-client";
    import NameEdit from "./NameEdit.svelte";

    export let tournament: Tournament;
    export let onUpdated: () => void = () => {};
</script>

{#if tournament?.settings}
    <NameEdit
        hint="Tournament Name"
        bind:img={tournament.settings.tournamentImage}
        bind:name={tournament.settings.tournamentName}
        {onUpdated}
    />
{/if}
