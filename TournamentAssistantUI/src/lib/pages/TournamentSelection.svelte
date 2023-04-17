<script lang="ts">
    import List, {
        Item,
        Graphic,
        Text,
        PrimaryText,
        SecondaryText,
    } from "@smui/list";
    import defaultLogo from "../assets/icon.png";
    import { getTournaments } from "tournament-assistant-client";
    import type { TournamentWithServerInfo } from "tournament-assistant-client";
    import { authToken } from "$lib/stores";

    export let onTournamentSelected = (
        id: string,
        address: string,
        port: string
    ) => {};
    export const refreshTournaments = () => {
        getTournaments(
            $authToken,
            (totalServers, succeededServers, failedServers) => {
                console.log({ totalServers, succeededServers, failedServers });
            },
            (initialTournaments) => {
                tournaments = initialTournaments;
            }
        );
    };

    let tournaments: TournamentWithServerInfo[] = [];

    //Scrape master server for tournaments
    refreshTournaments();

    //Convert image bytes to blob URLs
    $: tournamentsWithImagesAsUrls = tournaments.map((x) => {
        let byteArray = x.tournament?.settings?.tournamentImage;

        //Only make the blob url if there is actually image data
        if ((byteArray?.length ?? 0) > 1) {
            //Sometimes it's not parsed as a Uint8Array for some reason? So we'll shunt it back into one
            if (
                !(x.tournament?.settings?.tournamentImage instanceof Uint8Array)
            ) {
                byteArray = new Uint8Array(
                    Object.values(x.tournament?.settings?.tournamentImage!)
                );
            }

            var blob = new Blob([byteArray!], {
                type: "image/jpeg",
            });

            var urlCreator = window.URL || window.webkitURL;
            var imageUrl = urlCreator.createObjectURL(blob);

            return {
                ...x,
                tournament: {
                    ...x.tournament,
                    settings: {
                        ...x.tournament.settings,
                        tournamentImage: imageUrl,
                    },
                },
            };
        }

        //Set the image to undefined if we couldn't make a blob of it
        return {
            ...x,
            tournament: {
                ...x.tournament,
                settings: {
                    ...x.tournament.settings,
                    tournamentImage: undefined,
                },
            },
        };
    });
</script>

<List twoLine avatarList singleSelection>
    {#each tournamentsWithImagesAsUrls as item}
        <Item
            on:SMUI:action={() => {
                onTournamentSelected(
                    item.tournament.guid,
                    item.address,
                    item.port
                );
                console.log(
                    `selected: ${item.tournament.settings?.tournamentName} ${item.address}:${item.port}`
                );
            }}
        >
            <Graphic
                style="background-image: url({item.tournament.settings
                    ?.tournamentImage ??
                    defaultLogo}); background-size: contain"
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
