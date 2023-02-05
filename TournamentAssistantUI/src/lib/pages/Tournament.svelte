<script lang="ts">
    import LayoutGrid, { Cell } from "@smui/layout-grid";
    import UserList from "../components/UserList.svelte";
    import DebugLog from "../components/DebugLog.svelte";

    export let tournamentId: string;

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
