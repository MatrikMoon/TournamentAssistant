<script lang="ts">
    import { page } from "$app/stores";
    import LayoutGrid, { Cell } from "@smui/layout-grid";
    import UserList from "$lib/components/UserList.svelte";
    import DebugLog from "$lib/components/DebugLog.svelte";
    import { client } from "$lib/stores";
    import SongOptions from "$lib/components/SongOptions.svelte";

    let tournamentId = $page.url.searchParams.get("tournamentId")!;
    let serverAddress = $page.url.searchParams.get("address")!;
    let serverPort = $page.url.searchParams.get("port")!;
    let matchId = $page.url.searchParams.get("matchId")!;

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
</script>

<div>
    <!-- <div class="match-title">{tournament?.settings?.tournamentName}</div> -->
    <div class="match-title">Select a song, difficulty, and characteristic</div>
    <LayoutGrid>
        <Cell span={4}>
            <div class="player-list-title">Players</div>
            <div class="grid-cell">
                <UserList {tournamentId} {matchId} />
            </div>
        </Cell>
        <Cell spam={4}>
            <SongOptions {tournamentId} {matchId} />
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

    .match-title {
        color: var(--mdc-theme-text-primary-on-background);
        background-color: rgba($color: #000000, $alpha: 0.1);
        border-radius: 2vmin;
        text-align: center;
        font-size: 2rem;
        font-weight: 100;
        line-height: 1.1;
        padding: 2vmin;
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
