<script lang="ts">
    import {
        GameplayModifiers_GameOptions,
        QualifierEvent,
        QualifierEvent_QualifierMap,
    } from "tournament-assistant-client";
    import List, {
        Item,
        Graphic,
        Meta,
        Text,
        PrimaryText,
        SecondaryText,
    } from "@smui/list";
    import { BeatSaverService } from "$lib/services/beatSaver/beatSaverService";
    import type { QualifierMapWithSongInfo } from "../../lib/globalTypes";

    export let qualifier: QualifierEvent = {
        guid: "",
        name: "",
        guild: {
            id: "0",
            name: "dummy",
        },
        infoChannel: {
            id: "0",
            name: "dummy",
        },
        qualifierMaps: [],
        flags: 0,
        sort: 0,
        image: new Uint8Array([1]),
    };
    export let qualifierMapsWithSongInfo: QualifierMapWithSongInfo[] = [];
    export let onRemoveClicked: (
        map: QualifierMapWithSongInfo,
    ) => Promise<void>;

    let downloadingCoverArtForMaps: QualifierEvent_QualifierMap[] = [];

    // This chaotic function handles the automatic downloading of cover art. Potentially worth revisiting...
    // It's called a number of times due to using both `qualifier` and `downloadingCoverArtForMaps` on the
    // right-hand side of assignments inside. It still manages to avoid spamming the BeatSaver api though,
    // so... Meh?
    $: {
        const updateCoverArt = async () => {
            // We don't want to spam the API with requests if we don't have to, so we'll reuse maps we already have
            let missingItems = qualifier.qualifierMaps.filter(
                (x) =>
                    qualifierMapsWithSongInfo.find(
                        (y) =>
                            y.gameplayParameters?.beatmap?.levelId ===
                            x.gameplayParameters?.beatmap?.levelId,
                    ) === undefined,
            );

            console.log({ missingItems });

            // This function may trigger rapidly, and includes an async action below, so if there's any currently
            // downloading cover art, we should ignore it and let the existing download finish
            missingItems = missingItems.filter(
                (x) =>
                    !downloadingCoverArtForMaps.find((y) => x.guid === y.guid),
            );

            // Now, we *are* going to download whatever's left, so we should go ahead and add it to the downloading list
            downloadingCoverArtForMaps = [
                ...downloadingCoverArtForMaps,
                ...missingItems,
            ];

            let addedItems: QualifierMapWithSongInfo[] = [];

            for (let item of missingItems) {
                const songInfo = await BeatSaverService.getSongInfoByHash(
                    item.gameplayParameters!.beatmap!.levelId.substring(
                        "custom_level_".length,
                    ),
                );

                if (songInfo) {
                    addedItems.push({
                        ...item,
                        songInfo,
                    });
                }
            }

            console.log({ addedItems });

            // Merge added items into qualifierMapsWithSongInfo while removing items that have also been removed from the qualifier model
            qualifierMapsWithSongInfo = [
                ...qualifierMapsWithSongInfo,
                ...addedItems,
            ].filter((x) =>
                qualifier.qualifierMaps.map((y) => y.guid).includes(x.guid),
            );

            // Remove the items that have downloaded from the in-progress list
            downloadingCoverArtForMaps = downloadingCoverArtForMaps.filter(
                (x) => !addedItems.find((y) => x.guid === y.guid),
            );
        };

        console.log("updateCoverArt");
        updateCoverArt();
    }

    function getSelectedEnumMembers<T extends Record<keyof T, number>>(
        enumType: T,
        value: number,
    ): Extract<keyof T, string>[] {
        function hasFlag(value: number, flag: number): boolean {
            return (value & flag) === flag;
        }

        const selectedMembers: Extract<keyof T, string>[] = [];
        for (const member in enumType) {
            if (hasFlag(value, enumType[member])) {
                selectedMembers.push(member);
            }
        }
        return selectedMembers;
    }

    function getBadgeTextFromDifficulty(difficulty: number) {
        switch (difficulty) {
            case 1:
                return "N";
            case 2:
                return "H";
            case 3:
                return "Ex";
            case 4:
                return "E+";
            default:
                return "E";
        }
    }
</script>

<List threeLine avatarList singleSelection>
    {#each qualifierMapsWithSongInfo as map}
        <Item class="preview-item">
            <Graphic
                style="background-image: url({BeatSaverService.currentVersion(
                    map.songInfo,
                )?.coverURL}); background-size: contain"
            />
            <Text>
                <div class="title-text">
                    <!-- more null checks -->
                    {#if map.gameplayParameters && map.gameplayParameters.beatmap?.difficulty !== undefined}
                        <div
                            class={`difficulty-badge difficulty-badge-${map.gameplayParameters?.beatmap?.difficulty}`}
                        >
                            {getBadgeTextFromDifficulty(
                                map.gameplayParameters?.beatmap?.difficulty,
                            )}
                        </div>
                    {/if}

                    {map.songInfo.name}
                </div>
                <SecondaryText>
                    {map.songInfo.metadata.levelAuthorName}
                </SecondaryText>
                <!-- more null checks -->
                {#if map.gameplayParameters && map.gameplayParameters.gameplayModifiers}
                    <SecondaryText>
                        {map.attempts > 0
                            ? `${map.attempts} attempts - `
                            : ""}{map.disablePause
                            ? "Disable Pause - "
                            : ""}{getSelectedEnumMembers(
                            GameplayModifiers_GameOptions,
                            map.gameplayParameters.gameplayModifiers.options,
                        )
                            .filter(
                                (x) =>
                                    x !==
                                    GameplayModifiers_GameOptions[
                                        GameplayModifiers_GameOptions.None
                                    ],
                            )
                            .map((x) => `${x}`)
                            .join(" - ")}
                    </SecondaryText>
                {/if}
            </Text>
            <Meta class="material-icons" on:click={() => onRemoveClicked(map)}>
                close
            </Meta>
        </Item>
    {/each}
</List>

<style lang="scss">
    .title-text {
        margin-top: 10px;
        display: flex;
        align-items: center;

        .difficulty-badge {
            margin-right: 5px;
            padding: 2px 4px;
            border-radius: 5px;

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
    }
</style>
