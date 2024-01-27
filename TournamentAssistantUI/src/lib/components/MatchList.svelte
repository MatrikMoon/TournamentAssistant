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
  import { goto } from "$app/navigation";

  export let serverAddress: string;
  export let serverPort: string;
  export let tournamentId: string;

  // TAService now includes a getTournament wrapper, but I'm leaving this here for now since it's
  // extremely unlikely that we're still not connected to the server by the time we're showing this list
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

  $: matches =
    localMatchesInstance?.map((x) => {
      const leader = $taService.client.stateManager.getUser(
        tournamentId,
        x.leader,
      );
      return {
        guid: x.guid,
        name: `${leader?.discordInfo?.username}'s match`,
        image:
          leader?.discordInfo?.avatarUrl ??
          `https://cdn.scoresaber.com/avatars/${leader?.platformId}.jpg`,
        players: x.associatedUsers.map((y) =>
          $taService.client.stateManager.getUser(tournamentId, y),
        ),
      };
    }) ?? [];
</script>

<List twoLine avatarList singleSelection>
  {#each matches as item}
    <Item
      on:SMUI:action={() => {
        goto(
          `/tournament/match?tournamentId=${tournamentId}&address=${serverAddress}&port=${serverPort}&matchId=${item.guid}`,
        );
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
