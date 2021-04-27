using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TournamentAssistantUI.ViewModels
{
    public class LoadingDialogViewModel : ViewModelBase
    {
        private string _LoadingText;
        private int _Progress;
        private bool _Indeterminate;
        public string LoadingText
        {
            get => _LoadingText;
            set => this.RaiseAndSetIfChanged(ref _LoadingText, value);
        }
        public int Progress
        {
            get => _Progress;
            set => this.RaiseAndSetIfChanged(ref _Progress, value);
        }
        public bool Indeterminate
        {
            get => _Indeterminate;
            set => this.RaiseAndSetIfChanged(ref _Indeterminate, value);
        }
        public LoadingDialogViewModel()
        {
        }
    }
}
