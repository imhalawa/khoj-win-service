using System;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.ServiceProcess;
using System.Timers;
using System.Runtime.InteropServices;

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
        //private Timer _timer;
        Process _process;
        public KhojBackgroundService()
        {
            InitializeComponent();

            if (System.Diagnostics.EventLog.SourceExists("Khoj"))
            {
                System.Diagnostics.EventLog.CreateEventSource("Khoj", ServiceName);
            }
            this.eventLogger.Source = "Khoj";
            this.eventLogger.Log = "KhojLog";

            //this._timer = new Timer();
            //_timer.Interval = 6000;
            //_timer.Elapsed += new ElapsedEventHandler(this.OnTimer);
            //_timer.Start();

        }

        //private void OnTimer(object sender, ElapsedEventArgs e)
        //{
        //    eventLogger.WriteEntry($"{ServiceName} Polling ...", EventLogEntryType.Information, eventId++);
        //}

        protected override void OnStart(string[] args)
        {
            // Update the service state to Start Pending.
            ServiceStatus serviceStatus = new ServiceStatus();
            serviceStatus.dwCurrentState = ServiceState.SERVICE_START_PENDING;
            serviceStatus.dwWaitHint = 100000;
            SetServiceStatus(this.ServiceHandle, ref serviceStatus);

            eventLogger.WriteEntry($"Starting {ServiceName}", EventLogEntryType.Information, eventId++);
            try
            {



                string commandPath = WhereSeach("Khoj.exe");
                string arguments = $"--no-gui {args}";

                _process = new Process();
                _process.StartInfo = new ProcessStartInfo(commandPath);
                _process.Start();
                _process.Start();

                eventLogger.WriteEntry($"{ServiceName} Started Successfully", EventLogEntryType.Information, eventId++);

                // Update the service state to Running.
                serviceStatus.dwCurrentState = ServiceState.SERVICE_RUNNING;
                SetServiceStatus(this.ServiceHandle, ref serviceStatus);
            }
            catch (Exception ex)
            {
                eventLogger.WriteEntry($"Error occurred on attempting to start {ServiceName}, {ex.Message}", EventLogEntryType.Error, eventId++);
                StopService();
            }
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
            ServiceStatus serviceStatus = new ServiceStatus();
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
}