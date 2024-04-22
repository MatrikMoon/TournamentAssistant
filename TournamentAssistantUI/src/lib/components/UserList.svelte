<script lang="ts">
  import { taService } from "../stores";
  import { onDestroy, onMount } from "svelte";
  import List, {
    Item,
    Graphic,
    Text,
    PrimaryText,
    SecondaryText,
  } from "@smui/list";
  import {
    type User,
    User_PlayStates,
    User_ClientTypes,
    User_DownloadStates,
  } from "tournament-assistant-client";

  export let serverAddress: string;
  export let serverPort: string;
  export let tournamentId: string;
  export let matchId: string | undefined = undefined;
  export let selectedUsers: User[] = [];

  let localUsersInstance: User[] = [];

  onMount(async () => {
    console.log("onMount getUsers");
    await onChange();
  });

  async function onChange() {
    localUsersInstance = (await $taService.getTournament(
      serverAddress,
      serverPort,
      tournamentId,
    ))!.users;

    //Make sure players already in a match don't show up in the list, or
    //if a match is already selected, *only* those players show up in the list
    if (matchId) {
      const match = await $taService.getMatch(
        serverAddress,
        serverPort,
        tournamentId,
        matchId,
      );

      localUsersInstance = localUsersInstance.filter(
        (x) =>
          x.clientType === User_ClientTypes.Player &&
          x.playState !== User_PlayStates.InMenu &&
          match?.associatedUsers.includes(x.guid),
      );
    } else {
      const matches = await $taService.getMatches(
        serverAddress,
        serverPort,
        tournamentId,
      );

      localUsersInstance = localUsersInstance.filter(
        (x) =>
          x.clientType === User_ClientTypes.Player &&
          x.playState === User_PlayStates.WaitingForCoordinator &&
          !matches?.find((y) => y.associatedUsers.includes(x.guid)),
      );
    }

    //Make sure only players in the list can be selected
    selectedUsers = selectedUsers.filter((x) =>
      localUsersInstance?.find((y) => y.guid === x.guid),
    );
  }

  //When changes happen to the user list, re-render
  $taService.client.on("joinedTournament", onChange);
  $taService.subscribeToUserUpdates(onChange);
  $taService.subscribeToMatchUpdates(onChange);
  onDestroy(() => {
    $taService.client.removeListener("joinedTournament", onChange);
    $taService.unsubscribeFromUserUpdates(onChange);
    $taService.unsubscribeFromMatchUpdates(onChange);
  });

  $: users =
    localUsersInstance?.map((x) => {
      return {
        guid: x.guid,
        name: x.name.length > 0 ? x.name : x.discordInfo?.username,
        // TODO: Once TAAuth makes these null rather than empty strings, we can go back to `??`
        image: x.discordInfo?.avatarUrl
          ? x.discordInfo.avatarUrl
          : `https://cdn.scoresaber.com/avatars/${x.platformId}.jpg`,
        state: `${User_PlayStates[x.playState]}, ${User_DownloadStates[x.downloadState]}`,
      };
    }) ?? [];
</script>

<List twoLine avatarList>
  {#each users as item}
    <Item
      on:SMUI:action={async () => {
        const user = await $taService.getUser(
          serverAddress,
          serverPort,
          tournamentId,
          item.guid,
        );

        //Add or remove the user from the selected list depending on its current state
        if (user) {
          if (!selectedUsers.find((x) => x.guid === item.guid)) {
            selectedUsers = [...selectedUsers, user];
          } else {
            selectedUsers = selectedUsers.filter((x) => x.guid !== item.guid);
          }
        }
      }}
      selected={selectedUsers.find((x) => x.guid == item.guid) !== undefined}
    >
      <Graphic
        style="background-image: url({item.image}); background-size: contain"
      />
      <Text>
        <PrimaryText>{item.name}</PrimaryText>
        <SecondaryText>{item.state}</SecondaryText>
      </Text>
    </Item>
  {/each}
</List>
