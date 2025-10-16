namespace PacketSniffer.Interfaces;

public interface IMetrics
{
    public void IncDrop();
    public (long totalPkts, long totalBytes, double pps, double bps, long drops) Snapshot();
    public void IncPacket(int bytes);
}