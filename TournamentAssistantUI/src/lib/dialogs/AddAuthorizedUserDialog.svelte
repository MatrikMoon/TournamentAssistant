<script lang="ts">
  import Dialog, { Header, Title, Content, Actions } from "@smui/dialog";
  import IconButton from "@smui/icon-button";
  import LayoutGrid, { Cell } from "@smui/layout-grid";
  import Button, { Label } from "@smui/button";
  import {
    Permissions,
    Response_ResponseType,
  } from "tournament-assistant-client";
  import Textfield from "@smui/textfield";
  import { taService } from "$lib/stores";
  import FormField from "@smui/form-field";
  import Switch from "@smui/switch";

  export let serverAddress: string;
  export let serverPort: string;
  export let tournamentId: string;
  export let open = false;
  export let onAddClick = (discordId: string, permissions: Permissions) => {};

  let nameUpdateTimer: NodeJS.Timeout | undefined;
  let username: string = "";
  let discordAvatarUrl: string = "";
  let discordId: string = "";
  let permissions: Permissions = Permissions.None;

  // Don't allow creation unless we have all the required fields
  $: canCreate = (discordId?.length ?? 0) > 0;

  const debounceLookupDiscordInfo = (event: any) => {
    discordId = (event.target as HTMLInputElement)?.value;
    if (discordId?.length >= 15) {
      clearTimeout(nameUpdateTimer);
      nameUpdateTimer = setTimeout(async () => {
        const result = await $taService.getDiscordInfo(
          serverAddress,
          serverPort,
          tournamentId,
          discordId,
        );

        if (
          result.type === Response_ResponseType.Success &&
          result.details.oneofKind === "getDiscordInfo"
        ) {
          username = result.details.getDiscordInfo.discordUsername;
          discordAvatarUrl = result.details.getDiscordInfo.discordAvatarUrl;
        }
      }, 500);
    }
  };
</script>

<Dialog
  bind:open
  fullscreen
  scrimClickAction=""
  escapeKeyAction=""
  aria-labelledby="fullscreen-title"
  aria-describedby="fullscreen-content"
>
  <Header>
    <Title>Add an authorized user</Title>
    <IconButton action="cancel" class="material-icons">close</IconButton>
  </Header>
  <Content>
    <LayoutGrid>
      {#if username && discordAvatarUrl}
        <Cell span={8}>
          <div class="preview-container">
            <div class="preview">
              <img alt="" class={"avatar-image"} src={discordAvatarUrl} />
              <span class="username">{username}</span>
            </div>

            <div class="permission-select">
              {#each Object.keys(Permissions) as permissionType}
                {#if Number(permissionType) >= 0 && Number(permissionType) !== Permissions.None}
                  <FormField>
                    <Switch
                      checked={(permissions & Number(permissionType)) ===
                        Number(permissionType)}
                      on:SMUISwitch:change={(e) => {
                        if (e.detail.selected) {
                          permissions |= Number(permissionType);
                        } else {
                          permissions &= ~Number(permissionType);
                        }
                      }}
                    />
                    <span slot="label">
                      {Permissions[Number(permissionType)]}
                    </span>
                  </FormField>
                {/if}
              {/each}
            </div>
          </div>
        </Cell>
      {/if}
      <Cell span={8}>
        <Textfield
          value={discordId}
          on:input={debounceLookupDiscordInfo}
          variant="outlined"
          label={"Paste the user's discord ID"}
        />
      </Cell>
    </LayoutGrid>
  </Content>
  <Actions>
    <Button>
      <Label>Cancel</Label>
    </Button>
    <Button
      on:click={() => onAddClick(discordId, permissions)}
      disabled={!canCreate}
    >
      <Label>Create</Label>
    </Button>
  </Actions>
</Dialog>

<style lang="scss">
  .preview-container {
    display: flex;
    justify-content: center;

    .preview {
      display: flex;
      align-self: center;
      align-items: center;
      width: fit-content;
      height: fit-content;
      padding: 0 2vmin;
      border-radius: 5px;
      background-color: rgba($color: #000000, $alpha: 0.1);

      .avatar-image {
        width: 55px;
        height: 55px;
        border-radius: 50%;
        margin: 1vmin;
        object-fit: cover;
      }

      .username {
        margin-left: 5px;
      }
    }

    .permission-select {
      display: flex;
      flex-direction: column;
      padding: 2vmin;
      margin: 2vmin;
      border-radius: 5px;
      background-color: rgba($color: #000000, $alpha: 0.1);
    }
  }
</style>
