<script lang="ts">
  import Dialog from "@smui/dialog";
  import Button, { Label } from "@smui/button";
  import List, { Item, Graphic, Text, SecondaryText } from "@smui/list";
  import {
    Map,
    Push_SongFinished,
    Push_SongFinished_CompletionType,
  } from "tournament-assistant-client";
  export let open = false;
  export let results: Push_SongFinished[];

  $: resultsWithImages = results
    .sort((a, b) => b.score - a.score)
    .map((x, index) => {
      return {
        name:
          (x.player?.name.length ?? 0) > 0
            ? x.player?.name
            : x.player?.discordInfo?.username,
        score: x.score,
        misses: x.misses,
        badCuts: x.badCuts,
        goodCuts: x.goodCuts,
        endTime: Math.round((x.endTime + Number.EPSILON) * 100) / 100,
        percentage: (x.accuracy * 100).toFixed(2),
        resultType: x.type,
        badgeKey:
          x.type === Push_SongFinished_CompletionType.Failed
            ? "failed"
            : index + 1 <= 3 && x.type !== Push_SongFinished_CompletionType.Quit
              ? index + 1
              : "other",
        badgeText:
          x.type === Push_SongFinished_CompletionType.Passed
            ? index + 1
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
      clipboardText += `${Number(index) + 1}: ${resultsWithImages[index].name} - ${resultsWithImages[index].score} (${resultsWithImages[index].percentage}%) (End time: ${resultsWithImages[index].endTime})\n`;
    }

    try {
      await navigator.clipboard.writeText(clipboardText.trim());
      console.log("Text copied to clipboard successfully!");
    } catch (error) {
      console.error("Failed to copy text: ", error);
    }
  };
</script>

<Dialog fullscreen bind:open scrimClickAction="" escapeKeyAction="">
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
              {item.score} - {item.percentage}% - (End time: {item.endTime})
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
