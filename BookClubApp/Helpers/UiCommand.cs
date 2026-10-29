using System;
using System.Windows.Input;

namespace BookClubApp.Helpers {
    public sealed class UiCommand : ICommand {
        private readonly Action<object> action;
        public UiCommand(Action<object> action) { this.action = action; }
        public bool CanExecute(object parameter) { return true; }
        public void Execute(object parameter) { action(parameter); }
        public event EventHandler CanExecuteChanged { add { } remove { } }
    }
}
