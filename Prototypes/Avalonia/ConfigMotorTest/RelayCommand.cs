using System;
using System.Threading.Tasks;
using System.Windows.Input;

namespace MissionPlanner.Prototypes.Avalonia.ConfigMotorTest
{
    // Minimal ICommand implementations so the ViewModel can expose bindable commands
    // without pulling in a full MVVM toolkit for this sketch. Avalonia's {Binding X}
    // on a Button.Command requires an ICommand-typed property - binding directly to a
    // plain async method (as an earlier draft of this ViewModel did) does not work.

    public class RelayCommand : ICommand
    {
        private readonly Action<object> _execute;
        private readonly Func<object, bool> _canExecute;

        public RelayCommand(Action<object> execute, Func<object, bool> canExecute = null)
        {
            _execute = execute;
            _canExecute = canExecute;
        }

        public event EventHandler CanExecuteChanged;
        public bool CanExecute(object parameter) => _canExecute?.Invoke(parameter) ?? true;
        public void Execute(object parameter) => _execute(parameter);
        public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }

    public class AsyncRelayCommand : ICommand
    {
        private readonly Func<object, Task> _execute;
        private readonly Func<object, bool> _canExecute;
        private bool _isExecuting;

        public AsyncRelayCommand(Func<object, Task> execute, Func<object, bool> canExecute = null)
        {
            _execute = execute;
            _canExecute = canExecute;
        }

        public event EventHandler CanExecuteChanged;

        public bool CanExecute(object parameter) =>
            !_isExecuting && (_canExecute?.Invoke(parameter) ?? true);

        public async void Execute(object parameter)
        {
            _isExecuting = true;
            RaiseCanExecuteChanged();
            try
            {
                await _execute(parameter);
            }
            finally
            {
                _isExecuting = false;
                RaiseCanExecuteChanged();
            }
        }

        public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
