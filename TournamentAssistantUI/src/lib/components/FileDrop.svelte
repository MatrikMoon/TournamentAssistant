<script lang="ts">
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
    <div class="dropzone-expanding-background">
        <IconButton class="dropzone-button material-icons">add</IconButton>
    </div>
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
        width: fit-content;
        display: flex;
        align-items: center;
        margin-left: -2vmin;

        .dropzone-label {
            //padding-left: 1vmin;
        }

        .dropzone-expanding-background {
            //border: 3px solid var(--background-shaded);
            border-radius: 100%;
            padding: 2vmin;

            :global(.dropzone-button) {
                border-radius: 100%;
                background-color: var(--background-shaded);
            }
        }
    }
</style>
