<script lang="ts">
    import { client } from "../stores";
    import { onDestroy } from "svelte";
    import List, {
        Item,
        Graphic,
        Text,
        PrimaryText,
        SecondaryText,
    } from "@smui/list";
    import {
        User_DownloadStates,
        type User,
        User_PlayStates,
    } from "tournament-assistant-client";

    export let tournamentId: string;
    export let matchId: string | undefined = undefined;
    export let selectedUsers: User[] = [];

    let localUsersInstance =
        $client.stateManager.getTournament(tournamentId)?.users;

    function onChange() {
        localUsersInstance =
            $client.stateManager.getTournament(tournamentId)!.users;

        //Make sure players already in a match don't show up in the list, or
        //if a match is already selected, *only* those players show up in the list
        if (matchId) {
            const match = $client.stateManager.getMatch(tournamentId, matchId);
            localUsersInstance = localUsersInstance.filter((x) =>
                match?.associatedUsers.includes(x.guid)
            );
        } else {
            const matches = $client.stateManager.getMatches(tournamentId);
            localUsersInstance = localUsersInstance.filter(
                (x) => !matches?.find((y) => y.associatedUsers.includes(x.guid))
            );
        }

        //Make sure only players in the list can be selected
        selectedUsers = selectedUsers.filter((x) =>
            localUsersInstance?.find((y) => y.guid === x.guid)
        );
    }

    //When changes happen to the user list, re-render
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

    $: users =
        localUsersInstance?.map((x) => {
            return {
                guid: x.guid,
                name: x.name.length > 0 ? x.name : x.discordInfo?.username,
                image: x.userId
                    ? `https://cdn.scoresaber.com/avatars/${x.userId}.jpg`
                    : x.discordInfo?.avatarUrl,
                state: User_PlayStates[x.playState],
            };
        }) ?? [];
</script>

<List twoLine avatarList>
    {#each users as item}
        <Item
            on:SMUI:action={(e) => {
                const user = $client.stateManager.getUser(
                    tournamentId,
                    item.guid
                );

                //Add or remove the user from the selected list depending on its current state
                if (user) {
                    if (!selectedUsers.find((x) => x.guid === item.guid)) {
                        selectedUsers = [...selectedUsers, user];
                    } else {
                        selectedUsers = selectedUsers.filter(
                            (x) => x.guid !== item.guid
                        );
                    }
                }
            }}
            selected={selectedUsers.find((x) => x.guid == item.guid) !==
                undefined}
        >
            <Graphic
                style="background-image: url({item.image}); background-size: contain"
            />
            <Text>
                <PrimaryText>{item.name}</PrimaryText>
                <SecondaryText>{item.state}</SecondaryText>
            </Text>
        </Item>
    {/each}
</List>
