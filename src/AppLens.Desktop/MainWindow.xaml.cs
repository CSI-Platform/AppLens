using AppLens.Backend;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.Storage.Pickers;

namespace AppLens.Desktop;

public sealed partial class MainWindow : Window
{
    private readonly AuditService _auditService = new();
    private readonly ReportWriter _reportWriter = new();
    private CancellationTokenSource? _scanCancellation;
    private AuditSnapshot? _snapshot;

    public MainWindow()
    {
        InitializeComponent();
        ExtendsContentIntoTitleBar = false;
        SetStatus("Ready");
    }

    private async void RunScan_Click(object sender, RoutedEventArgs e)
    {
        if (ConsentCheckBox.IsChecked != true)
        {
            await ShowDialogAsync("Consent required", "Please confirm that you understand this is a read-only local scan.");
            return;
        }

        _scanCancellation = new CancellationTokenSource();
        SetBusy(true);
        SetStatus("Scanning...");

        try
        {
            _snapshot = await _auditService.RunAsync(_scanCancellation.Token);
            RenderSnapshot(_snapshot);
            SetStatus("Scan complete");
        }
        catch (OperationCanceledException)
        {
            SetStatus("Scan cancelled");
        }
        catch (Exception ex)
        {
            SetStatus("Scan failed");
            await ShowDialogAsync("Scan failed", ex.Message);
        }
        finally
        {
            SetBusy(false);
            _scanCancellation.Dispose();
            _scanCancellation = null;
        }
    }

    private void CancelScan_Click(object sender, RoutedEventArgs e)
    {
        _scanCancellation?.Cancel();
    }

    private async void ExportJson_Click(object sender, RoutedEventArgs e)
    {
        await ExportAsync("JSON report", ".json", snapshot => _reportWriter.WriteJson(snapshot, IncludeRawDetailsCheckBox.IsChecked == true));
    }

    private async void ExportMarkdown_Click(object sender, RoutedEventArgs e)
    {
        await ExportAsync("Markdown report", ".md", snapshot => _reportWriter.WriteMarkdown(snapshot, IncludeRawDetailsCheckBox.IsChecked == true));
    }

    private async void ExportHtml_Click(object sender, RoutedEventArgs e)
    {
        await ExportAsync("HTML report", ".html", snapshot => _reportWriter.WriteHtml(snapshot, IncludeRawDetailsCheckBox.IsChecked == true));
    }

    private async Task ExportAsync(string label, string extension, Func<AuditSnapshot, string> contentFactory)
    {
        if (_snapshot is null)
        {
            return;
        }

        var picker = new FileSavePicker(AppWindow.Id)
        {
            SuggestedFileName = $"AppLens-{_snapshot.GeneratedAt:yyyyMMdd-HHmmss}",
            DefaultFileExtension = extension,
            CommitButtonText = "Export"
        };
        picker.FileTypeChoices.Add(label, [extension]);

        var result = await picker.PickSaveFileAsync();
        if (result is null)
        {
            return;
        }

        await File.WriteAllTextAsync(result.Path, contentFactory(_snapshot));
        SetStatus($"Exported {Path.GetFileName(result.Path)}");
    }

    private void RenderSnapshot(AuditSnapshot snapshot)
    {
        MachineText.Text = snapshot.Machine.ComputerName;
        AppsText.Text = (snapshot.Inventory.DesktopApplications.Count + snapshot.Inventory.StoreApplications.Count).ToString();
        FindingsText.Text = snapshot.Findings.Count.ToString();
        FindingsList.ItemsSource = snapshot.Findings;
        AppsList.ItemsSource = snapshot.Inventory.DesktopApplications
            .Concat(snapshot.Inventory.StoreApplications)
            .Concat(snapshot.Inventory.RuntimesAndFrameworks)
            .ToList();
        DiagnosticsList.ItemsSource = BuildDiagnostics(snapshot);

        ExportJsonButton.IsEnabled = true;
        ExportMarkdownButton.IsEnabled = true;
        ExportHtmlButton.IsEnabled = true;
    }

    private static List<DiagnosticRow> BuildDiagnostics(AuditSnapshot snapshot)
    {
        var rows = new List<DiagnosticRow>();
        rows.AddRange(snapshot.Tune.TopProcesses.Select(process =>
            new DiagnosticRow(process.Name, Formatting.Size(process.WorkingSetBytes), $"PID {process.Id}; CPU {process.CpuSeconds:N1}s")));
        rows.AddRange(snapshot.Tune.StartupEntries.Take(20).Select(entry =>
            new DiagnosticRow(entry.Name, entry.State, entry.Location)));
        rows.AddRange(snapshot.Tune.StorageHotspots.Select(item =>
            new DiagnosticRow(item.Location, Formatting.Size(item.Bytes), item.Path)));
        rows.AddRange(snapshot.Tune.ToolProbes.Select(tool =>
            new DiagnosticRow(tool.Name, tool.Status, tool.Output)));
        return rows;
    }

    private void SetBusy(bool isBusy)
    {
        RunButton.IsEnabled = !isBusy;
        CancelButton.IsEnabled = isBusy;
        ScanProgress.IsActive = isBusy;
    }

    private void SetStatus(string status)
    {
        StatusText.Text = status;
    }

    private async Task ShowDialogAsync(string title, string content)
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = content,
            CloseButtonText = "OK",
            XamlRoot = Content.XamlRoot
        };
        await dialog.ShowAsync();
    }
}

public sealed record DiagnosticRow(string Name, string Value, string Detail);
