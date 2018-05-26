using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace RSCoreLib.OS
    {
    public class CommandResult
        {
        public string Command { get; internal set; }
        public int ErrorCount { get; internal set; } = 0;
        public int ReturnCode { get; internal set; }
        public int RetrievedLinesOfOutput { get; internal set; }
        public bool Success => ErrorCount == 0 && ReturnCode == 0;

        public override string ToString ()
            {
            return $"Command: '{Command}'. Success={Success} (Errors: {ErrorCount}, ReturnCode: {ReturnCode}) Lines Retrieved: {RetrievedLinesOfOutput}";
            }
        }

    public enum SandboxStatus
        {
        Initializing,
        Idle,
        Busy,
        ShuttingDown,
        Exited
        }

    public enum ShellOutputType
        {
        Message,
        Error
        }

    public class ShellOutput
        {
        internal ShellOutput (string data, ShellOutputType type = ShellOutputType.Message)
            {
            Data = data;
            Type = type;
            }

        public string Data;
        public ShellOutputType Type;
        }

    public class CommandLineSandbox : IDisposable
        {
        private Process _process = new Process();
        private readonly BufferBlock<ShellOutput> _buffer = new BufferBlock<ShellOutput>();
        private readonly ActionBlock<ShellOutput> _actionBlock;
        private SandboxStatus _status = SandboxStatus.Initializing;

        public CommandLineSandbox (string startupWorkingDirectory = null)
            {
            var si = _process.StartInfo;
            si.FileName = "cmd";
            if (startupWorkingDirectory != null)
                si.WorkingDirectory = startupWorkingDirectory;

            si.UseShellExecute = false;
            si.RedirectStandardInput = true;
            si.RedirectStandardOutput = true;
            si.RedirectStandardError = true;
            si.CreateNoWindow = true;
            si.Arguments = "/k";

            _process.EnableRaisingEvents = true;
            _process.Exited += (s, e) =>
            {
                Dispose();
            };

            _process.Start();
            _process.ErrorDataReceived += (sender, e) => _buffer.Post(new ShellOutput(e.Data, ShellOutputType.Error));
            _process.BeginErrorReadLine();

            _process.OutputDataReceived += (sender, e) => _buffer.Post(new ShellOutput(e.Data));
            _process.BeginOutputReadLine();

            ExecutionDataflowBlockOptions options = new ExecutionDataflowBlockOptions()
                {
                SingleProducerConstrained = false
                };

            _actionBlock = new ActionBlock<ShellOutput>((Action<ShellOutput>)OutputReceived, options);

            DataflowLinkOptions flowOptions = new DataflowLinkOptions()
                {
                PropagateCompletion = true
                };

            _buffer.LinkTo(_actionBlock, flowOptions); //Don't remember the IDisposable, we do not need to unlink

            _status = SandboxStatus.Idle;
            }

        public Action<ShellOutput> OutputHandler { get; set; } = null;
        private const string END_OF_COMMAND_TOKEN = "--command-end-";
        private const string RESULT_OF_COMMAND_TOKEN = "--command-result:";
        private ManualResetEventSlim _endOfCommandEvent = new ManualResetEventSlim(false);
        private int _lastErrorLevel = 0;
        private int _errorCount = 0;
        private bool _awaitingResult = false;

        private void OutputReceived (ShellOutput so)
            {
            if (_status != SandboxStatus.Busy)
                return;

            if (string.IsNullOrEmpty(so.Data))
                return;

            if (so.Type == ShellOutputType.Error)
                _errorCount++;
            else if (so.Type == ShellOutputType.Message)
                {
                if (!_awaitingResult && so.Data == END_OF_COMMAND_TOKEN)
                    {
                    _awaitingResult = true;
                    _process.StandardInput.WriteLine("echo " + RESULT_OF_COMMAND_TOKEN + "%errorlevel%");
                    _process.StandardInput.Flush();
                    }
                else if (_awaitingResult && so.Data.StartsWith(RESULT_OF_COMMAND_TOKEN))
                    {
                    var errorLevel = so.Data.Substring(RESULT_OF_COMMAND_TOKEN.Length);
                    int errorLevelInt;
                    if (int.TryParse(errorLevel, out errorLevelInt))
                        {
                        _lastErrorLevel = errorLevelInt;
                        }
                    else
                        {
                        _lastErrorLevel = 9000;
                        }

                    SignalCompletionAfterDelay().SwallowAndLogExceptions();
                    }
                }

            OutputHandler?.Invoke(so);

            //otherwise just dispose of it.
            }

        private async Task SignalCompletionAfterDelay ()
            {
            //give the commandline some time to flush the output buffer. Should not be necessary, but let's be safe
            await Task.Delay(100);
            _endOfCommandEvent.Set();
            }

        public void Dispose ()
            {
            Dispose(false);
            }

        public void Dispose (bool useTaskkill)
            {
            lock (this)
                {
                if (_status == SandboxStatus.Exited || _status == SandboxStatus.ShuttingDown)
                    return;

                _status = SandboxStatus.ShuttingDown;
                }

            try
                {
                if (!_process.HasExited)
                    {
                    Log.Information("Terminating command line");
                    _process.CancelErrorRead();
                    _process.CancelOutputRead();
                    _process.Taskkill(false, useTaskkill, 5);
                    }

                //in case our command is still waiting
                _endOfCommandEvent.Set();
                _endOfCommandEvent.Dispose();
                _process.Dispose();
                _buffer.Complete();
                }
            catch (Exception e)
                {
                Log.Error("Error while shutting down process {0}: {1}", _process.ProcessName, e.Message);
                }

            _status = SandboxStatus.Exited;
            }

        public CommandResult ExecuteCommand (string command)
            {
            lock (this)
                {
                if (_status != SandboxStatus.Idle)
                    throw new InvalidOperationException("The current command line is not in idle state.");

                _status = SandboxStatus.Busy;
                }

            CommandResult result = new CommandResult() { Command = command };
            SendCommand(command);

            lock (this)
                {
                if (_status != SandboxStatus.Busy)
                    throw new TaskCanceledException("The process ended during command execution");

                result.ReturnCode = _lastErrorLevel;
                if (_lastErrorLevel != 0)
                    ResetErrorLevel();

                result.ErrorCount = _errorCount;
                _lastErrorLevel = 0;
                _errorCount = 0;
                _status = SandboxStatus.Idle;
                _awaitingResult = false;
                return result;
                }
            }

        private void ResetErrorLevel ()
            {
            _process.StandardInput.WriteLine("cmd /c \"exit /b0\"");
            _process.StandardInput.Flush();
            }

        private void SendCommand (string command)
            {
            _endOfCommandEvent.Reset();
            command = string.Concat(command, "&echo ", END_OF_COMMAND_TOKEN);
            _process.StandardInput.WriteLine(command);
            _process.StandardInput.Flush();
            _endOfCommandEvent.Wait();
            }
        }
    }
