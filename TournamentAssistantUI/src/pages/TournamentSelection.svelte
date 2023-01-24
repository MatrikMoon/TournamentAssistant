<script lang="ts">
    import { client } from "../stores";
    import List, {
        Item,
        Graphic,
        Text,
        PrimaryText,
        SecondaryText,
    } from "@smui/list";
    import Button, { Label } from "@smui/button";
    import logo from "../assets/icon.png";
    import { Scraper } from "tournament-assistant-client";

    // let knownServers = $client.knownServers;

    // //When changes happen to the server list, re-render
    // $client.on("serverAdded", () => (knownServers = $client.knownServers));
    // $client.on("serverDeleted", () => (knownServers = $client.knownServers));

    // $: tournaments = knownServers.flatMap((x) => {
    //     // let byteArray = x.info?.userImage;

    //     // if (!(x.info?.userImage instanceof Uint8Array)) {
    //     //     byteArray = new Uint8Array(Object.values(x.info?.userImage!));
    //     // }

    //     // var blob = new Blob([byteArray!], {
    //     //     type: "image/jpeg",
    //     // });

    //     // var urlCreator = window.URL || window.webkitURL;
    //     // var imageUrl = urlCreator.createObjectURL(blob);

    //     return {
    //         guid: "a",
    //         name: "a",
    //         imageUrl: "a",
    //         description: "a",
    //     };
    // });

    let tournaments = [];
</script>

<List twoLine avatarList singleSelection>
    {#each tournaments as item}
        <Item
            on:SMUI:action={() => {
                console.log(`selected: ${item.guid}`);
            }}
        >
            <Graphic
                style="background-image: url({logo}); background-size: contain"
            />
            <Text>
                <PrimaryText>{item.name}</PrimaryText>
                <SecondaryText>{item.description}</SecondaryText>
            </Text>
        </Item>
    {/each}
</List>
<Button
    variant="raised"
    on:click={() => {
        const test = new Scraper.Scraper();
        test.getTournaments();
    }}
>
    <Label>Test</Label>
</Button>
