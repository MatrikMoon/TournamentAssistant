<script lang="ts">
    import List, {
        Item,
        Graphic,
        Text,
        PrimaryText,
        SecondaryText,
    } from "@smui/list";
    import Button, { Label } from "@smui/button";
    import logo from "../assets/icon.png";
    import { getTournaments } from "tournament-assistant-client";
    import type { TournamentWithServerInfo } from "tournament-assistant-client";
    import { client } from "../stores";
    import { v4 as uuidv4 } from "uuid";

    $client.connect();

    export let onTournamentSelected = (id: string) => {};

    let tournaments: TournamentWithServerInfo[] = [];

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
                <PrimaryText
                    >{item.tournament.settings?.tournamentName}</PrimaryText
                >
                <SecondaryText>{`${item.address}:${item.port}`}</SecondaryText>
            </Text>
        </Item>
    {/each}
</List>
<Button
    variant="raised"
    on:click={() => {
        getTournaments(
            (totalServers, succeededServers, failedServers) => {
                console.log({ totalServers, succeededServers, failedServers });
            },
            (initialTournaments) => {
                console.log("success");
                tournaments = initialTournaments;
            }
        );
    }}
>
    <Label>Test</Label>
</Button>
<Button
    variant="raised"
    on:click={() => {
        $client.createTournament({
            guid: uuidv4(),
            users: [],
            matches: [],
            qualifiers: [],
            settings: {
                tournamentName: "Test Tournament",
                tournamentImage: new Uint8Array([1]),
                enableTeams: false,
                teams: [],
                scoreUpdateFrequency: 30,
                bannedMods: [],
            },
        });
    }}
>
    <Label>Create dummy tournament</Label>
</Button>
