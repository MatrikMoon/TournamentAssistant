<script lang="ts">
    import { client, selectedUserGuid } from "../stores";
    import List, {
        Item,
        Graphic,
        Text,
        PrimaryText,
        SecondaryText,
    } from "@smui/list";

    export let tournamentId: string;

    let localUsersInstance =
        $client.stateManager.getTournament(tournamentId)?.users;

    //When changes happen to the user list, re-render
    $client.stateManager.on(
        "userConnected",
        () =>
            (localUsersInstance =
                $client.stateManager.getTournament(tournamentId)!.users)
    );
    $client.stateManager.on(
        "userUpdated",
        () =>
            (localUsersInstance =
                $client.stateManager.getTournament(tournamentId)!.users)
    );
    $client.stateManager.on(
        "userDisconnected",
        () =>
            (localUsersInstance =
                $client.stateManager.getTournament(tournamentId)!.users)
    );

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
