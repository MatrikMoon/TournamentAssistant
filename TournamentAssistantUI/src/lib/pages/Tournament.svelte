<script lang="ts">
    import LayoutGrid, { Cell } from "@smui/layout-grid";
    import UserList from "../components/UserList.svelte";
    import DebugLog from "../components/DebugLog.svelte";

    export let tournamentId: string;
</script>

<LayoutGrid>
    <Cell span={8} class="grid-cell">TODO</Cell>
    <Cell span={4} class="grid-cell">
        <UserList {tournamentId} />
    </Cell>
    <Cell span={12} class="grid-cell">
        <DebugLog />
    </Cell>
</LayoutGrid>

<style lang="scss">
    :global {
        body {
            .mdc-tab {
                .mdc-tab__text-label {
                    color: var(--mdc-theme-text-secondary-on-background);
                }

                &--active {
                    .mdc-tab__text-label {
                        color: var(--mdc-theme-primary);
                    }
                }
            }
        }

        .grid-cell {
            background-color: rgba($color: #000000, $alpha: 0.1);
        }
    }
</style>
