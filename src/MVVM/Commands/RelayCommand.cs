using System;
using System.Windows.Input;

namespace LogViewer.MVVM.Commands
{
    public class RelayCommand : ICommand
    {
        private readonly Action executeWithoutParam;
        private readonly Action<object> execute;
        private readonly Func<object, bool> canExecute;

        public event EventHandler CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }

        public RelayCommand(Action<object> execute, Func<object, bool> canExecute = null)
        {
            this.execute = execute;
            this.canExecute = canExecute;
        }

        public RelayCommand(Action executeAction)
        {
            this.executeWithoutParam = executeAction;
        }

        public bool CanExecute(object parameter)
        {
            return this.canExecute == null || this.canExecute(parameter);
        }

        public void Execute(object parameter = null)
        {
            if (parameter == null && executeWithoutParam != null)
                this.executeWithoutParam();
            else
                this.execute(parameter);
        }
    }
}
