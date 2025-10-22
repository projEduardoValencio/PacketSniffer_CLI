using System.Runtime.CompilerServices;
using PacketDotNet;
using PacketSniffer.Capture;
using PacketSniffer.Interfaces;
using PacketSniffer.Worker;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace PacketSniffer.UI;

public sealed class CliApp
{
    private readonly ICaptureProvider _capture;
    private readonly IMetrics _metrics;
    private readonly Queue<TableData> _rows = new();
    private readonly object _rowsLock = new();
    private readonly IProcessor _processor;

    public CliApp(ICaptureProvider capture, IMetrics metrics, IProcessor processor)
    {
        _capture = capture;
        _metrics = metrics;
        _processor = processor;
    }

    public async Task<int> Run(string? deviceName, string? bpfFilter, CancellationToken ct)
    {
        // Check system devices
        var devices = _capture.GetDevices().ToList();
        if (!devices.Any())
        {
            AnsiConsole.MarkupLine("[red]Nenhuma interface encontrada.[/]");
            return 2;
        }

        // Get Device from user input
        deviceName ??= AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("Selecione a interface")
                .PageSize(10)
                .AddChoices(devices.Select(d => d.description.ToString()))
        );
        deviceName = devices.First(d => d.description.ToString() == deviceName).name;
        
        // Set up processor
        _processor.SetUp(ct, LinkLayers.Ethernet, _metrics, AddRow);

        // Set up capture with Device and Filter
        _capture.Open(deviceName);
        _capture.SetFilter(bpfFilter);
        
        // Start capture of Packets
        _capture.Start((buf, offset, lenght, linkType) => { _processor.TryEnqueue(buf); });

        // UI Construction
        UIConstruction(deviceName, out Table table, out Panel panel, out Layout layout);
        
        var tableRows = new List<TableData>();
        await AnsiConsole.Live(layout).StartAsync(async context =>
        {
            while (!ct.IsCancellationRequested)
            {
                tableRows.Clear();
                
                // Read new Rows
                lock (_rows)
                {
                    while (_rows.Count > 0)
                    {
                        var item = _rows.Dequeue();
                        tableRows.Add(new TableData(item.time, item.source, item.destination, item.protocol));
                        //table.AddRow(item.time, item.source, item.destination, item.protocol);
                    }
                }

                // Remove duplicates
                CompactTable(tableRows, table);
                
                // Cut table to fit in 10 rows
                while (table.Rows.Count > 10)
                {
                    table.Rows.RemoveAt(0);
                }
                
                // Get Snapshot
                var (totalPackets, totalBytes, packetsPerSec, bytesPerSec, drops) = _metrics.Snapshot();
                
                // Update panel with snapshot
                panel = new Panel(
                    $"packets: {totalPackets} | bytes: {totalBytes} | pps: {packetsPerSec} | {bytesPerSec} | drops: {drops}"
                ).Border(BoxBorder.Rounded).Header("Metrics").Expand();

                // Update UI
                layout["header"].Update(panel);
                context.Refresh();
                
                // Wait 0.5 sec to update screen
                await Task.Delay(500);
            }
        });
        
        AnsiConsole.WriteLine("[red]:cross_mark: Shuting down... [/]");
        
        return 0;
    }

    private static void UIConstruction(string deviceName, out Table table, out Panel panel, out Layout layout)
    {
        panel = new Panel($"packets: {0} | bytes: {0} | pps: {0} | {0} | drops: {0}")
            .Border(BoxBorder.Rounded)
            .Header("Metrics")
            .Expand();
        table = new Table()
            .Title($"[bold]NetSniffer[/] on [green]{deviceName}[/]")
            .AddColumn("Time")
            .AddColumn("Source")
            .AddColumn("Destination")
            .AddColumn("Protocol")
            .Border(TableBorder.Rounded)
            .Expand();

        table.Columns[0].Width(10);
        table.Columns[1].Width(10);
        table.Columns[2].Width(10);
        table.Columns[3].Width(10);
        
        layout = new Layout("Root")
            .SplitRows(
                new Layout("header").Ratio(1),
                new Layout("table").Ratio(4)
            );
        layout["header"].Update(panel);
        layout["table"].Update(table);
    }

    // Compact table ignoring Time from packet caputure
    private void CompactTable(List<TableData> tableRows, Table table)
    {
        var renderOptions = RenderOptions.Create(AnsiConsole.Console);
        
        // Previous Data from Table
        TableData prevTableData = new TableData(null, null, null, null);
        
        // If tableRow and table has any item, fill the prevTableData with last item
        if (tableRows.Count > 0 && table.Rows.Count > 0)
        {
            var lastIndex = table.Rows.Count - 1;
            prevTableData = new TableData(
                "",
                table.Rows.ElementAt(lastIndex).ElementAt(1).Render(renderOptions, 50).ElementAt(0).Text, 
                table.Rows.ElementAt(lastIndex).ElementAt(2).Render(renderOptions, 50).ElementAt(0).Text,
                table.Rows.ElementAt(lastIndex).ElementAt(3).Render(renderOptions, 10).ElementAt(0).Text
            );
        }

        // If we have the same item again we don't need to show on Console
        foreach (var item in tableRows)
        {
            if (!item.Equals(prevTableData))
            {
                table.AddRow(item.time, item.source, item.destination, item.protocol);
                prevTableData = item;
            }
        }
    }

    // Function call when Worker get some data
    private void AddRow((string time, string source, string destination, string proto) data)
    {
        // Don't add if exceed 30 items
        if (_rows.Count < 30)
        {
            lock (_rows)
            {
                _rows.Enqueue(new TableData(data.time, data.source, data.destination, data.proto));
            }
        }
    }

    private record TableData(string time, string source, string destination, string protocol)
    {
        // Used in CompactTable
        public virtual bool Equals(TableData? other)
        {
            var isSourceEqual = this.source == other?.source;
            var isDestinationEqual = this.destination == other?.destination;
            var isProtocolEqual = this.protocol == other?.protocol;
            
            return isProtocolEqual && isSourceEqual && isDestinationEqual;
        }
    };
}