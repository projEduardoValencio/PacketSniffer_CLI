using PacketSniffer.Interfaces;

namespace PacketSniffer.Worker;

public sealed class Metrics : IMetrics
{
    private long _packets;
    private long _bytes;
    private long _drops;
    private long _lastPacket;
    private long _lastBytes;
    private DateTime _lastSnapshot = DateTime.UtcNow;
    
    // Interlocked atomic mult thread can access and modify
    // Volatile get value without lock thread
    public void IncPacket(int bytes)
    {
        Interlocked.Increment(ref _packets);
        Interlocked.Add(ref _bytes, bytes);
    }

    public void IncDrop() => Interlocked.Increment(ref _drops);

    public (long totalPkts, long totalBytes, double pps, double bps, long drops) Snapshot()
    {
        // Calc frame time
        var now = DateTime.UtcNow;
        var duration = (now - _lastSnapshot).TotalSeconds;
        
        // Short interval protection
        if (duration <= 0.1) 
        {
            return (Volatile.Read(ref _packets), Volatile.Read(ref _bytes), 0, 0, Volatile.Read(ref _drops));
        }

        // Get AVG metrics
        var packets = Volatile.Read(ref _packets);
        var bytes = Volatile.Read(ref _bytes);

        var deltaPacket = packets - _lastPacket;
        var deltaBytes = bytes - _lastBytes;

        var avgPacket = deltaPacket / duration;
        var avgBytes = deltaBytes / duration;
        
        // Update properties
        _lastSnapshot = now;
        _lastPacket = packets;
        _lastBytes = bytes;
        
        return (packets, bytes, avgPacket, avgBytes, Volatile.Read(ref _drops));
    }
}