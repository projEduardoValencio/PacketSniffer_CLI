using PacketDotNet;

namespace PacketSniffer.Interfaces;

public interface IProcessor
{
    public bool TryEnqueue(ReadOnlySpan<byte> data);
    public ValueTask DisposeAsync();

    public void SetUp(CancellationToken cancellationToken, LinkLayers linkLayer, IMetrics metrics,
        Action<(string time, string source, string destination, string proto)> onParsed, int capacity = 2048);
}