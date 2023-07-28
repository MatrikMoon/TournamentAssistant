<script lang="ts">
    import Dialog, { Header, Title, Content, Actions } from "@smui/dialog";
    import IconButton from "@smui/icon-button";
    import LayoutGrid, { Cell } from "@smui/layout-grid";
    import Button, { Label } from "@smui/button";
    import type { Team, Tournament } from "tournament-assistant-client";
    import Textfield from "@smui/textfield";
    import DataTable, {
        Head,
        Body,
        Row,
        Cell as TableCell,
    } from "@smui/data-table";
    import { v4 as uuidv4 } from "uuid";

    export let open = false;
    export let tournament: Tournament;
    export let onCreateClick = () => {};

    let newTeamName = "";

    const onAddTeamClick = () => {
        tournament.settings!.teams = [
            ...tournament.settings!.teams,
            { guid: uuidv4(), name: newTeamName },
        ];
    };

    const onDeleteTeamClick = (guid: string) => {
        tournament.settings!.teams = tournament.settings!.teams.filter(
            (x) => x.guid !== guid
        );
    };

    //Don't allow creation unless we have all the required fields
    let canCreateTournament = false;
    $: if (tournament.settings!.teams.length > 0) {
        canCreateTournament = true;
    }

    let canCreateTeam = false;
    $: if (newTeamName.length > 0) {
        canCreateTeam = true;
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
                <div class="add-team-control">
                    <Textfield
                        bind:value={newTeamName}
                        variant="outlined"
                        label="Team Name"
                    />
                    <Button on:click={onAddTeamClick}>
                        <Label>Add Team</Label>
                    </Button>
                </div>
            </Cell>
            <Cell span={12}>
                <DataTable
                    table$aria-label="People list"
                    style="max-width: 100%;"
                >
                    <Head>
                        <Row>
                            <TableCell>Team Name</TableCell>
                            <TableCell numeric>Delete</TableCell>
                        </Row>
                    </Head>
                    <Body>
                        {#each tournament.settings?.teams ?? [] as team}
                            <Row>
                                <TableCell>{team.name}</TableCell>
                                <TableCell numeric>
                                    <IconButton
                                        on:click={() =>
                                            onDeleteTeamClick(team.guid)}
                                        class="material-icons">close</IconButton
                                    >
                                </TableCell>
                            </Row>
                        {/each}
                    </Body>
                </DataTable>
            </Cell>
        </LayoutGrid>
    </Content>
    <Actions>
        <Button>
            <Label>Cancel</Label>
        </Button>
        <Button on:click={onCreateClick} disabled={!canCreateTournament}>
            <Label>Create</Label>
        </Button>
    </Actions>
</Dialog>

<style lang="scss">
    .add-team-control {
        display: flex;
        align-items: center;

        :global(> button) {
            margin-left: 1vmin;
            min-width: fit-content;
        }
    }
</style>
