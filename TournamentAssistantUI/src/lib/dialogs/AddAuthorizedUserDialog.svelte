<script lang="ts">
  import Dialog, { Header, Title, Content, Actions } from "@smui/dialog";
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

<Dialog bind:open scrimClickAction="" escapeKeyAction="">
  <Header>
    <Title>Add an Authorized User</Title>
  </Header>
  <Content>
    <LayoutGrid>
      {#if username && discordAvatarUrl}
        <Cell span={12}>
          <div class="min-size-cell">
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
          </div>
        </Cell>
      {/if}
      <Cell span={12}>
        <div class="min-size-cell">
          <Textfield
            value={discordId}
            on:input={debounceLookupDiscordInfo}
            variant="outlined"
            label={"Paste the user's discord ID"}
          />
          <div class="alternative-method-hint">
            Tired of adding each user one at a time? You can
            <a
              href="https://discord.com/oauth2/authorize?client_id=708801604719214643&permissions=0&integration_type=0&scope=bot"
              target="_blank"
            >
              add the TA discord bot
            </a>
            to your server to add users by their role (ie: add all users with the
            @Participant or @Coordinator role).
            <br />
            <br />
            You should use tournamentId:
            <div class="tournament-id-highlight">
              {tournamentId}
            </div>
          </div>
        </div>
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
  .min-size-cell {
    min-width: 400px;

    .alternative-method-hint {
      color: var(--mdc-theme-text-primary-on-background);
      background-color: rgba($color: #000000, $alpha: 0.1);
      border-radius: 2vmin;
      text-align: center;
      font-weight: 100;
      line-height: 1.1;
      padding: 2vmin;
      margin: 2vmin 2vmin 0vmin 2vmin;

      .tournament-id-highlight,
      a {
        color: var(--mdc-theme-primary);
      }
    }

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
        box-shadow: 5px 5px 5px rgba($color: #000000, $alpha: 0.2);

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
        box-shadow: 5px 5px 5px rgba($color: #000000, $alpha: 0.2);
      }
    }
  }
</style>
