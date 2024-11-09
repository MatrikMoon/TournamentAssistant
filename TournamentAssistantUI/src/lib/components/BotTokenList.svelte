<script lang="ts">
  import List, {
    Item,
    Text,
    PrimaryText,
    SecondaryText,
    Meta,
  } from "@smui/list";
  import { Wrapper } from "@smui/tooltip";
  import Tooltip from "@smui/tooltip/src/Tooltip.svelte";
  import { Response_GetBotTokensForUser_BotUser } from "tournament-assistant-client";

  export let onCopyClicked: (
    botUser: Response_GetBotTokensForUser_BotUser,
  ) => Promise<void> = async (a) => {};

  export let onRemoveClicked: (
    botUser: Response_GetBotTokensForUser_BotUser,
  ) => Promise<void> = async (a) => {};

  export let botUsers: Response_GetBotTokensForUser_BotUser[] = [];
</script>

<List twoLine>
  {#each botUsers as item}
    <Item>
      <Text>
        <div class="content">
          <PrimaryText>{item.username}</PrimaryText>
          <SecondaryText>
            {item.guid}
          </SecondaryText>
        </div>
      </Text>
      <div class="meta-buttons">
        <Wrapper>
          <Meta
            class="material-icons"
            on:click$stopPropagation={() => onCopyClicked(item)}
          >
            content_copy
          </Meta>
          <Tooltip>Copy Bot Guid</Tooltip>
        </Wrapper>
        <Wrapper>
          <Meta
            class="material-icons"
            on:click$stopPropagation={() => onRemoveClicked(item)}
          >
            close
          </Meta>
          <Tooltip>Revoke Bot Token</Tooltip>
        </Wrapper>
      </div>
    </Item>
  {/each}
</List>

<style lang="scss">
  .content {
    margin-right: 5px;
  }

  .meta-buttons {
    display: flex;
    margin-left: auto;
  }
</style>
