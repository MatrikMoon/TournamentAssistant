<script lang="ts">
    import LayoutGrid, { Cell } from "@smui/layout-grid";
    import UserList from "../components/UserList.svelte";
    import DebugLog from "../components/DebugLog.svelte";
    import { client, selectedUserGuid } from "../stores";
    import List, {
        Item,
        Graphic,
        Text,
        PrimaryText,
        SecondaryText,
    } from "@smui/list";

    let componentKnownServersInstance = $client.knownHosts;

    //When changes happen to the host list, re-render
    $client.on(
        "hostAdded",
        () => (componentKnownServersInstance = $client.knownHosts)
    );
    $client.on(
        "hostDeleted",
        () => (componentKnownServersInstance = $client.knownHosts)
    );

    $: hosts = componentKnownServersInstance.map((x) => {
        let byteArray = x.info?.userImage;

        if (!(x.info?.userImage instanceof Uint8Array)) {
            byteArray = new Uint8Array(Object.values(x.info?.userImage!));
        }

        var blob = new Blob([byteArray!], {
            type: "image/jpeg",
        });

        var urlCreator = window.URL || window.webkitURL;
        var imageUrl = urlCreator.createObjectURL(blob);

        return {
            guid: x.guid,
            name: x.name,
            description: x.info?.machineName,
            image: imageUrl,
        };
    });
</script>

<LayoutGrid>
    <Cell span={8} class="grid-cell">TODO</Cell>
    <Cell span={4} class="grid-cell">
        <UserList />
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
