<script lang="ts">
  import Dialog from "@smui/dialog";
  import Button, { Label } from "@smui/button";
  import CircularProgress from "@smui/circular-progress";
  import Textfield from "@smui/textfield";
  import Fab, { Icon } from "@smui/fab";
  import { taService } from "$lib/stores";
  import { Response_ResponseType } from "tournament-assistant-client";

  export let open = false;
  export let serverAddress: string;
  export let serverPort: string;
  export let onTokenCreated: () => Promise<void> = async () => {};

  let tokenText = "";
  let tokenName = "";
  let creatingToken = false;

  const onContinueClick = () => {
    open = false;
  };

  const onClipboardClick = async () => {
    try {
      await navigator.clipboard.writeText(tokenText);
      console.log("Text copied to clipboard successfully!");
    } catch (error) {
      console.error("Failed to copy text: ", error);
    }
  };

  const onCreateTokenClick = async () => {
    creatingToken = true;
    const response = await $taService.generateBotToken(
      serverAddress,
      serverPort,
      tokenName,
    );

    if (
      response.type === Response_ResponseType.Success &&
      response.details.oneofKind === "generateBotToken"
    ) {
      tokenText = response.details.generateBotToken.botToken;
    }

    await onTokenCreated();

    creatingToken = false;
  };
</script>

<Dialog bind:open scrimClickAction="" escapeKeyAction="">
  <div class="dialog-title">
    {#if tokenText}
      Please copy your token, you will not be able to see it again
    {:else}
      Enter a name for your new bot user
    {/if}
  </div>
  {#if tokenText}
    <div class="token-text">
      {tokenText}
    </div>
    <div class="action-buttons">
      <Button on:click={onClipboardClick}>
        <Label>Copy to Clipboard</Label>
      </Button>
      <Button on:click={onContinueClick}>
        <Label>Continue</Label>
      </Button>
    </div>
  {:else if creatingToken}
    <div class="progress">
      <CircularProgress style="height: 48px; width: 48px;" indeterminate />
    </div>
  {:else}
    <div class="input-container">
      <div class="name-input">
        <Textfield
          bind:value={tokenName}
          variant="outlined"
          label="Bot Username"
        />
      </div>
      <div class="create-button">
        <Fab color="primary" mini on:click={onCreateTokenClick}>
          <Icon class="material-icons">add</Icon>
        </Fab>
      </div>
    </div>

    <div class="action-buttons">
      <Button on:click={onContinueClick}>
        <Label>Cancel</Label>
      </Button>
    </div>
  {/if}
</Dialog>

<style lang="scss">
  .progress {
    text-align: center;
    min-height: 100px;
  }

  .dialog-title {
    color: var(--mdc-theme-text-primary-on-background);
    background-color: rgba($color: #000000, $alpha: 0.1);
    border-radius: 2vmin 2vmin 0 0;
    padding: 2vmin;
    margin: 15px 0 0 0;
    width: 70%;
    align-self: center;
    text-align: center;
    font-size: 2rem;
    font-weight: 100;
    line-height: 1.1;
  }

  .input-container {
    display: flex;
    place-content: center;
    align-items: center;
    width: -webkit-fill-available;
    width: -moz-available;
    margin: 10px 10px 0 10px;

    * {
      padding: 5px;
    }

    .name-input {
      width: -webkit-fill-available;
      width: -moz-available;
    }
  }

  .token-text {
    color: var(--mdc-theme-text-primary-on-background);
    background-color: rgba($color: #000000, $alpha: 0.1);
    border-radius: 2vmin;
    text-align: center;
    padding: 5px;
    margin: 5px;
    overflow-wrap: break-word;
  }

  .action-buttons {
    margin: 2vmin;
    text-align: right;
  }
</style>
