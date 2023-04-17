<script lang="ts">
    import LayoutGrid, { Cell } from "@smui/layout-grid";
    import UserList from "../components/UserList.svelte";
    import DebugLog from "../components/DebugLog.svelte";
    import { client } from "$lib/stores";
    import { onDestroy } from "svelte";

    export let tournamentId: string;
    export let serverAddress: string;
    export let serverPort: string;

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

    $: console.log(tournamentId);
</script>

<LayoutGrid>
    <Cell span={8}><div class="grid-cell">TODO</div></Cell>
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

<style lang="scss">
    .grid-cell {
        background-color: rgba($color: #000000, $alpha: 0.1);
    }
</style>
