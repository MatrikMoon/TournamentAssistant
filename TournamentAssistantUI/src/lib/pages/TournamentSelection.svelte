<script lang="ts">
    import List, {
        Item,
        Graphic,
        Text,
        PrimaryText,
        SecondaryText,
    } from "@smui/list";
    import logo from "../assets/icon.png";
    import { getTournaments } from "tournament-assistant-client";
    import type { TournamentWithServerInfo } from "tournament-assistant-client";

    export let onTournamentSelected = (id: string) => {};

    let tournaments: TournamentWithServerInfo[] = [];

    getTournaments(
        (totalServers, succeededServers, failedServers) => {
            console.log({ totalServers, succeededServers, failedServers });
        },
        (initialTournaments) => {
            console.log("success");
            tournaments = initialTournaments;
        }
    );

    $: console.log({ tournaments });
</script>

<List twoLine avatarList singleSelection>
    {#each tournaments as item}
        <Item
            on:SMUI:action={() => {
                onTournamentSelected(item.tournament.guid);
                console.log(
                    `selected: ${item.tournament.settings?.tournamentName} ${item.address}:${item.port}`
                );
            }}
        >
            <Graphic
                style="background-image: url({logo}); background-size: contain"
            />
            <Text>
                <PrimaryText>
                    {item.tournament.settings?.tournamentName}
                </PrimaryText>
                <SecondaryText>{`${item.address}:${item.port}`}</SecondaryText>
            </Text>
        </Item>
    {/each}
</List>
