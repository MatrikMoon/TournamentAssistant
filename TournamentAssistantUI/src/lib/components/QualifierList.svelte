<script lang="ts">
  import { taService } from "../stores";
  import { onDestroy } from "svelte";
  import List, {
    Item,
    Graphic,
    Text,
    PrimaryText,
    SecondaryText,
  } from "@smui/list";
  import defaultLogo from "../assets/icon.png";

  export let tournamentId: string;

  export let onQualifierSelected = (id: string) => {};

  // TAService now includes a getTournament wrapper, but I'm leaving this here for now since it's
  // extremely unlikely that we're still not connected to the server by the time we're showing this list
  let localQualifiersInstance =
    $taService.client.stateManager.getTournament(tournamentId)?.qualifiers;

  function onChange() {
    localQualifiersInstance =
      $taService.client.stateManager.getTournament(tournamentId)!.qualifiers;
  }

  //When changes happen to the qualifier list, re-render
  $taService.client.on("joinedTournament", onChange);
  $taService.subscribeToQualifierUpdates(onChange);
  onDestroy(() => {
    $taService.client.removeListener("joinedTournament", onChange);
    $taService.unsubscribeFromQualifierUpdates(onChange);
  });

  $: qualifiers =
    localQualifiersInstance?.map((x) => {
      let byteArray = x.image;

      //Only make the blob url if there is actually image data
      if ((byteArray?.length ?? 0) > 1) {
        //Sometimes it's not parsed as a Uint8Array for some reason? So we'll shunt it back into one
        if (!(x.image instanceof Uint8Array)) {
          byteArray = new Uint8Array(Object.values(x.image!));
        }

        var blob = new Blob([byteArray!], {
          type: "image/jpeg",
        });

        var urlCreator = window.URL || window.webkitURL;
        var imageUrl = urlCreator.createObjectURL(blob);

        return {
          ...x,
          image: imageUrl,
        };
      }

      //Set the image to undefined if we couldn't make a blob of it
      return {
        ...x,
        image: undefined,
      };
    }) ?? [];
</script>

<List twoLine avatarList singleSelection>
  {#each qualifiers as item}
    <Item
      on:SMUI:action={() => {
        onQualifierSelected(item.guid);
      }}
      selected={false}
    >
      <Graphic
        style="background-image: url({item.image ??
          defaultLogo}); background-size: contain"
      />
      <Text>
        <PrimaryText>{item.name}</PrimaryText>
        <SecondaryText>
          {item.qualifierMaps.length} songs
        </SecondaryText>
      </Text>
    </Item>
  {/each}
</List>
