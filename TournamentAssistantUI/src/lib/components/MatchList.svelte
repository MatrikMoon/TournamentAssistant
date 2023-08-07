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

  export let tournamentId: string;

  let localMatchesInstance =
    $taService.client.stateManager.getTournament(tournamentId)?.matches;

  function onChange() {
    localMatchesInstance =
      $taService.client.stateManager.getTournament(tournamentId)!.matches;
  }

  //When changes happen to the user list, re-render
  $taService.client.on("joinedTournament", onChange);
  $taService.subscribeToMatchUpdates(onChange);
  onDestroy(() => {
    $taService.client.removeListener("joinedTournament", onChange);
    $taService.unsubscribeFromMatchUpdates(onChange);
  });

  $: console.log({ localMatchesInstance });

  $: matches =
    localMatchesInstance?.map((x) => {
      const leader = $taService.client.stateManager.getUser(
        tournamentId,
        x.leader
      );
      return {
        guid: x.guid,
        name: `${leader?.discordInfo?.username}'s match`,
        image: leader!.userId
          ? `https://cdn.scoresaber.com/avatars/${leader!.userId}.jpg`
          : leader!.discordInfo?.avatarUrl,
        players: x.associatedUsers.map((y) =>
          $taService.client.stateManager.getUser(tournamentId, y)
        ),
      };
    }) ?? [];
</script>

<List twoLine avatarList singleSelection>
  {#each matches as item}
    <Item
      on:SMUI:action={() => {
        //$selectedUserGuid = item.guid;
      }}
      selected={false}
    >
      <Graphic
        style="background-image: url({item.image}); background-size: contain"
      />
      <Text>
        <PrimaryText>{item.name}</PrimaryText>
        <SecondaryText
          >{item.players
            .map((x) => x?.discordInfo?.username ?? x?.name)
            .join(",")}</SecondaryText
        >
      </Text>
    </Item>
  {/each}
</List>
