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
  import { masterAddress, masterApiPort } from "tournament-assistant-client";

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

  $: qualifiers = localQualifiersInstance ?? [];
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
        style="background-image: url({item.image?.length > 0
          ? `https://${masterAddress}:${masterApiPort}/api/file/${item.image}`
          : defaultLogo}); background-size: contain"
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
