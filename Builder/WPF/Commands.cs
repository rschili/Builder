using System;
using System.Threading.Tasks;
using System.Windows.Input;

namespace RSCoreLib.WPF
    {
    /// <summary>
    /// Dummy command to use as fallback value, when no command is available
    /// </summary>
    public class NullCommand : ICommand
        {
        public bool CanExecute (object parameter)
            {
            return false;
            }

        public event EventHandler CanExecuteChanged
            {
            add
                {
                }
            remove
                {
                }
            }

        public void Execute (object parameter)
            {
            throw new NotImplementedException("NullCommand cannot be executed.");
            }
        }

    public class SimpleCommand : ICommand
        {
        public Action<object> Handler { get; set; }
        public Func<object, bool?> CanExecuteHandler { get; set; }
        private bool m_enabled = false;
        public bool Enabled
            {
            get
                {
                return m_enabled;
                }
            set
                {
                if (value == m_enabled)
                    return;

                m_enabled = value;
                CommandManager.InvalidateRequerySuggested();
                }
            }

        public event EventHandler CanExecuteChanged
            {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
            }

        public void Execute(object parameter = null)
            {
            if (!CanExecute(parameter))
                return;

            Handler?.Invoke(parameter);
            }

        public bool CanExecute(object parameter = null)
            {
            if (!m_enabled)
                return false;

            var canExecute = CanExecuteHandler?.Invoke(parameter);

            //either no handler (enabled) or use value returned by handler
            return !canExecute.HasValue || canExecute.Value;
            }

        public SimpleCommand(bool enable = true)
            {
            m_enabled = enable;
            }
        }
    }
