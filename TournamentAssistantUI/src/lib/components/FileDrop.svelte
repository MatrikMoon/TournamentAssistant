<script lang="ts">
    import { Icon } from "@smui/button";
    import IconButton from "@smui/icon-button";
    import { filedrop } from "filedrop-svelte";
    import type { Files } from "filedrop-svelte/file";
    import type { FileDropOptions } from "filedrop-svelte/options";

    let files: Files;
    let options: FileDropOptions = {
        windowDrop: false,
        hideInput: true,
        multiple: false,
    };
</script>

<div
    class="dropzone"
    use:filedrop={options}
    on:filedrop={(e) => {
        files = e.detail.files;
    }}
    on:filedragenter={(filedragenter) => console.log({ filedragenter })}
    on:filedragleave={(filedragleave) => console.log({ filedragleave })}
    on:filedragover={(filedragover) => console.log({ filedragover })}
    on:filedialogcancel={(filedialogcancel) =>
        console.log({ filedialogcancel })}
    on:filedialogclose={(filedialogclose) => console.log({ filedialogclose })}
    on:filedialogopen={(filedialogopen) => console.log({ filedialogopen })}
    on:windowfiledragenter={(windowfiledragenter) =>
        console.log({ windowfiledragenter })}
    on:windowfiledragleave={(windowfiledragleave) =>
        console.log({ windowfiledragleave })}
    on:windowfiledragover={(windowfiledragover) =>
        console.log({ windowfiledragover })}
>
    <Icon class="material-icons">add</Icon>
    <div class="dropzone-label">Add an Image</div>
</div>

{#if files}
    <h3>Accepted files</h3>
    <ul>
        {#each files.accepted as file}
            <li>{file.name} - {file.size}</li>
        {/each}
    </ul>
    <h3>Rejected files</h3>
    <ul>
        {#each files.rejected as rejected}
            <li>
                {rejected.file.name} - {rejected.error.message}
            </li>
        {/each}
    </ul>
{/if}

<style lang="scss">
    .dropzone {
        display: flex;
        align-items: center;
        border: 1px solid var(--mdc-theme-text-secondary-on-background);
        border-radius: 5px;
        padding: 2vmin;

        .dropzone-label {
            padding-left: 1vmin;
            color: var(--mdc-theme-text-secondary-on-background);
        }

        :global(.material-icons) {
            color: var(--mdc-theme-text-secondary-on-background);
        }

        &:hover {
            border: 2px solid var(--mdc-theme-primary);
            padding: -1px;
            animation: shake 0.2s;
            animation-iteration-count: 1;
        }
    }

    @keyframes shake {
        0% {
            transform: translate(1px, 1px) rotate(0deg);
        }
        10% {
            transform: translate(-1px, -2px) rotate(-1deg);
        }
        20% {
            transform: translate(-3px, 0px) rotate(1deg);
        }
        30% {
            transform: translate(3px, 2px) rotate(0deg);
        }
        40% {
            transform: translate(1px, -1px) rotate(1deg);
        }
        50% {
            transform: translate(-1px, 2px) rotate(-1deg);
        }
        60% {
            transform: translate(-3px, 1px) rotate(0deg);
        }
        70% {
            transform: translate(3px, 1px) rotate(-1deg);
        }
        80% {
            transform: translate(-1px, -1px) rotate(1deg);
        }
        90% {
            transform: translate(1px, 2px) rotate(0deg);
        }
        100% {
            transform: translate(1px, -2px) rotate(-1deg);
        }
    }
</style>
