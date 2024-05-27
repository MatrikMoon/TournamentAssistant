<script lang="ts">
    import Textfield from "@smui/textfield";
    import FileDrop from "./FileDrop.svelte";

    export let hint: string;
    export let name = "";
    export let img: Uint8Array | undefined;
    export let onNameUpdated: () => void = () => {};
    export let onImageUpdated: () => void = () => {};

    const resizeImage = async (
        file: File,
        maxWidth: number,
        maxHeight: number,
    ): Promise<Blob | null> => {
        return new Promise((resolve, reject) => {
            let image = new Image();
            image.src = URL.createObjectURL(file);
            image.onload = () => {
                let width = image.width;
                let height = image.height;

                if (width <= maxWidth && height <= maxHeight) {
                    resolve(file);
                }

                let newWidth;
                let newHeight;

                if (width > height) {
                    newHeight = height * (maxWidth / width);
                    newWidth = maxWidth;
                } else {
                    newWidth = width * (maxHeight / height);
                    newHeight = maxHeight;
                }

                let canvas = document.createElement("canvas");
                canvas.width = newWidth;
                canvas.height = newHeight;

                let context = canvas.getContext("2d");

                context!.drawImage(image, 0, 0, newWidth, newHeight);

                canvas.toBlob(resolve, file.type);
            };
            image.onerror = reject;
        });
    };

    const _onNameUpdated = (event: any) => {
        name = (event.target as HTMLInputElement)?.value;
        onNameUpdated();
    };

    const _onImageUpdated = async (file: File) => {
        const blob = await resizeImage(file, 200, 200);
        img = new Uint8Array(await blob!.arrayBuffer());
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
