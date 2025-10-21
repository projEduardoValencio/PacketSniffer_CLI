using System.Buffers;
using System.Threading.Channels;
using PacketDotNet;
using PacketSniffer.Interfaces;

namespace PacketSniffer.Worker;

public sealed class PacketProcessor : IProcessor, IAsyncDisposable
{
    private Channel<byte[]> _queue;
    private CancellationToken _cancellationToken;
    private Task _consumer;
    private IMetrics _metrics;
    private LinkLayers _linkLayer;
    private Action<(string time, string source, string destination, string proto)> _onParsed;

    public void SetUp(CancellationToken cancellationToken, LinkLayers linkLayer, IMetrics metrics, Action<(string time, string source, string destination, string proto)> onParsed, int capacity = 2048)
    {
        _linkLayer = linkLayer;
        _metrics = metrics;
        _onParsed = onParsed;
        _cancellationToken = cancellationToken;

        _queue = Channel.CreateBounded<byte[]>(new BoundedChannelOptions(capacity)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.DropOldest
        });

        _consumer = Task.Run(ConsumeAsync);
    }

    public bool TryEnqueue(ReadOnlySpan<byte> data)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(data.Length);
        data.CopyTo(buffer);

        if (!_queue.Writer.TryWrite(buffer))
        {
            ArrayPool<byte>.Shared.Return(buffer);
            _metrics.IncDrop();
            return false;
        }
        
        _metrics.IncPacket(data.Length);
        return true;
    }

    private async Task ConsumeAsync()
    {
        var ct = _cancellationToken;

        while (await _queue.Reader.WaitToReadAsync(ct))
        {
            while (_queue.Reader.TryPeek(out var buffer))
            {
                try
                {
                    var packet = Packet.ParsePacket(_linkLayer, buffer);
                    var ip = packet.Extract<IPPacket>();
                    var protocol = ip?.Protocol.ToString() ?? "UNK";

                    string source = "-";
                    string destination = "-";

                    if (ip != null)
                    {
                        source = ip.SourceAddress.ToString();
                        destination = ip.DestinationAddress.ToString();
                    }
                
                    _onParsed((DateTime.Now.ToString(" HH:mm:ss"), source, destination, protocol));
                }
                catch (Exception ex)
                {
                    Console.BackgroundColor = ConsoleColor.Red;
                    Console.WriteLine(ex.Message);
                    Console.ResetColor();
                }
                finally
                {
                    System.Buffers.ArrayPool<byte>.Shared.Return(buffer);
                }
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        _queue.Writer.TryComplete();
        try
        {
            await _consumer;
        }
        catch (Exception ex)
        {
            Console.BackgroundColor = ConsoleColor.Red;
            Console.WriteLine(ex.Message);
            Console.ResetColor();
        }
    }
}