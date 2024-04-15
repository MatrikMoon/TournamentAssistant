<script lang="ts">
  import List, { Item, Graphic, Text, PrimaryText } from "@smui/list";
  import type { Tournament } from "tournament-assistant-client";

  export let tournament: Tournament;

  let teams = [
    {
      guid: "000000000000000000",
      name: "Test Team",
      image: [1],
    },
  ];

  // $: teams = tournament?.settings?.teams.map((x) => {
  //   let byteArray = x.image;

  //   // Only make the blob url if there is actually image data
  //   if ((byteArray?.length ?? 0) > 1) {
  //     // Sometimes it's not parsed as a Uint8Array for some reason? So we'll shunt it back into one
  //     if (!(x.image instanceof Uint8Array)) {
  //       byteArray = new Uint8Array(Object.values(x.image!));
  //     }

  //     var blob = new Blob([byteArray!], {
  //       type: "image/jpeg",
  //     });

  //     var urlCreator = window.URL || window.webkitURL;
  //     var imageUrl = urlCreator.createObjectURL(blob);

  //     return {
  //       ...x,
  //       imageUrl,
  //     };
  //   }

  //   // Set the image to undefined if we couldn't make a blob of it
  //   return x;
  // }) ?? [
  //   {
  //     guid: "000000000000000000",
  //     name: "Test Team",
  //     image: [1],
  //   },
  // ];
</script>

<List twoLine avatarList>
  {#each teams as item}
    <Item>
      <Graphic
        style="background-image: url({item.image}); background-size: contain"
      />
      <Text>
        <PrimaryText>{item.name}</PrimaryText>
      </Text>
    </Item>
  {/each}
</List>
