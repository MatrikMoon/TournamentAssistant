<script lang="ts">
  import List, {
    Item,
    Text,
    PrimaryText,
    SecondaryText,
    Meta,
  } from "@smui/list";
  import type {
    Tournament,
    Tournament_TournamentSettings_Team,
  } from "tournament-assistant-client";
  import defaultLogo from "../assets/icon.png";

  export let tournament: Tournament;
  export let onRemoveClicked: (
    team: Tournament_TournamentSettings_Team,
  ) => Promise<void>;

  $: teams =
    tournament?.settings?.teams.map((x) => {
      let byteArray = x.image;

      // Only make the blob url if there is actually image data
      if ((byteArray?.length ?? 0) > 1) {
        // Sometimes it's not parsed as a Uint8Array for some reason? So we'll shunt it back into one
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
          imageUrl,
        };
      }

      // Set the image to undefined if we couldn't make a blob of it
      return { ...x, imageUrl: undefined };
    }) ?? [];
</script>

<List twoLine avatarList>
  {#each teams as team}
    <Item>
      <img alt="" class={"team-image"} src={team.imageUrl ?? defaultLogo} />
      <Text>
        <PrimaryText>{team.name}</PrimaryText>
        <!-- <SecondaryText>test</SecondaryText> -->
      </Text>
      <Meta class="material-icons" on:click={() => onRemoveClicked(team)}>
        close
      </Meta>
    </Item>
  {/each}
</List>

<style lang="scss">
  .team-image {
    width: 55px;
    height: 55px;
    border-radius: 50%;
    margin: 1vmin;
    object-fit: cover;
  }
</style>
