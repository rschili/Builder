using System;
using System.Windows;

namespace RSCoreLib.OS
    {
    public static class ClipboardHelper
        {
        /// <summary>
        /// Clipboard implementation in both, WPF and Windows Forms is buggy. Sometimes it just fails and throws an exception
        /// because the clipboard is currently busy. We will retry 5 times until we give up.
        /// </summary>
        /// <param name="text"></param>
        public static void CopyToClipboard (string text)
            {
            string errorInfo = null;
            for (int i = 0; i < 5; i++)
                {
                try
                    {
                    Clipboard.SetText(text, TextDataFormat.UnicodeText);
                    return;
                    }
                catch (Exception e)
                    {
                    errorInfo = e.Message;
                    }
                }

            Log.Error("Failed to copy to clipboard. {0}", errorInfo);
            }
        }
    }
