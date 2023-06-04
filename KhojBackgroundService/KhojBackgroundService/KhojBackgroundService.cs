using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.Threading;
using System.Timers;

namespace KhojBackgroundService
{
    public enum ServiceState
    {
        SERVICE_STOPPED = 0x00000001,
        SERVICE_START_PENDING = 0x00000002,
        SERVICE_STOP_PENDING = 0x00000003,
        SERVICE_RUNNING = 0x00000004,
        SERVICE_CONTINUE_PENDING = 0x00000005,
        SERVICE_PAUSE_PENDING = 0x00000006,
        SERVICE_PAUSED = 0x00000007,
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct ServiceStatus
    {
        public int dwServiceType;
        public ServiceState dwCurrentState;
        public int dwControlsAccepted;
        public int dwWin32ExitCode;
        public int dwServiceSpecificExitCode;
        public int dwCheckPoint;
        public int dwWaitHint;
    };

    public partial class KhojBackgroundService : ServiceBase
    {
        private int eventId = 1;
        private Process _process;
        private ServiceStatus serviceStatus;

        public KhojBackgroundService()
        {
            InitializeComponent();

            if (!EventLog.SourceExists("Khoj"))
            {
                EventLog.CreateEventSource("Khoj", ServiceName);
            }

            this.eventLogger.Source = "Khoj";
            this.eventLogger.Log = "KhojLog";
        }

        private void OnTimer(object sender, ElapsedEventArgs e)
        {
            eventLogger.WriteEntry($"{ServiceName} Polling ...", EventLogEntryType.Information, eventId++);
        }

        protected override void OnStart(string[] args)
        {
            // Service Polling
            var _timer = new System.Timers.Timer();
            _timer.Interval = 6000;
            _timer.Elapsed += this.OnTimer;
            _timer.Start();

            // Update the service state to Start Pending.
            serviceStatus = new ServiceStatus();
            serviceStatus.dwCurrentState = ServiceState.SERVICE_START_PENDING;
            serviceStatus.dwWaitHint = 100000;
            SetServiceStatus(this.ServiceHandle, ref serviceStatus);

            eventLogger.WriteEntry($"Starting {ServiceName}", EventLogEntryType.Information, eventId++);

            Thread th = new Thread(new ParameterizedThreadStart(RunKhojServerCommand));
            th.Start(new KhojThreadParameters() { Args = args });
        }

        private void RunKhojServerCommand(object parameters)
        {
            var args = parameters as KhojThreadParameters;

            try
            {
                string commandPath = WhereSeach("Khoj");
                string arguments = "--no-gui";

                eventLogger.WriteEntry($"Command line {commandPath} is about to start", EventLogEntryType.Information, eventId++);

                _process = new Process()
                {
                    StartInfo = new ProcessStartInfo(commandPath),
                    EnableRaisingEvents = true,
                };
                _process.StartInfo.Arguments = arguments;
                _process.StartInfo.UseShellExecute = true;
                _process.StartInfo.CreateNoWindow = true;

                // Attach the event handler for the Exited event
                _process.Exited += ProcessExited;

                // Start the child process
                _process.Start();

                // Update the service state to Running.
                serviceStatus.dwCurrentState = ServiceState.SERVICE_RUNNING;
                SetServiceStatus(this.ServiceHandle, ref serviceStatus);

                eventLogger.WriteEntry($"{ServiceName} Started Successfully", EventLogEntryType.Information, eventId++);
            }
            catch (Exception ex)
            {
                eventLogger.WriteEntry($"Error occurred on attempting to start {ServiceName}, {ex.Message}", EventLogEntryType.Error, eventId++);
                StopService();
            }
        }

        private void ProcessExited(object sender, EventArgs e)
        {
            // This event handler is called when the child process has exited

            // Perform any required actions or retrieve information after the process has exited

            // Update the service state to Stopped.
            serviceStatus.dwCurrentState = ServiceState.SERVICE_STOPPED;
            SetServiceStatus(this.ServiceHandle, ref serviceStatus);
        }


        private void ChildProcess_Exited(object sender, EventArgs e)
        {
            eventLogger.WriteEntry($"{ServiceName} Child Process Exited", EventLogEntryType.Information, eventId++);
            StopService();
        }

        protected override void OnStop()
        {
            StopService();
        }

        private string WhereSeach(string fileName)
        {
            var paths = new[] { Environment.CurrentDirectory }.Concat(Environment.GetEnvironmentVariable("PATH").Split(';'));
            var extensions = new[] { string.Empty }.Concat(Environment.GetEnvironmentVariable("PATHEXT").Split(';')).Where(e => e.StartsWith("."));
            var combinations = paths.SelectMany(x => extensions, (path, extension) => Path.Combine(path, fileName + extension));
            return combinations.FirstOrDefault(File.Exists);
        }

        private void StopService()
        {
            // Update the service state to Stop Pending.
            serviceStatus.dwCurrentState = ServiceState.SERVICE_STOP_PENDING;
            serviceStatus.dwWaitHint = 100000;
            SetServiceStatus(this.ServiceHandle, ref serviceStatus);

            if (_process != null)
            {
                try
                {
                    eventLogger.WriteEntry($"Stopping {ServiceName}", EventLogEntryType.Information, eventId++);
                    _process.Close();
                    eventLogger.WriteEntry($"{ServiceName} Stopped Successfully", EventLogEntryType.Information, eventId++);
                }
                catch (Exception ex)
                {
                    eventLogger.WriteEntry($"Error occurred on attempting to start {ServiceName}, {ex.Message}", EventLogEntryType.Error, eventId++);
                    _process.Kill();
                }
                finally
                {
                    // Update the service state to Stopped.
                    serviceStatus.dwCurrentState = ServiceState.SERVICE_STOPPED;
                    SetServiceStatus(this.ServiceHandle, ref serviceStatus);
                }
            }
        }

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool SetServiceStatus(System.IntPtr handle, ref ServiceStatus serviceStatus);
    }


    internal class KhojThreadParameters
    {
        public string[] Args { get; set; }
    }
}