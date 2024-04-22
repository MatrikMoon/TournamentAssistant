<script lang="ts">
    import Textfield from "@smui/textfield";
    import FileDrop from "./FileDrop.svelte";

    export let hint: string;
    export let name = "";
    export let img: Uint8Array | undefined;
    export let onNameUpdated: () => void = () => {};
    export let onImageUpdated: () => void = () => {};

    const _onNameUpdated = (event: any) => {
        name = (event.target as HTMLInputElement)?.value;
        onNameUpdated();
    };

    const _onImageUpdated = async (file: File) => {
        img = new Uint8Array(await file.arrayBuffer());
        onImageUpdated();
    };
</script>

<div class="name-and-image">
    <div class="image">
        <FileDrop onFileSelected={_onImageUpdated} {img} />
    </div>
    <div class="name">
        <Textfield
            value={name}
            on:input={_onNameUpdated}
            variant="outlined"
            label={hint}
        />
    </div>
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
