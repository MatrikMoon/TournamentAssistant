<script lang="ts">
  import Dialog from "@smui/dialog";
  import Button, { Label } from "@smui/button";
  import List, { Item, Graphic, Text, SecondaryText } from "@smui/list";
  import {
    Push_SongFinished,
    Push_SongFinished_CompletionType,
  } from "tournament-assistant-client";
  import type { MapWithSongInfo } from "$lib/globalTypes";
  import { BeatSaverService } from "$lib/services/beatSaver/beatSaverService";

  export let open = false;
  export let results: Push_SongFinished[];
  export let mapWithSongInfo: MapWithSongInfo;

  $: resultsWithImages = results
    .filter((x) => {
      // This shouldn't happen, but just in case a result in the list
      // doesn't match the expected song, we'll throw it out

      return (
        x.beatmap?.levelId ===
        mapWithSongInfo.gameplayParameters?.beatmap?.levelId
      );
    })
    .map((x, index) => {
      const maxScore = BeatSaverService.getMaxScore(
        mapWithSongInfo.songInfo,
        x.beatmap?.characteristic?.serializedName ?? "Standard",
        BeatSaverService.getDifficultyAsString(x.beatmap?.difficulty ?? 4),
      );

      return {
        name:
          (x.player?.name.length ?? 0) > 0
            ? x.player?.name
            : x.player?.discordInfo?.username,
        score: x.score,
        percentage: (x.score / maxScore).toFixed(2),
        resultType: x.type,
        badgeKey:
          x.type === Push_SongFinished_CompletionType.Failed
            ? "failed"
            : index <= 3
              ? index
              : "other",
        badgeText:
          x.type === Push_SongFinished_CompletionType.Passed
            ? index
            : Push_SongFinished_CompletionType[x.type],
        image:
          x.player?.discordInfo?.avatarUrl ??
          `https://cdn.scoresaber.com/avatars/${x.player?.platformId}.jpg`,
      };
    });

  const onContinueClick = () => {
    open = false;
  };

  const onClipboardClick = async () => {
    let clipboardText = "Results:\n";

    for (const index in resultsWithImages) {
      clipboardText += `${index}: ${resultsWithImages[index].name} - ${resultsWithImages[index].score} (${resultsWithImages[index].percentage}%)`;
    }

    try {
      await navigator.clipboard.writeText(clipboardText);
      console.log("Text copied to clipboard successfully!");
    } catch (error) {
      console.error("Failed to copy text: ", error);
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
  <div class="results-title">Results</div>
  <div class="results-list">
    <List twoLine avatarList>
      {#each resultsWithImages as item}
        <Item>
          <Graphic
            style="background-image: url({item.image}); background-size: contain"
          />
          <Text>
            <div class="item-name">
              <div class={`placement-badge placement-badge-${item.badgeKey}`}>
                {item.badgeText}
              </div>
              {item.name}
            </div>
            <SecondaryText>
              {item.score} - {item.percentage}%
            </SecondaryText>
          </Text>
        </Item>
      {/each}
    </List>
  </div>
  <div class="action-buttons">
    <Button on:click={onClipboardClick}>
      <Label>Copy to Clipboard</Label>
    </Button>
    <Button on:click={onContinueClick}>
      <Label>Continue</Label>
    </Button>
  </div>
</Dialog>

<style lang="scss">
  .results-title {
    color: var(--mdc-theme-text-primary-on-background);
    background-color: rgba($color: #000000, $alpha: 0.1);
    border-radius: 2vmin 2vmin 0 0;
    padding: 2vmin;
    margin: 6vmin 0 0 0;
    width: 70%;
    align-self: center;
    text-align: center;
    font-size: 2rem;
    font-weight: 100;
    line-height: 1.1;
  }

  .results-list {
    color: var(--mdc-theme-text-primary-on-background);
    background-color: rgba($color: #000000, $alpha: 0.1);
    border-radius: 0 0 2vmin 2vmin;
    padding: 0 2vmin;
    width: 70%;
    align-self: center;

    .item-name {
      margin-top: 10px;
      display: flex;
      align-items: center;
    }
  }

  .action-buttons {
    margin: 2vmin;
    text-align: right;
  }

  .placement-badge {
    margin-right: 5px;
    padding: 2px 4px;
    border-radius: 5px;

    &-1 {
      background-color: rgba($color: gold, $alpha: 0.4);
    }

    &-2 {
      background-color: rgba($color: silver, $alpha: 0.4);
    }

    &-3 {
      background-color: rgba($color: orange, $alpha: 0.4);
    }

    &-failed {
      background-color: rgba($color: red, $alpha: 0.4);
    }

    &-other {
      background-color: rgba($color: gray, $alpha: 0.4);
    }
  }
</style>
