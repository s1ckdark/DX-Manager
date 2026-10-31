using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Threading;
using Microsoft.Win32.SafeHandles;

namespace DexManager.Services
{
    internal sealed class SingleInstanceActivationService : IDisposable
    {
        private static readonly byte[] Command = Encoding.ASCII.GetBytes("open");
        private readonly object _sync = new object();
        private readonly string _pipeName;
        private NamedPipeServerStream _server;
        private bool _disposed;
        private bool _listening;

        public SingleInstanceActivationService(string instanceName)
        {
            _pipeName = PipeName(instanceName);
        }

        public event EventHandler ActivationRequested;

        private static string PipeName(string instanceName)
        {
            return instanceName + "-" + WindowsIdentity.GetCurrent().User.Value + "-" +
                Process.GetCurrentProcess().SessionId;
        }

        public void StartListening()
        {
            lock (_sync)
            {
                if (_disposed || _listening) return;
                _listening = true;
                new Thread(Listen) { IsBackground = true, Name = "DX Manager activation" }.Start();
            }
        }

        public static bool SignalPrimary(string instanceName)
        {
            try
            {
                using (var client = new NamedPipeClientStream(".", PipeName(instanceName), PipeDirection.Out))
                {
                    client.Connect(2000);
                    uint processId;
                    if (NativeMethods.GetNamedPipeServerProcessId(client.SafePipeHandle, out processId))
                        NativeMethods.AllowSetForegroundWindow(processId);
                    client.Write(Command, 0, Command.Length);
                    client.Flush();
                    return true;
                }
            }
            catch (IOException) { return false; }
            catch (TimeoutException) { return false; }
            catch (UnauthorizedAccessException) { return false; }
        }

        private void Listen()
        {
            while (true)
            {
                NamedPipeServerStream server = null;
                try
                {
                    var security = new PipeSecurity();
                    security.SetAccessRuleProtection(true, false);
                    security.AddAccessRule(new PipeAccessRule(WindowsIdentity.GetCurrent().User,
                        PipeAccessRights.FullControl, AccessControlType.Allow));
                    server = new NamedPipeServerStream(_pipeName, PipeDirection.In, 1,
                        PipeTransmissionMode.Byte, PipeOptions.Asynchronous, 64, 0, security);
                    lock (_sync)
                    {
                        if (_disposed) return;
                        _server = server;
                    }
                    server.WaitForConnection();
                    using (var deadline = new Timer(delegate { ClosePipe(server); }, null, 1000, Timeout.Infinite))
                    {
                        var received = new byte[Command.Length];
                        var count = 0;
                        while (count < received.Length)
                        {
                            var read = server.Read(received, count, received.Length - count);
                            if (read == 0) break;
                            count += read;
                        }
                        deadline.Change(Timeout.Infinite, Timeout.Infinite);
                        if (count == Command.Length && Encoding.ASCII.GetString(received) == "open")
                        {
                            EventHandler handler;
                            lock (_sync) handler = _disposed ? null : ActivationRequested;
                            if (handler != null) handler(this, EventArgs.Empty);
                        }
                    }
                }
                catch (IOException) { Thread.Sleep(50); }
                catch (ObjectDisposedException) { }
                catch (UnauthorizedAccessException) { Thread.Sleep(50); }
                finally
                {
                    lock (_sync) if (ReferenceEquals(_server, server)) _server = null;
                    ClosePipe(server);
                }
                lock (_sync) if (_disposed) return;
            }
        }

        private static void ClosePipe(NamedPipeServerStream server)
        {
            if (server == null) return;
            try { server.Dispose(); } catch (IOException) { }
        }

        public void Dispose()
        {
            NamedPipeServerStream server;
            lock (_sync)
            {
                if (_disposed) return;
                _disposed = true;
                server = _server;
                _server = null;
            }
            ClosePipe(server);
        }

        private static class NativeMethods
        {
            [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
            [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
            internal static extern bool GetNamedPipeServerProcessId(SafePipeHandle pipe, out uint processId);
            [System.Runtime.InteropServices.DllImport("user32.dll")]
            [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
            internal static extern bool AllowSetForegroundWindow(uint processId);
        }
    }
}
