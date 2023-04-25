<script lang="ts">
    import { client } from "../stores";
    import { onDestroy } from "svelte";
    import Select, { Option } from "@smui/select";
    import LayoutGrid, { Cell } from "@smui/layout-grid";
    import DebugLog from "./DebugLog.svelte";
    import { getAllLevels, type Song } from "$lib/ostHelper";
    import Autocomplete from "@smui-extra/autocomplete";

    export let tournamentId: string;
    export let matchId: string;

    let selectedSongId = "";

    let localMatchInstance = $client.stateManager.getMatch(
        tournamentId,
        matchId
    );

    function onChange() {
        localMatchInstance = $client.stateManager.getMatch(
            tournamentId,
            matchId
        );
    }

    //When changes happen, re-render
    $client.on("joinedTournament", onChange);
    $client.stateManager.on("userConnected", onChange);
    $client.stateManager.on("userUpdated", onChange);
    $client.stateManager.on("userDisconnected", onChange);
    $client.stateManager.on("matchCreated", onChange);
    $client.stateManager.on("matchUpdated", onChange);
    $client.stateManager.on("matchDeleted", onChange);
    onDestroy(() => {
        $client.removeListener("joinedTournament", onChange);
        $client.stateManager.removeListener("userConnected", onChange);
        $client.stateManager.removeListener("userUpdated", onChange);
        $client.stateManager.removeListener("userDisconnected", onChange);
        $client.stateManager.removeListener("matchCreated", onChange);
        $client.stateManager.removeListener("matchUpdated", onChange);
        $client.stateManager.removeListener("matchDeleted", onChange);
    });

    function getOptionLabel(option: Song) {
        if (option) {
            return option.levelName;
        }
        return "";
    }
</script>

<LayoutGrid>
    <Cell span={8}>
        <Autocomplete
            bind:value={selectedSongId}
            options={getAllLevels()}
            {getOptionLabel}
            label="Song ID"
            textfield$variant="outlined"
        />
    </Cell>
    <Cell spam={4} />
    <Cell span={12}>
        <div class="grid-cell">
            <DebugLog />
        </div>
    </Cell>
</LayoutGrid>

<style lang="scss">
    .grid-cell {
        background-color: rgba($color: #000000, $alpha: 0.1);
    }
</style>
