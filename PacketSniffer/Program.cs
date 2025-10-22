using PacketSniffer.Capture;
using PacketSniffer.Interfaces;
using PacketSniffer.UI;
using PacketSniffer.Worker;
using Spectre.Console;


public abstract class Program()
{
    public static async Task Main(string[] args)
    {
        var cts = GenerateCancelTokenSource();

        var (device, filter) = ReadArgs(args);

        try
        {
            await using var cap = new SharpPcapProvider();
            IMetrics metrics = new Metrics();
            IProcessor processor = new PacketProcessor();
            
            var app = new CliApp(cap, metrics, processor);
            var code = await app.Run(device, filter, cts.Token);
            Environment.ExitCode = code;
        }
        finally
        {
            Console.WriteLine("Shutting down...");
        }
    }

    private static (string? device, string? filter) ReadArgs(string[] args)
    {
        string? device = null;
        string? filter = null;

        for (int i = 1; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--dev":
                    device = args[++i]; 
                    break;
                case "--filter":
                    filter = args[++i];
                    break;
            }
        }

        return (device, filter);
    }

    private static CancellationTokenSource GenerateCancelTokenSource()
    {
        var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) => { 
            e.Cancel = true;
            cts.Cancel();
            throw new Exception("Cancelled");
        };
        return cts;
    }
}
