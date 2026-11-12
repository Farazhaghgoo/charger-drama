using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ChargerDrama
{
    internal enum DeviceKind { UsbDrive, Mouse }

    internal sealed class DeviceEventArgs : EventArgs
    {
        public DeviceKind Kind { get; private set; }
        public bool Arrived { get; private set; }
        public string Detail { get; private set; }

        public DeviceEventArgs(DeviceKind kind, bool arrived, string detail)
        {
            Kind = kind;
            Arrived = arrived;
            Detail = detail;
        }
    }

    internal sealed class DeviceWatcher : NativeWindow, IDisposable
    {
        private const int WM_DEVICECHANGE = 0x0219;
        private const int WM_POWERBROADCAST = 0x0218;
        private const int DBT_DEVICEARRIVAL = 0x8000;
        private const int DBT_DEVICEREMOVECOMPLETE = 0x8004;
        private const int DBT_DEVTYP_VOLUME = 2;
        private const int DBT_DEVTYP_DEVICEINTERFACE = 5;
        private const int DBTF_MEDIA = 1;
        private const int DBTF_NET = 2;
        private const int DEVICE_NOTIFY_WINDOW_HANDLE = 0;
        private const int PBT_APMRESUMESUSPEND = 0x7;
        private const int PBT_APMRESUMEAUTOMATIC = 0x12;

        private const long StartupSuppressMs = 3000;
        private const long ResumeSuppressMs = 15000;
        private const long DebounceMs = 1500;

        private static readonly Guid GuidMouse = new Guid("378DE44C-56EF-11D1-BC8C-00A0C91405DD");

        [StructLayout(LayoutKind.Sequential)]
        private struct DEV_BROADCAST_HDR
        {
            public int dbch_size;
            public int dbch_devicetype;
            public int dbch_reserved;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DEV_BROADCAST_VOLUME
        {
            public int dbch_size;
            public int dbch_devicetype;
            public int dbch_reserved;
            public int dbcv_unitmask;
            public ushort dbcv_flags;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct DEV_BROADCAST_DEVICEINTERFACE
        {
            public int dbcc_size;
            public int dbcc_devicetype;
            public int dbcc_reserved;
            public Guid dbcc_classguid;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 1)]
            public string dbcc_name;
        }

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr RegisterDeviceNotification(IntPtr hRecipient, IntPtr notificationFilter, int flags);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnregisterDeviceNotification(IntPtr handle);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        public event EventHandler<DeviceEventArgs> DeviceChanged;
        public event EventHandler Resumed;

        private readonly Stopwatch clock = Stopwatch.StartNew();
        private long suppressUntilMs = StartupSuppressMs;
        private readonly long[] lastRaised = new long[4];
        private IntPtr notifyHandle = IntPtr.Zero;
        private bool bypassSuppression;
        private bool disposed;

        public DeviceWatcher()
        {
            for (int i = 0; i < lastRaised.Length; i++) lastRaised[i] = long.MinValue;

            CreateParams cp = new CreateParams();
            cp.Caption = "ChargerDrama.DeviceWatcher";
            cp.Style = unchecked((int)0x80000000); // WS_POPUP, not visible
            cp.ExStyle = 0x80;                      // WS_EX_TOOLWINDOW
            cp.Parent = IntPtr.Zero;                // top-level (NOT HWND_MESSAGE)
            CreateHandle(cp);

            IntPtr buf = IntPtr.Zero;
            try
            {
                DEV_BROADCAST_DEVICEINTERFACE filter = new DEV_BROADCAST_DEVICEINTERFACE();
                filter.dbcc_size = Marshal.SizeOf(typeof(DEV_BROADCAST_DEVICEINTERFACE));
                filter.dbcc_devicetype = DBT_DEVTYP_DEVICEINTERFACE;
                filter.dbcc_reserved = 0;
                filter.dbcc_classguid = GuidMouse;
                filter.dbcc_name = "";
                buf = Marshal.AllocHGlobal(filter.dbcc_size);
                Marshal.StructureToPtr(filter, buf, false);
                notifyHandle = RegisterDeviceNotification(Handle, buf, DEVICE_NOTIFY_WINDOW_HANDLE);
                Marshal.DestroyStructure(buf, typeof(DEV_BROADCAST_DEVICEINTERFACE));
            }
            catch (Exception)
            {
                notifyHandle = IntPtr.Zero;
            }
            finally
            {
                if (buf != IntPtr.Zero) Marshal.FreeHGlobal(buf);
            }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            try
            {
                if (notifyHandle != IntPtr.Zero)
                {
                    UnregisterDeviceNotification(notifyHandle);
                    notifyHandle = IntPtr.Zero;
                }
            }
            catch (Exception) { }
            try
            {
                if (Handle != IntPtr.Zero) DestroyHandle();
            }
            catch (Exception) { }
        }

        protected override void WndProc(ref Message m)
        {
            try
            {
                if (m.Msg == WM_DEVICECHANGE)
                    OnDeviceChange(m.WParam, m.LParam);
                else if (m.Msg == WM_POWERBROADCAST)
                    OnPower(m.WParam);
            }
            catch (Exception) { }
            base.WndProc(ref m);
        }

        private void OnPower(IntPtr wParam)
        {
            int w = wParam.ToInt32();
            if (w == PBT_APMRESUMEAUTOMATIC || w == PBT_APMRESUMESUSPEND)
            {
                suppressUntilMs = clock.ElapsedMilliseconds + ResumeSuppressMs;
                EventHandler h = Resumed;
                if (h != null) h(this, EventArgs.Empty);
            }
        }

        private void OnDeviceChange(IntPtr wParam, IntPtr lParam)
        {
            int w = wParam.ToInt32();
            if (w != DBT_DEVICEARRIVAL && w != DBT_DEVICEREMOVECOMPLETE) return;
            if (lParam == IntPtr.Zero) return;
            bool arrived = (w == DBT_DEVICEARRIVAL);

            DEV_BROADCAST_HDR hdr = (DEV_BROADCAST_HDR)Marshal.PtrToStructure(lParam, typeof(DEV_BROADCAST_HDR));
            if (hdr.dbch_devicetype == DBT_DEVTYP_VOLUME)
            {
                DEV_BROADCAST_VOLUME vol = (DEV_BROADCAST_VOLUME)Marshal.PtrToStructure(lParam, typeof(DEV_BROADCAST_VOLUME));
                if ((vol.dbcv_flags & DBTF_MEDIA) != 0 || (vol.dbcv_flags & DBTF_NET) != 0) return;
                string letters = "";
                for (int i = 0; i < 26; i++)
                {
                    if ((vol.dbcv_unitmask & (1 << i)) != 0)
                    {
                        if (letters.Length > 0) letters += " ";
                        letters += ((char)('A' + i)).ToString() + ":";
                    }
                }
                if (letters.Length == 0) return;
                Raise(DeviceKind.UsbDrive, arrived, letters);
            }
            else if (hdr.dbch_devicetype == DBT_DEVTYP_DEVICEINTERFACE)
            {
                DEV_BROADCAST_DEVICEINTERFACE di = (DEV_BROADCAST_DEVICEINTERFACE)Marshal.PtrToStructure(lParam, typeof(DEV_BROADCAST_DEVICEINTERFACE));
                if (di.dbcc_classguid != GuidMouse) return;
                IntPtr namePtr = new IntPtr(lParam.ToInt64() + Marshal.OffsetOf(typeof(DEV_BROADCAST_DEVICEINTERFACE), "dbcc_name").ToInt64());
                string name = Marshal.PtrToStringUni(namePtr);
                Raise(DeviceKind.Mouse, arrived, name ?? "");
            }
        }

        private void Raise(DeviceKind kind, bool arrived, string detail)
        {
            long now = clock.ElapsedMilliseconds;
            if (!bypassSuppression && now < suppressUntilMs) return;
            int idx = ((int)kind) * 2 + (arrived ? 1 : 0);
            if (lastRaised[idx] != long.MinValue && now - lastRaised[idx] < DebounceMs) return;
            lastRaised[idx] = now;
            EventHandler<DeviceEventArgs> h = DeviceChanged;
            if (h != null) h(this, new DeviceEventArgs(kind, arrived, detail));
        }

        public void SimulateVolume(bool arrival, char driveLetter)
        {
            if (Handle == IntPtr.Zero) return;
            char c = char.ToUpperInvariant(driveLetter);
            if (c < 'A' || c > 'Z') return;
            DEV_BROADCAST_VOLUME vol = new DEV_BROADCAST_VOLUME();
            vol.dbch_size = Marshal.SizeOf(typeof(DEV_BROADCAST_VOLUME));
            vol.dbch_devicetype = DBT_DEVTYP_VOLUME;
            vol.dbch_reserved = 0;
            vol.dbcv_unitmask = 1 << (c - 'A');
            vol.dbcv_flags = 0;
            IntPtr buf = Marshal.AllocHGlobal(vol.dbch_size);
            try
            {
                Marshal.StructureToPtr(vol, buf, false);
                SendSim(arrival, buf);
            }
            finally { Marshal.FreeHGlobal(buf); }
        }

        public void SimulateMouse(bool arrival)
        {
            if (Handle == IntPtr.Zero) return;
            string path = "\\\\?\\HID#VID_FAKE&PID_0001#ChargerDramaSimulated#{378de44c-56ef-11d1-bc8c-00a0c91405dd}";
            int nameOffset = Marshal.OffsetOf(typeof(DEV_BROADCAST_DEVICEINTERFACE), "dbcc_name").ToInt32();
            int size = nameOffset + (path.Length + 1) * 2;
            IntPtr buf = Marshal.AllocHGlobal(size);
            try
            {
                for (int i = 0; i < size; i++) Marshal.WriteByte(buf, i, 0);
                Marshal.WriteInt32(buf, 0, size);
                Marshal.WriteInt32(buf, 4, DBT_DEVTYP_DEVICEINTERFACE);
                Marshal.WriteInt32(buf, 8, 0);
                byte[] g = GuidMouse.ToByteArray();
                Marshal.Copy(g, 0, new IntPtr(buf.ToInt64() + 12), g.Length);
                Marshal.Copy(path.ToCharArray(), 0, new IntPtr(buf.ToInt64() + nameOffset), path.Length);
                SendSim(arrival, buf);
            }
            finally { Marshal.FreeHGlobal(buf); }
        }

        private void SendSim(bool arrival, IntPtr lParam)
        {
            bool old = bypassSuppression;
            bypassSuppression = true;
            try
            {
                SendMessage(Handle, WM_DEVICECHANGE, new IntPtr(arrival ? DBT_DEVICEARRIVAL : DBT_DEVICEREMOVECOMPLETE), lParam);
            }
            finally { bypassSuppression = old; }
        }
    }
}
