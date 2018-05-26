using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;

namespace RSCoreLib.WPF
    {
    public class DropDownButton : ToggleButton
        {
        public static readonly DependencyProperty DropDownProperty = DependencyProperty.Register("DropDown", typeof(ContextMenu), typeof(DropDownButton), new UIPropertyMetadata(null));

        public DropDownButton ()
            {
            Content = "…";
            VerticalAlignment = System.Windows.VerticalAlignment.Center;
            HorizontalAlignment = System.Windows.HorizontalAlignment.Center;
            Width = 20;
            Height = 20;
            Binding binding = new Binding("DropDown.IsOpen");
            binding.Source = this;
            this.SetBinding(IsCheckedProperty, binding);
            }

        public ContextMenu DropDown
            {
            get { return (ContextMenu)GetValue(DropDownProperty); }
            set { SetValue(DropDownProperty, value); }
            }

        protected override void OnClick ()
            {
            if (DropDown != null)
                {
                DropDown.PlacementTarget = this;
                DropDown.Placement = PlacementMode.Bottom;
                DropDown.IsOpen = true;
                }
            }
        }
    }
