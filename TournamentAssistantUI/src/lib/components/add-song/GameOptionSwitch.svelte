<!-- 
    This component specifically handles switches which switch GameOptions on and
    off in the AddSong component. It's possible this will be acting on more than
    one result at a time (playlists), so it will show as Checked when at least
    one song has the GameOption enabled
-->

<script lang="ts">
  import Switch from "@smui/switch";
  import type {
    GameplayModifiers_GameOptions,
    GameplayParameters,
  } from "tournament-assistant-client";

  export let gameOption: GameplayModifiers_GameOptions;
  export let gameplayParameters: GameplayParameters[] | undefined;
  export let disabled: boolean | undefined = undefined;
</script>

{#if gameplayParameters}
  <Switch
    {disabled}
    checked={gameplayParameters.some(
      (x) =>
        x.gameplayModifiers &&
        (x.gameplayModifiers.options & gameOption) === gameOption,
    )}
    on:SMUISwitch:change={(e) => {
      if (gameplayParameters) {
        if (e.detail.selected) {
          gameplayParameters.forEach((x) => {
            if (x.gameplayModifiers) {
              x.gameplayModifiers.options |= gameOption;
            }
          });
        } else {
          gameplayParameters.forEach((x) => {
            if (x.gameplayModifiers) {
              x.gameplayModifiers.options &= ~gameOption;
            }
          });
        }
      }
    }}
  />
{/if}
