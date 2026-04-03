<script lang="ts">
  import Dialog from "@smui/dialog";
  import Button, { Label } from "@smui/button";
  import List, { Item, Graphic, Text, SecondaryText } from "@smui/list";
  import { Push_SongFinished, Push_SongFinished_CompletionType, Tournament_TournamentSettings_Team } from "tournament-assistant-client";

  export let open = false;
  export let results: Push_SongFinished[];
  export let teams: Tournament_TournamentSettings_Team[];

  function getTeamInfo(result: Push_SongFinished): Tournament_TournamentSettings_Team {
    const team = teams.find((x) => x.guid === result.player?.teamId);

    if (!team) {
      return {
        guid: "no-team",
        name: "No Team",
        image: "",
      };
    }
    return team;
  }

  $: sortedResults = [...results].sort((a, b) => b.score - a.score);

  $: resultsWithImages = sortedResults.map((x, index) => {
    const team = getTeamInfo(x);

    return {
      teamId: team.guid,
      teamName: team.name,
      name: (x.player?.name?.length ?? 0) > 0 ? x.player?.name : (x.player?.discordInfo?.username ?? "Unknown Player"),
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
      badgeText: x.type === Push_SongFinished_CompletionType.Passed ? index + 1 : Push_SongFinished_CompletionType[x.type],
      image: x.player?.discordInfo?.avatarUrl ?? `https://cdn.scoresaber.com/avatars/${x.player?.platformId}.jpg`,
    };
  });

  $: teamResults = Object.values(
    resultsWithImages.reduce(
      (acc, item) => {
        if (!acc[item.teamId]) {
          acc[item.teamId] = {
            teamId: item.teamId,
            teamName: item.teamName,
            totalScore: 0,
            players: [],
          };
        }

        acc[item.teamId].players.push(item);
        acc[item.teamId].totalScore += item.score;

        return acc;
      },
      {} as Record<
        string,
        {
          teamId: string;
          teamName: string;
          totalScore: number;
          players: typeof resultsWithImages;
        }
      >,
    ),
  ).sort((a, b) => b.totalScore - a.totalScore);

  const onContinueClick = () => {
    open = false;
  };

  const onClipboardClick = async () => {
    const lines: string[] = ["Results:", ""];

    for (const [teamIndex, team] of teamResults.entries()) {
      lines.push(`${teamIndex + 1}. ${team.teamName}`);
      lines.push(`   Total Score: ${team.totalScore}`);

      const sortedPlayers = [...team.players].sort((a, b) => b.score - a.score);

      for (const [playerIndex, player] of sortedPlayers.entries()) {
        const status =
          player.resultType === Push_SongFinished_CompletionType.Passed ? "" : ` [${Push_SongFinished_CompletionType[player.resultType]}]`;

        lines.push(`   ${playerIndex + 1}. ${player.name} - ${player.score} (${player.percentage}%) - End time: ${player.endTime}${status}`);
      }

      lines.push("");
    }

    try {
      await navigator.clipboard.writeText(lines.join("\n").trim());
      console.log("Text copied to clipboard successfully!");
    } catch (error) {
      console.error("Failed to copy text: ", error);
    }
  };
</script>

<Dialog fullscreen bind:open scrimClickAction="" escapeKeyAction="">
  <div class="results-title">Results</div>

  <div class="results-columns">
    {#each teamResults as team}
      <div class="team-column">
        <div class="team-header">
          <div class="team-name">{team.teamName}</div>
          <div class="team-score">Total: {team.totalScore}</div>
        </div>

        <div class="results-list">
          <List twoLine avatarList>
            {#each team.players as item}
              <Item>
                <Graphic style="background-image: url({item.image}); background-size: contain" />
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
      </div>
    {/each}
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
    width: 85%;
    align-self: center;
    text-align: center;
    font-size: 2rem;
    font-weight: 100;
    line-height: 1.1;
  }

  .results-columns {
    width: 85%;
    align-self: center;
    display: grid;
    grid-template-columns: repeat(auto-fit, minmax(320px, 1fr));
    gap: 2vmin;
    margin-top: 0;
  }

  .team-column {
    display: flex;
    flex-direction: column;
    min-width: 0;
  }

  .team-header {
    color: var(--mdc-theme-text-primary-on-background);
    background-color: rgba($color: #000000, $alpha: 0.1);
    border-radius: 0;
    padding: 1.5vmin 2vmin;
    text-align: center;

    .team-name {
      font-size: 1.4rem;
      font-weight: 500;
    }

    .team-score {
      margin-top: 0.4rem;
      font-size: 1rem;
      opacity: 0.9;
    }
  }

  .results-list {
    color: var(--mdc-theme-text-primary-on-background);
    background-color: rgba($color: #000000, $alpha: 0.1);
    border-radius: 0 0 2vmin 2vmin;
    padding: 0 2vmin;

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
