<script lang="ts">
    import Textfield from "@smui/textfield";
    import FileDrop from "./FileDrop.svelte";

    export let hint: string;
    export let name = "";
    export let img: Uint8Array | undefined;
    export let onUpdated: () => void = () => {};

    const onImageUpdated = async (file: File) => {
        img = new Uint8Array(await file.arrayBuffer());
        onUpdated();
    };

    const onNameUpdated = (event: any) => {
        name = (event.target as HTMLInputElement)?.value;
        onUpdated();
    };
</script>

<div class="name-and-image">
    <div class="image">
        <FileDrop onFileSelected={onImageUpdated} {img} />
    </div>
    <div class="name">
        <Textfield
            value={name}
            on:input={onNameUpdated}
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
