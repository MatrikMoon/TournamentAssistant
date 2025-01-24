<script lang="ts">
    import type { MapWithSongInfo } from "$lib/globalTypes";
    import { BeatSaverService } from "$lib/services/beatSaver/beatSaverService";
    import { getBadgeTextFromDifficulty } from "$lib/utils";

    export let mapWithSongInfo: MapWithSongInfo | undefined = undefined;
</script>

{#if mapWithSongInfo}
    <div
        class="image"
        style="background-image: url({BeatSaverService.currentVersion(
            mapWithSongInfo.songInfo,
        )?.coverURL}); background-size: cover"
    />
    <div class="content">
        <h2 class="title">{mapWithSongInfo.songInfo.name}</h2>
        <h3 class="subtitle">
            {mapWithSongInfo.songInfo.metadata.songAuthorName}
        </h3>
        {#if mapWithSongInfo.gameplayParameters && mapWithSongInfo.gameplayParameters.beatmap?.difficulty !== undefined}
            <div
                class={`difficulty-badge difficulty-badge-${mapWithSongInfo.gameplayParameters?.beatmap?.difficulty}`}
            >
                {getBadgeTextFromDifficulty(
                    mapWithSongInfo.gameplayParameters?.beatmap?.difficulty,
                )}
            </div>
        {/if}
    </div>
{/if}

<style lang="scss">
    .image {
        height: 170px;
        width: -webkit-fill-available;
        width: -moz-available;
    }

    .content {
        padding: 15px;

        h2,
        h3 {
            margin: 0;
        }

        .title {
            color: var(--mdc-theme-text-primary-on-background);
        }

        .subtitle {
            color: var(--mdc-theme-text-secondary-on-background);
        }
    }

    .difficulty-badge {
        margin-right: 5px;
        padding: 2px 4px;
        border-radius: 5px;
        width: min-content;
        color: var(--mdc-theme-text-primary-on-background);

        &-0 {
            background-color: rgba($color: green, $alpha: 0.4);
        }

        &-1 {
            background-color: rgba($color: blue, $alpha: 0.4);
        }

        &-2 {
            background-color: rgba($color: orange, $alpha: 0.4);
        }

        &-3 {
            background-color: rgba($color: red, $alpha: 0.4);
        }

        &-4 {
            background-color: rgba($color: rgb(247, 0, 255), $alpha: 0.4);
        }
    }
</style>
