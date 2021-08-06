using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.ViewControllers;
using HMUI;
using System.Linq;
using TournamentAssistantShared;

namespace TournamentAssistant.ViewControllers
{
    [ViewDefinition("TournamentAssistant.Views.patch-notes-view.bsml")]
    [HotReload(RelativePathToLayout = @"..\Views\patch-notes-view.bsml")]
    internal class PatchNotesView : BSMLAutomaticViewController
    {
        [UIComponent("background")]
        protected readonly ImageView _imageView = null!;

        [UIComponent("patch-notes-text-page")]
        protected readonly TextPageScrollView _patchNotesTextPage = null!;

        protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
        {
            base.DidActivate(firstActivation, addedToHierarchy, screenSystemEnabling);

            if (firstActivation)
            {
                string[] notes = SharedConstructs.Changelog.Split('\n');
                string writeToBox = notes.Aggregate((a, b) => $"{b}\n{a}");
                _patchNotesTextPage.SetText(writeToBox);
                _imageView.color = _imageView.color.ColorWithAlpha(0.5f);
            }
        }
    }
}