<script lang="ts">
  import List, {
    Item,
    Text,
    PrimaryText,
    SecondaryText,
    Meta,
  } from "@smui/list";
  import {
    masterAddress,
    masterApiPort,
    type Tournament,
    type Tournament_TournamentSettings_Team,
  } from "tournament-assistant-client";
  import defaultLogo from "../assets/icon.png";

  export let tournament: Tournament;
  export let onRemoveClicked: (
    team: Tournament_TournamentSettings_Team
  ) => Promise<void>;

  $: teams = tournament?.settings?.teams ?? [];
</script>

<List twoLine avatarList>
  {#each teams as team}
    <Item>
      <img
        alt=""
        class={"team-image"}
        src={team.image?.length > 0
          ? `https://${masterAddress}:${masterApiPort}/api/file/${team.image}`
          : defaultLogo}
      />
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
