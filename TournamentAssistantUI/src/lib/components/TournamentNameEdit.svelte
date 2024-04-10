<script lang="ts">
    import Textfield from "@smui/textfield";
    import FileDrop from "./FileDrop.svelte";
    import type { Tournament } from "tournament-assistant-client";

    export let tournament: Tournament;
    export let onUpdated: () => void = () => {};
</script>

<div class="name-and-image">
    <div class="image">
        <FileDrop
            onFileSelected={async (file) => {
                if (tournament?.settings) {
                    tournament.settings.tournamentImage = new Uint8Array(
                        await file.arrayBuffer(),
                    );

                    onUpdated();
                }
            }}
            img={tournament?.settings?.tournamentImage}
        />
    </div>
    {#if tournament.settings}
        <div class="name">
            <Textfield
                bind:value={tournament.settings.tournamentName}
                on:input={onUpdated}
                variant="outlined"
                label="Tournament Name"
            />
        </div>
    {/if}
</div>

<style lang="scss">
    .name-and-image {
        display: flex;

        .image {
            margin-right: 1vmin;
        }
        .name {
            width: -webkit-fill-available;
        }
    }
</style>
