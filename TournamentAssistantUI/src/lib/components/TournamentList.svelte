<script lang="ts">
  import List, {
    Item,
    Graphic,
    Text,
    PrimaryText,
    SecondaryText,
  } from "@smui/list";
  import defaultLogo from "../assets/icon.png";
  import type { Tournament } from "tournament-assistant-client";
  import { masterClient } from "$lib/stores";
  import { onDestroy } from "svelte";

  export let onTournamentSelected = (
    id: string,
    address: string,
    port: string
  ) => {};

  let tournaments: Tournament[] = $masterClient.stateManager.getTournaments();

  function onChange() {
    tournaments = $masterClient.stateManager.getTournaments();
  }

  //When changes happen to the user list, re-render
  $masterClient.stateManager.on("tournamentCreated", onChange);
  $masterClient.stateManager.on("tournamentUpdated", onChange);
  $masterClient.stateManager.on("tournamentDeleted", onChange);
  onDestroy(() => {
    $masterClient.stateManager.removeListener("tournamentCreated", onChange);
    $masterClient.stateManager.removeListener("tournamentUpdated", onChange);
    $masterClient.stateManager.removeListener("tournamentDeleted", onChange);
  });

  //Convert image bytes to blob URLs
  $: tournamentsWithImagesAsUrls = tournaments.map((x) => {
    let byteArray = x.settings?.tournamentImage;

    //Only make the blob url if there is actually image data
    if ((byteArray?.length ?? 0) > 1) {
      //Sometimes it's not parsed as a Uint8Array for some reason? So we'll shunt it back into one
      if (!(x.settings?.tournamentImage instanceof Uint8Array)) {
        byteArray = new Uint8Array(Object.values(x.settings?.tournamentImage!));
      }

      var blob = new Blob([byteArray!], {
        type: "image/jpeg",
      });

      var urlCreator = window.URL || window.webkitURL;
      var imageUrl = urlCreator.createObjectURL(blob);

      return {
        ...x,
        settings: {
          ...x.settings,
          tournamentImage: imageUrl,
        },
      };
    }

    //Set the image to undefined if we couldn't make a blob of it
    return {
      ...x,
      settings: {
        ...x.settings,
        tournamentImage: undefined,
      },
    };
  });
</script>

<List twoLine avatarList singleSelection>
  {#each tournamentsWithImagesAsUrls as item}
    <Item
      on:SMUI:action={() => {
        const address = item.server?.address;
        const port = `${item.server?.websocketPort}`;

        if (!address || !port) {
          return;
        }

        onTournamentSelected(item.guid, address, port);
        console.log(
          `selected: ${item.settings?.tournamentName} ${address}:${port}`
        );
      }}
    >
      <Graphic
        style="background-image: url({item.settings?.tournamentImage ??
          defaultLogo}); background-size: contain"
      />
      <Text>
        <PrimaryText>
          {item.settings?.tournamentName}
        </PrimaryText>
        <SecondaryText
          >{`${item.server?.address}:${item.server?.websocketPort}`}</SecondaryText
        >
      </Text>
    </Item>
  {/each}
</List>
