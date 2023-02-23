<script lang="ts">
    import { Icon } from "@smui/button";
    import { filedrop } from "filedrop-svelte";
    import type { Files } from "filedrop-svelte/file";
    import type { FileDropOptions } from "filedrop-svelte/options";

    let timer: NodeJS.Timeout | undefined;
    let hoveredWithFile = false;
    $: dropzoneClass = hoveredWithFile ? " hovered-with-file" : "";

    //Only debounce going from hovered to not hovered
    const debounceHoveredWithFile = (newValue: boolean) => {
        if (!hoveredWithFile) {
            hoveredWithFile = newValue;
        } else {
            clearTimeout(timer);
            timer = setTimeout(() => {
                hoveredWithFile = newValue;
            }, 100);
        }
    };

    let files: Files;
    let options: FileDropOptions = {
        windowDrop: false,
        hideInput: true,
        multiple: false,
    };
</script>

<div
    class="dropzone{dropzoneClass}"
    use:filedrop={options}
    on:filedrop={(e) => {
        files = e.detail.files;
    }}
    on:filedragenter={(filedragenter) => {
        debounceHoveredWithFile(true);
    }}
    on:filedragleave={(filedragleave) => {
        debounceHoveredWithFile(false);
    }}
    on:filedragover={(filedragover) => {
        debounceHoveredWithFile(true);
    }}
    on:filedrop={(filedrop) => {
        debounceHoveredWithFile(false);
    }}
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
        cursor: default;
        display: flex;
        align-items: center;
        border: 1px solid var(--mdc-theme-text-secondary-on-background);
        border-radius: 5px;
        padding: 2vmin;
        background: var(--background-color);

        //Transition back from hovered if it was hovered
        transform: scale(1, 1);

        //Hover color transition time
        transition: 0.3s;

        .dropzone-label {
            padding-left: 1vmin;
            color: var(--mdc-theme-text-secondary-on-background);
        }

        :global(.material-icons) {
            color: var(--mdc-theme-text-secondary-on-background);
        }

        &:hover {
            background: var(--background-color-shaded);
            transition: 0.2s;
        }
    }

    .hovered-with-file {
        background: var(--mdc-theme-primary-shaded);
        border: 1px dashed var(--mdc-theme-text-primary-on-background);
        animation: grow 0.5s ease forwards;

        .dropzone-label {
            padding-left: 1vmin;
            color: var(--mdc-theme-text-primary-on-background);
        }

        :global(.material-icons) {
            color: var(--mdc-theme-text-primary-on-background);
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

    @keyframes grow {
        from {
            transform: scale(1, 1);
        }

        to {
            transform: scale(1.2, 1.2);
        }
    }

    @keyframes shrink {
        to {
            transform: scale(1, 1);
        }

        from {
            transform: scale(1.2, 1.2);
        }
    }
</style>
