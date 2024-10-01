<script lang="ts">
  import Dialog from "@smui/dialog";
  import { type GameplayParameters } from "tournament-assistant-client";
  import type { SongInfo } from "$lib/services/beatSaver/songInfo";
  import EditSong from "$lib/components/add-song/EditSong.svelte";

  export let showMatchOnlyOptions = true;
  export let showQualifierOnlyOptions = true;
  export let showTargetTextbox = false;
  export let gameplayParameters: GameplayParameters | undefined = undefined;
  export let songInfoList: SongInfo | undefined = undefined;
  export let addingPlaylistOrPool = false;
  export let onSongUpdated = (result: GameplayParameters) => {};

  export let open = false;
</script>

<Dialog bind:open slot="over" scrimClickAction="" escapeKeyAction="">
  <EditSong
    edit={true}
    {showMatchOnlyOptions}
    {showQualifierOnlyOptions}
    {showTargetTextbox}
    gameplayParameters={gameplayParameters
      ? [gameplayParameters]
      : gameplayParameters}
    songInfoList={songInfoList ? [songInfoList] : songInfoList}
    {addingPlaylistOrPool}
    onSongsAdded={(result) => {
      // Edit mode should only ever have one song edited at a time
      onSongUpdated(result[0]);
      open = false;
    }}
  />
</Dialog>
