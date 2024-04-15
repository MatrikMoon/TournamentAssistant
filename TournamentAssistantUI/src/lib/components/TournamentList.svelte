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
  import { taService } from "$lib/stores";
  import { onDestroy, onMount } from "svelte";
  import Button from "@smui/button";

  export let onTournamentSelected = (
    id: string,
    address: string,
    port: string,
  ) => {};

  let tournaments: Tournament[] = [];

  onMount(async () => {
    console.log("onMount getTournaments");
    await onChange();
  });

  async function onChange() {
    tournaments = await $taService.getTournaments();
  }

  //When changes happen to the user list, re-render
  $taService.subscribeToTournamentUpdates(onChange);
  onDestroy(() => {
    $taService.unsubscribeFromTournamentUpdates(onChange);
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
          `selected: ${item.settings?.tournamentName} ${address}:${port}`,
        );
      }}
    >
      <img
        alt=""
        class={"tournament-image"}
        src={item.settings?.tournamentImage ?? defaultLogo}
      />

      <Text>
        <PrimaryText>
          {item.settings?.tournamentName}
        </PrimaryText>
        <SecondaryText>
          {`${item.server?.address}:${item.server?.websocketPort}`}
        </SecondaryText>
      </Text>
      <!-- <Button
        on:click={() => {
          const address = item.server?.address;
          const port = `${item.server?.websocketPort}`;
          const tournament = tournaments.find((x) => x.guid === item.guid);

          if (!address || !port || !tournament) {
            return;
          }

          $taService.deleteTournament(address, port, tournament);
        }}
      >
        DELETE
      </Button> -->
    </Item>
  {/each}
</List>

<style lang="scss">
  .tournament-image {
    width: 55px;
    height: 55px;
    border-radius: 50%;
    margin: 1vmin;
    object-fit: cover;
  }
</style>
