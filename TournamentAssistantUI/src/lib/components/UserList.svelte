<script lang="ts">
    import { client, selectedUserGuid } from "../stores";
    import { onDestroy } from "svelte";
    import List, {
        Item,
        Graphic,
        Text,
        PrimaryText,
        SecondaryText,
    } from "@smui/list";

    export let tournamentId: string;

    let tournament = $client.stateManager.getTournament(tournamentId);
    let localUsersInstance =
        $client.stateManager.getTournament(tournamentId)?.users;

    $: console.log({ tournament });
    $: console.log({ localUsersInstance });

    function onChange() {
        localUsersInstance =
            $client.stateManager.getTournament(tournamentId)!.users;
    }

    //When changes happen to the user list, re-render
    $client.on("joinedTournament", onChange);
    $client.stateManager.on("userConnected", onChange);
    $client.stateManager.on("userUpdated", onChange);
    $client.stateManager.on("userDisconnected", onChange);
    onDestroy(() => {
        $client.removeListener("joinedTournament", onChange);
        $client.stateManager.removeListener("userConnected", onChange);
        $client.stateManager.removeListener("userUpdated", onChange);
        $client.stateManager.removeListener("userDisconnected", onChange);
    });

    $: users =
        localUsersInstance?.map((x) => {
            return {
                guid: x.guid,
                name: x.name,
                image: `https://cdn.scoresaber.com/avatars/${x.userId}.jpg`,
            };
        }) ?? [];
</script>

<List twoLine avatarList singleSelection>
    {#each users as item}
        <Item
            on:SMUI:action={() => {
                $selectedUserGuid = item.guid;
            }}
            selected={$selectedUserGuid === item.guid}
        >
            <Graphic
                style="background-image: url({item.image}); background-size: contain"
            />
            <Text>
                <PrimaryText>{item.name}</PrimaryText>
                <SecondaryText>Description</SecondaryText>
            </Text>
        </Item>
    {/each}
</List>
