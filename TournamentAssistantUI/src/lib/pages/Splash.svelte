<script lang="ts">
    import logo from "../assets/icon.png";
    import LinearProgress from "@smui/linear-progress";
    import { ConnectState, connectState, connectStateText } from "../stores";
</script>

<div class="splash">
    <img src={logo} alt="Logo" class="logo" />
    <div
        class={$connectState !== ConnectState.NotStarted
            ? ""
            : "loadingEllipses"}
    >
        {$connectStateText}
    </div>
    <LinearProgress
        indeterminate
        closed={$connectState !== ConnectState.NotStarted &&
            $connectState !== ConnectState.Connecting}
    />
</div>

<style lang="scss">
    .splash {
        text-align: center;
        padding: 1em;
        margin: 0 auto;

        div {
            color: var(--mdc-theme-text-primary-on-background);
            font-size: 2rem;
            font-weight: 100;
            line-height: 1.1;
            margin: 2rem auto;
        }

        .logo {
            height: 16rem;
            width: 16rem;
        }

        .loadingEllipses {
            &:after {
                overflow: hidden;
                display: inline-block;
                vertical-align: bottom;
                -webkit-animation: ellipsis steps(4, end) 900ms infinite;
                animation: ellipsis steps(4, end) 900ms infinite;
                content: "\2026"; /* ascii code for the ellipsis character */
                width: 0px;

                @keyframes ellipsis {
                    to {
                        width: 1.25em;
                    }
                }

                @-webkit-keyframes ellipsis {
                    to {
                        width: 1.25em;
                    }
                }
            }
        }
    }
</style>
