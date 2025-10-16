using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PacketSniffer.Capture
{
    public record DeviceInfo(string name, string description);

    public interface ICaptureProvider
    {
        IEnumerable<DeviceInfo> GetDevices();

        void Open(string deviceName, bool promiscuous = true, int readTimeoutMs = 1000);
        void SetFilter(string? filter);
        void Start(Action<byte[], int, int, uint> handlePacket);
        void Stop();
    }
}
