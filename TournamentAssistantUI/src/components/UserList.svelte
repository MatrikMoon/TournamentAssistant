<script lang="ts">
    import { client, selectedUserGuid } from "../stores";
    import List, {
        Item,
        Graphic,
        Text,
        PrimaryText,
        SecondaryText,
    } from "@smui/list";

    let localUsersInstance = $client.users;

    //When changes happen to the user list, re-render
    $client.on("userConnected", () => (localUsersInstance = $client.users));
    $client.on("userUpdated", () => (localUsersInstance = $client.users));
    $client.on("userDisconnected", () => (localUsersInstance = $client.users));

    $: users = localUsersInstance.map((x) => {
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
                <SecondaryText>{item.description}</SecondaryText>
            </Text>
        </Item>
    {/each}
</List>
