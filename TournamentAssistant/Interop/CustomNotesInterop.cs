namespace TournamentAssistant.Interop
{
    static class CustomNotesInterop
    {
        public static void EnableHMDOnly()
        {
            CustomNotes.Utilities.LayerUtils.EnableHMDOnly();
        }

        public static void DisableHMDOnly()
        {
            CustomNotes.Utilities.LayerUtils.DisableHMDOnly();
        }
    }
}
