<script lang="ts">
    import { client } from "../stores";
    import List, {
        Item,
        Graphic,
        Text,
        PrimaryText,
        SecondaryText,
    } from "@smui/list";
    import logo from "../assets/icon.png";

    let componentKnownServersInstance = $client.knownServers;

    //When changes happen to the server list, re-render
    $client.on(
        "serverAdded",
        () => (componentKnownServersInstance = $client.knownServers)
    );
    $client.on(
        "serverDeleted",
        () => (componentKnownServersInstance = $client.knownServers)
    );

    $: tournaments = componentKnownServersInstance.flatMap((x) => {
        // let byteArray = x.info?.userImage;

        // if (!(x.info?.userImage instanceof Uint8Array)) {
        //     byteArray = new Uint8Array(Object.values(x.info?.userImage!));
        // }

        // var blob = new Blob([byteArray!], {
        //     type: "image/jpeg",
        // });

        // var urlCreator = window.URL || window.webkitURL;
        // var imageUrl = urlCreator.createObjectURL(blob);

        return {
            name: x.name,
            address: x.address,
            port: x.port,
            //image: imageUrl,
        };
    });
</script>

<List twoLine avatarList singleSelection>
    {#each servers as item}
        <Item
            on:SMUI:action={() => {
                console.log(`selected: ${item.address}:${item.port}`);
            }}
        >
            <Graphic
                style="background-image: url({logo}); background-size: contain"
            />
            <Text>
                <PrimaryText>{item.name}</PrimaryText>
                <SecondaryText>{item.address}:{item.port}</SecondaryText>
            </Text>
        </Item>
    {/each}
</List>
