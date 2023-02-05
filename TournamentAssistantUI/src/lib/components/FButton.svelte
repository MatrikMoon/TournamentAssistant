<script lang="ts">
    export let text: string;
    export let buttonState: string;
</script>

<button class={`btn${buttonState}`}>
    {#if text !== undefined}
        <!-- Two spans. One has a shadow, and the shadowspan has .7 opacity to make the shadow mesh with the background better -->
        <!-- One has only text, and full opacity, so that the text doesn't also fade into the background with the shadow's opacity -->
        <div>
            <span>{text}</span>
            <span class="shadowSpan">{text}</span>
        </div>
    {/if}
</button>

<style lang="scss">
    //Shadow function
    @function shadow-string($color1, $color2, $length) {
        $total-length: $length;
        $string: $color1 0px 0px;
        @while ($length * 2) > 0 {
            $mix-amount: 100 - ((($length / 2) / $total-length) * 100);
            $mixed-color: mix($color1, $color2, $mix-amount);
            $string-addition: $length/2 + px $length/2 + px;
            $string: $mixed-color $string-addition, $string;
            $length: $length - 1;
        }
        @return $string;
    }

    .btn {
        //Font
        font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", "Roboto",
            "Oxygen", "Ubuntu", "Cantarell", "Fira Sans", "Droid Sans",
            "Helvetica Neue", sans-serif;

        //Traits
        position: relative;
        display: block;
        padding: 0;
        overflow: hidden;

        //Border and shaping
        border-width: 0;
        outline: none;
        border-radius: 0.4em;
        box-shadow: 1px 2px 4px rgba(0, 0, 0, 0.6);

        //Colors
        --background-color: #eee;
        --text-color: #969696;
        --hovered-text-color: #eee;
        --hovered-color: #ff6114;
        --hovered-secondary-color: #fc0254;
        background-color: var(--background-color);

        //Animation
        transition: 0.2s;

        &.hovered {
            transition: 0s;
            transform: scale(1.2);
            background-image: linear-gradient(
                to right,
                var(--hovered-color),
                var(--hovered-secondary-color)
            );

            & span {
                color: var(--hovered-text-color);

                &.shadowSpan {
                    text-shadow: shadow-string(
                        #555,
                        rgba(255, 255, 255, 0),
                        40
                    );
                }
            }
        }

        &.selected {
            transition: 0s;
            transform: scale(1.1);
            background-image: linear-gradient(
                to right,
                var(--selected-color),
                var(--selected-secondary-color)
            );

            & span {
                color: var(--selected-text-color);

                &.shadowSpan {
                    text-shadow: shadow-string(
                        #555,
                        rgba(255, 255, 255, 0),
                        40
                    );
                }
            }
        }

        //Text Formatting
        & span {
            display: block;
            position: absolute;
            padding: 6px 18px;
            color: var(--text-color);

            &.shadowSpan {
                opacity: 0.5;
                position: relative;
            }
        }

        & > * {
            position: relative;
        }
    }
</style>
