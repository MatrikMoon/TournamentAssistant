<script lang="ts">
    import Dialog, { Header, Title, Content, Actions } from "@smui/dialog";
    import IconButton from "@smui/icon-button";
    import LayoutGrid, { Cell } from "@smui/layout-grid";
    import Button, { Label } from "@smui/button";
    import FormField from "@smui/form-field";
    import Switch from "@smui/switch";
    import type { Team, Tournament } from "tournament-assistant-client";

    export let open = false;
    export let tournament: Tournament;

    let teams: Team[] = [];

    //Don't allow creation unless we have all the required fields
    let canCreate = false;
    $: if (teams.length > 0) {
        canCreate = true;
    }
</script>

<Dialog
    bind:open
    fullscreen
    scrimClickAction=""
    escapeKeyAction=""
    aria-labelledby="fullscreen-title"
    aria-describedby="fullscreen-content"
>
    <Header>
        <Title>Add teams to your Tournament</Title>
        <IconButton class="material-icons">close</IconButton>
    </Header>
    <Content>
        <LayoutGrid>
            <Cell span={12}>
                <Button>
                    <Label>Test</Label>
                </Button>
            </Cell>
        </LayoutGrid>
    </Content>
    <Actions>
        <Button>
            <Label>Cancel</Label>
        </Button>
        <Button disabled={!canCreate}>
            <Label>Create</Label>
        </Button>
    </Actions>
</Dialog>
