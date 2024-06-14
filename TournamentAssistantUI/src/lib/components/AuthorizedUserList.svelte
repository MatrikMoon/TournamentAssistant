<script lang="ts">
  import { getSelectedEnumMembers } from "$lib/utils";
  import List, {
    Item,
    Graphic,
    Text,
    PrimaryText,
    SecondaryText,
    Meta,
  } from "@smui/list";
  import {
    Permissions,
    type Response_GetAuthorizedUsers_AuthroizedUser,
  } from "tournament-assistant-client";

  export let showRemoveButton = false;
  export let onRemoveClicked: (
    authorizedUser: Response_GetAuthorizedUsers_AuthroizedUser,
  ) => Promise<void> = async (a) => {};

  export let authorizedUsers: Response_GetAuthorizedUsers_AuthroizedUser[] = [];
</script>

<List twoLine avatarList>
  {#each authorizedUsers as item}
    <Item>
      <Graphic
        style="background-image: url({item.discordAvatarUrl}); background-size: contain"
      />
      <Text>
        <PrimaryText>{item.discordUsername}</PrimaryText>
        <SecondaryText>
          {getSelectedEnumMembers(Permissions, item.permission)
            .filter((x) => x !== Permissions[Permissions.None])
            .map((x) => `${x}`)
            .join(" / ")}
        </SecondaryText>
      </Text>
      {#if showRemoveButton}
        <Meta
          class="material-icons"
          on:click$stopPropagation={() => onRemoveClicked(item)}
        >
          close
        </Meta>
      {/if}
    </Item>
  {/each}
</List>
