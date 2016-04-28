using System;
using System.Collections.ObjectModel;
using System.Data.SQLite;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using log4net;
using RSCoreLib.WPF;

namespace Builder
    {
    public class OutputVM : ViewModelBase
        {
        private static readonly ILog log = LogManager.GetLogger(typeof(OutputVM));
        public readonly MainVM Parent;
        private StreamWriter _logWriter = null;
        public event EventHandler<string> OutputReceived;
        public event EventHandler OutputCleared;

        public OutputVM (MainVM parent)
            {
            Parent = parent;
            }

        private StringBuilder _outputCache = new StringBuilder();
        private string _currentLogPath = null;

        public string GetCurrentBufferedOutput()
            {
            lock(this)
                {
                return _outputCache.ToString();
                }
            }

        public void SendOutput(string line)
            {
            if (string.IsNullOrEmpty(line))
                return;

            Task.Run(() =>
            {
                OutputReceived?.Invoke(this, line);

                lock (this)
                    {
                    _outputCache.AppendLine(line);
                    if (_logWriter != null)
                        _logWriter.WriteLine(line);
                    }
            });
            }

        public void StartWritingLog(string fullPath)
            {
            try
                {
                if (Parent?.SettingsVM?.Buildlogs != true)
                    return;

                lock (this)
                    {
                    EndWritingLog();
                    var dir = Path.GetDirectoryName(fullPath);
                    if (!Directory.Exists(dir))
                        Directory.CreateDirectory(dir);

                    _currentLogPath = fullPath;
                    _logWriter = new StreamWriter(fullPath, false, Encoding.UTF8);
                    }
                }
            catch(Exception e)
                {
                log.Warn($"Failed to start writing to log file {fullPath}", e);
                }
            }

        public void EndWritingLog ()
            {
            lock (this)
                {
                if(_logWriter != null)
                    {
                    _logWriter.Dispose();
                    _logWriter = null;

                    //Does not seem to work, files are not getting compressed.
                    /*if (_currentLogPath != null && File.Exists(_currentLogPath))
                        {
                        File.SetAttributes(_currentLogPath,
                            File.GetAttributes(_currentLogPath) | FileAttributes.Compressed);
                        }*/

                    _currentLogPath = null;
                    }
                }
            }

        public void Cls()
            {
            lock (this)
                {
                EndWritingLog();
                _outputCache.Clear();
                OutputCleared?.Invoke(this, EventArgs.Empty);
                }

            if (Parent?.SettingsVM?.ShowOutputOnBuild == true)
                {
                Parent?.ShowOutputCommand.Execute(this);
                }
            }
        }
    }
