namespace PacketSniffer.Shared
{
    public class PacketException : Exception
    {
        public PacketException(string msg) : base(msg) { }
    }

    public class DeviceNotFoundException : PacketException {
        public string DeviceName { get; set; }
        public DeviceNotFoundException(string deviceName) : base($"Device {deviceName} not found")
        {
            DeviceName = deviceName;
        }
    }

    public class DeviceNotOpened : PacketException {
        public DeviceNotOpened() : base($"Device not opened") { }
    }
}
