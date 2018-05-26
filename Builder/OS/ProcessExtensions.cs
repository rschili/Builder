using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace RSCoreLib.OS
    {
    public static class ProcessNativeMethods
        {
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool AttachConsole (uint dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
        public static extern bool FreeConsole ();

        [DllImport("kernel32.dll")]
        public static extern bool SetConsoleCtrlHandler (ConsoleCtrlDelegate HandlerRoutine, bool Add);
        // Delegate type to be used as the Handler Routine for SCCH
        public delegate bool ConsoleCtrlDelegate (CtrlTypes CtrlType);

        // Enumerated type for the control messages sent to the handler routine
        public enum CtrlTypes : uint
            {
            CTRL_C_EVENT = 0,
            CTRL_BREAK_EVENT,
            CTRL_CLOSE_EVENT,
            CTRL_LOGOFF_EVENT = 5,
            CTRL_SHUTDOWN_EVENT
            }

        [DllImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GenerateConsoleCtrlEvent (CtrlTypes dwCtrlEvent, uint dwProcessGroupId);
        }

    public static class ProcessExtensions
        {
        public static void TerminateByInterrupt (this Process p, int timeout = 5000)
            {
            if (p.HasExited)
                return;//super nice

            p.StandardInput.Close();
            if (p.WaitForExit(200))
                return;//nice

            //This does not require the console window to be visible.
            if (ProcessNativeMethods.AttachConsole((uint)p.Id))
                {
                try
                    {
                    //Disable Ctrl-C handling for our program
                    if (ProcessNativeMethods.SetConsoleCtrlHandler(null, true))
                        {
                        try
                            {
                            ProcessNativeMethods.GenerateConsoleCtrlEvent(ProcessNativeMethods.CtrlTypes.CTRL_C_EVENT, 0);
                            ProcessNativeMethods.GenerateConsoleCtrlEvent(ProcessNativeMethods.CtrlTypes.CTRL_C_EVENT, 0);
                            ProcessNativeMethods.GenerateConsoleCtrlEvent(ProcessNativeMethods.CtrlTypes.CTRL_C_EVENT, 0);
                            //Must wait here. If we don't and re-enable Ctrl-C handling below too fast, we might terminate ourselves.
                            if (p.WaitForExit(timeout))
                                {
                                return;//okay, interrupt did the trick.
                                }

                            Log.Warning("Interrupting the process {0} did not work, terminating the process tree...", p.ProcessName);
                            }
                        finally
                            {
                            //Re-enable Ctrl-C handling or any subsequently started programs will inherit the disabled state.
                            ProcessNativeMethods.SetConsoleCtrlHandler(null, false);
                            }
                        }
                    }
                catch(Exception e)
                    {
                    Log.Error("Interrupt termination of process threw exception. {0}", e.ToString());
                    //continue to kill the process
                    }
                finally
                    {
                    ProcessNativeMethods.FreeConsole();
                    }
                }

            p.Taskkill();
            }

        public static bool Taskkill (this Process p, bool force = false, bool skipClosingStandardInput = false, int timeoutSeconds = 5)
            {
            if (p.HasExited)
                return true;//super nice

            try
                {
                if (!skipClosingStandardInput)
                    {
                    p.StandardInput.Close();
                    if (p.WaitForExit(1000))
                        {
                        return true;
                        }
                    }

                string args = $"{(force ? "/F " : string.Empty)}/T /PID {p.Id}";
                Log.Information($"Using Taskkill to shutdown process with id {p.Id}");
                ProcessStartInfo processStartInfo = new ProcessStartInfo("taskkill", args)
                    {
                    CreateNoWindow = true,
                    UseShellExecute = false
                    };

                var terminateProcess = Process.Start(processStartInfo);
                return p.WaitForExit(timeoutSeconds * 1000);
                }
            catch(Exception e)
                {
                Log.Error($"Interrupt termination of process threw exception. {e.Message}");
                return false;
                }
            }
        }
    }
