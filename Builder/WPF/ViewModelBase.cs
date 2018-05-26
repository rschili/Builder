using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace RSCoreLib.WPF
    {
    /// <summary>
    /// Basic class that wraps the INotifyPropertyChanged implementation
    /// </summary>
    public class ViewModelBase : INotifyPropertyChanged
        {
        public event PropertyChangedEventHandler m_propertyChanged;
        public event PropertyChangedEventHandler PropertyChanged
            {
            add { m_propertyChanged += value; }
            remove { m_propertyChanged -= value; }
            }

        protected virtual void OnPropertyChanged ([CallerMemberName] string propertyName = "")
            {
            PropertyChangedEventHandler handler = this.m_propertyChanged;
            if (handler != null)
                {
                var e = new PropertyChangedEventArgs(propertyName);
                handler(this, e);
                }
            }
        }
    }
