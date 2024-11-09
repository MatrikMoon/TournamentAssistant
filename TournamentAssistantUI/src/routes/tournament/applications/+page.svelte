<script lang="ts">
  import Fab, { Icon, Label } from "@smui/fab";
  import TaDrawer from "$lib/components/TADrawer.svelte";
  import {
    Response_GetBotTokensForUser_BotUser,
    Response_ResponseType,
  } from "tournament-assistant-client";
  import { taService } from "$lib/stores";
  import BotTokenList from "$lib/components/BotTokenList.svelte";
  import NewTokenDialog from "$lib/dialogs/NewTokenDialog.svelte";
  import { page } from "$app/stores";
  import { onMount } from "svelte";
  import { getUserIdFromToken } from "$lib/services/jwtService";
  import { authToken } from "$lib/stores";

  let serverAddress = $page.url.searchParams.get("address")!;
  let serverPort = $page.url.searchParams.get("port")!;

  let tokenDialogOpen = false;
  let botUsers: Response_GetBotTokensForUser_BotUser[] = [];

  const onCopyClicked = async (
    botUser: Response_GetBotTokensForUser_BotUser,
  ) => {
    try {
      await navigator.clipboard.writeText(botUser.guid);
      console.log("Text copied to clipboard successfully!");
    } catch (error) {
      console.error("Failed to copy text: ", error);
    }
  };

  const onRemoveClicked = async (
    botUser: Response_GetBotTokensForUser_BotUser,
  ) => {
    const response = await $taService.revokeBotToken(
      serverAddress,
      serverPort,
      botUser.guid,
    );

    if (
      response.type === Response_ResponseType.Success &&
      response.details.oneofKind === "revokeBotToken"
    ) {
      await refreshList();
    }
  };

  const onTokenCreated = async () => {
    await refreshList();
  };

  const refreshList = async () => {
    const response = await $taService.getBotTokensForUser(
      serverAddress,
      serverPort,
      getUserIdFromToken($authToken),
    );

    if (
      response.type === Response_ResponseType.Success &&
      response.details.oneofKind === "getBotTokensForUser"
    ) {
      botUsers = response.details.getBotTokensForUser.botUsers;
    }
  };

  onMount(refreshList);
</script>

<NewTokenDialog
  bind:open={tokenDialogOpen}
  {serverAddress}
  {serverPort}
  {onTokenCreated}
/>
<div class="list-title">Pick a tournament</div>

<div class="token-list">
  <BotTokenList {botUsers} {onRemoveClicked} {onCopyClicked} />
</div>

<div class="create-token-button-container">
  <Fab
    color="primary"
    on:click={() => {
      tokenDialogOpen = true;
    }}
    extended
  >
    <Icon class="material-icons">add</Icon>
    <Label>Create New Token</Label>
  </Fab>
</div>

<style lang="scss">
  .list-title {
    color: var(--mdc-theme-text-primary-on-background);
    background-color: rgba($color: #000000, $alpha: 0.1);
    border-radius: 2vmin;
    text-align: center;
    font-size: 2rem;
    font-weight: 100;
    line-height: 1.1;
    padding: 2vmin;
  }

  .token-list {
    background-color: rgba($color: #000000, $alpha: 0.1);
    border-radius: 2vmin;
    width: fit-content;
    text-align: -webkit-center;
    overflow-y: auto;
    max-height: 70vh;
    margin: 2vmin auto;
  }

  .create-token-button-container {
    position: fixed;
    bottom: 2vmin;
    right: 2vmin;
  }
</style>
