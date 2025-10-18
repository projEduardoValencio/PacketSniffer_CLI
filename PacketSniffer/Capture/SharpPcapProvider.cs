
using PacketDotNet;
using PacketSniffer.Shared;
using SharpPcap;

namespace PacketSniffer.Capture
{
    public class SharpPcapProvider : ICaptureProvider
    {
        private ICaptureDevice? _device;

        public IEnumerable<DeviceInfo> GetDevices()
        {
            return CaptureDeviceList.Instance.Select(device => new DeviceInfo(device.Name, device.Description));
        }

        public void Open(string deviceName, bool promiscuous = true, int readTimeoutMs = 1000)
        {
            _device = CaptureDeviceList.Instance.FirstOrDefault(device => device.Name == deviceName);

            if (_device == null)
            {
                throw new DeviceNotFoundException(deviceName);
            }

            _device.Open(promiscuous ? DeviceModes.Promiscuous : DeviceModes.None, readTimeoutMs);
        }

        public void SetFilter(string? filter)
        {
            if (_device == null) throw new DeviceNotOpened();

            if (!string.IsNullOrWhiteSpace(filter))
                _device.Filter = filter;
        }

        public void Start(Action<byte[], int, int, uint> handlePacket)
        {
            if (_device == null) throw new DeviceNotOpened();
            
            _device.OnPacketArrival += (_, e) =>
            {
                var raw = e.GetPacket();
                handlePacket(raw.Data, 0, raw.Data.Length, (uint)raw.LinkLayerType);
            };
            
            _device.StartCapture();
        }

        public void Stop()
        {
            if (_device == null) return;
            _device.StopCapture();
            _device.Close();
        }

        public ValueTask DisposeAsync()
        {
            _device?.Close();
            _device = null;
            return ValueTask.CompletedTask;
        }
    }
}
