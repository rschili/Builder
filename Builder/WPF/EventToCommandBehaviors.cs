using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace RSCoreLib.WPF
    {
    public class SelectedItemChangedEventToCommandBehavior
        {
        public static DependencyProperty CommandProperty = DependencyProperty.RegisterAttached("Command", typeof(ICommand), typeof(SelectedItemChangedEventToCommandBehavior), new UIPropertyMetadata(CommandChanged));

        public static void SetCommand (DependencyObject target, ICommand value)
            {
            target.SetValue(CommandProperty, value);
            }

        private static void CommandChanged (DependencyObject target, DependencyPropertyChangedEventArgs e)
            {
            var treeView = target as TreeView;
            if (treeView != null)
                {
                if ((e.NewValue != null) && (e.OldValue == null))
                    {
                    treeView.SelectedItemChanged += SelectedItemChanged;
                    }
                else if ((e.NewValue == null) && (e.OldValue != null))
                    {
                    treeView.SelectedItemChanged -= SelectedItemChanged;
                    }

                return;
                }

            var listBox = target as ListBox;
            if (listBox != null)
                {
                if ((e.NewValue != null) && (e.OldValue == null))
                    {
                    listBox.SelectionChanged += SelectionChanged;
                    }
                else if ((e.NewValue == null) && (e.OldValue != null))
                    {
                    listBox.SelectionChanged -= SelectionChanged;
                    }
                }
            }

        private static void SelectionChanged (object sender, SelectionChangedEventArgs e)
            {
            Control control = sender as Control;
            ICommand command = (ICommand)control.GetValue(CommandProperty);
            object commandParameter = ((ListBox)sender).SelectedItem;
            command.Execute(commandParameter);
            }

        private static void SelectedItemChanged (object sender, RoutedPropertyChangedEventArgs<object> e)
            {
            Control control = sender as Control;
            ICommand command = (ICommand)control.GetValue(CommandProperty);
            object commandParameter = e.NewValue;
            command.Execute(commandParameter);
            }
        }

    public static class UpdateBehavior
        {
        public static readonly DependencyProperty UpdatePropertySourceWhenEnterPressedProperty = DependencyProperty.RegisterAttached(
            "UpdatePropertySourceWhenEnterPressed", typeof(DependencyProperty), typeof(UpdateBehavior), new PropertyMetadata(null, OnUpdatePropertySourceWhenEnterPressedPropertyChanged));

        static UpdateBehavior ()
            {

            }

        public static void SetUpdatePropertySourceWhenEnterPressed (DependencyObject dp, DependencyProperty value)
            {
            dp.SetValue(UpdatePropertySourceWhenEnterPressedProperty, value);
            }

        public static DependencyProperty GetUpdatePropertySourceWhenEnterPressed (DependencyObject dp)
            {
            return (DependencyProperty)dp.GetValue(UpdatePropertySourceWhenEnterPressedProperty);
            }

        private static void OnUpdatePropertySourceWhenEnterPressedPropertyChanged (DependencyObject dp, DependencyPropertyChangedEventArgs e)
            {
            UIElement element = dp as UIElement;

            if (element == null)
                {
                return;
                }

            if (e.OldValue != null)
                {
                element.PreviewKeyDown -= HandlePreviewKeyDown;
                }

            if (e.NewValue != null)
                {
                element.PreviewKeyDown += new KeyEventHandler(HandlePreviewKeyDown);
                }
            }

        static void HandlePreviewKeyDown (object sender, KeyEventArgs e)
            {
            if (e.Key == Key.Enter || e.Key == Key.Return)
                {
                DoUpdateSource(e.Source);
                }
            }

        static void DoUpdateSource (object source)
            {
            DependencyProperty property =
                GetUpdatePropertySourceWhenEnterPressed(source as DependencyObject);

            if (property == null)
                {
                return;
                }

            UIElement elt = source as UIElement;

            if (elt == null)
                {
                return;
                }

            BindingExpression binding = BindingOperations.GetBindingExpression(elt, property);

            if (binding != null)
                {
                binding.UpdateSource();
                }
            }
        }
    }
