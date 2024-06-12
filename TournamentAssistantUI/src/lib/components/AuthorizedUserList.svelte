<script lang="ts">
  import List, {
    Item,
    Graphic,
    Text,
    PrimaryText,
    SecondaryText,
  } from "@smui/list";
  import type { Response_GetAuthorizedUsers_AuthroizedUser } from "tournament-assistant-client";

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
        <SecondaryText>{item.permission}</SecondaryText>
      </Text>
    </Item>
  {/each}
</List>
