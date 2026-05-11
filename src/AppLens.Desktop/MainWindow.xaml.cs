using AppLens.Backend;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.Storage.Pickers;
using System.Runtime.InteropServices;
using Windows.Graphics;
using WinRT.Interop;

namespace AppLens.Desktop;

public sealed partial class MainWindow : Window
{
    private readonly AuditService _auditService = new();
    private readonly ReportWriter _reportWriter = new();
    private readonly TuneActionExecutor _tuneActionExecutor = new();
    private readonly AppLensRuntimeStorage _runtimeStorage = AppLensRuntimeStorage.Default();
    private readonly IBlackboardStore _blackboardStore;
    private readonly ModuleStatusService _moduleStatusService = new();
    private readonly DashboardReadModelService _dashboardReadModelService;
    private CancellationTokenSource? _scanCancellation;
    private AuditSnapshot? _snapshot;
    private List<TuneActionRecord> _actionLog = [];

    public MainWindow()
    {
        _blackboardStore = new BlackboardStore(_runtimeStorage);
        _dashboardReadModelService = new DashboardReadModelService(_moduleStatusService, _blackboardStore);
        InitializeComponent();
        ExtendsContentIntoTitleBar = false;
        ResizeForDashboardViewport();
        SetStatus("Ready");
        RuntimeRootText.Text = _runtimeStorage.Root;
        LedgerPathText.Text = _runtimeStorage.EventsJsonl;
        _ = RefreshDashboardAsync();
    }

    private async void RefreshDashboard_Click(object sender, RoutedEventArgs e)
    {
        await RefreshDashboardAsync(showErrors: true);
    }

    private void ResizeForDashboardViewport()
    {
        var dpi = GetDpiForWindow(WindowNative.GetWindowHandle(this));
        var scale = Math.Max(1, dpi / 96d);
        var displayArea = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary);
        var workArea = displayArea.WorkArea;
        var bounds = DashboardWindowSizing.Calculate(
            new DashboardWorkArea(workArea.X, workArea.Y, workArea.Width, workArea.Height),
            scale);

        AppWindow.MoveAndResize(
            new RectInt32(bounds.X, bounds.Y, bounds.Width, bounds.Height),
            displayArea);
    }

    private async void RunScan_Click(object sender, RoutedEventArgs e)
    {
        if (ConsentCheckBox.IsChecked != true)
        {
            await ShowDialogAsync("Consent required", "Please confirm that you understand AppLens scans locally and Tune actions require separate approval.");
            return;
        }

        await RunScanAsync(preserveActionLog: false);
    }

    private async Task RunScanAsync(bool preserveActionLog)
    {
        var existingActionLog = preserveActionLog ? _actionLog.ToList() : [];
        _scanCancellation = new CancellationTokenSource();
        SetBusy(true);
        SetStatus("Scanning...");

        try
        {
            var snapshot = await _auditService.RunAsync(_scanCancellation.Token);
            _snapshot = WithActionLog(snapshot, existingActionLog);
            _actionLog = _snapshot.ActionLog.ToList();
            var ledgerRecorded = await AppendLedgerEventAsync(BlackboardEvent.ForScanCompleted(_snapshot));
            RenderSnapshot(_snapshot);
            SetStatus(ledgerRecorded ? "Scan complete" : "Scan complete; ledger write failed");
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

    private async void ExportBundle_Click(object sender, RoutedEventArgs e)
    {
        if (_snapshot is null)
        {
            return;
        }

        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        var directory = Path.Combine(desktop, $"AppLens-{_snapshot.GeneratedAt:yyyyMMdd-HHmmss}");
        await _reportWriter.WriteAllAsync(_snapshot, directory, IncludeRawDetailsCheckBox.IsChecked == true);
        SetStatus($"Exported report bundle to {Path.GetFileName(directory)}");
        await ShowDialogAsync("Report bundle exported", $"Reports were saved to:\n{directory}");
    }

    private async void ApplyTuneActions_Click(object sender, RoutedEventArgs e)
    {
        if (_snapshot is null)
        {
            return;
        }

        if (TuneConsentCheckBox.IsChecked != true)
        {
            await ShowDialogAsync("Tune approval required", "Select the Tune approval checkbox before running AppLens-Tune actions.");
            return;
        }

        var selectedItems = TunePlanList.SelectedItems
            .OfType<TunePlanItem>()
            .ToList();
        if (selectedItems.Count == 0)
        {
            await ShowDialogAsync("No actions selected", "Select one or more AppLens-Tune plan items first.");
            return;
        }

        SetTuneBusy(true);
        SetStatus("Running Tune actions...");
        var results = new List<TuneActionRecord>();

        try
        {
            foreach (var item in selectedItems)
            {
                results.Add(await _tuneActionExecutor.ExecuteAsync(item, userApproved: true));
            }

            var ledgerFailures = 0;
            foreach (var result in results)
            {
                if (!await AppendLedgerEventAsync(BlackboardEvent.ForTuneAction(result)))
                {
                    ledgerFailures++;
                }
            }

            _actionLog.AddRange(results);
            _snapshot = WithActionLog(_snapshot, _actionLog);
            RenderSnapshot(_snapshot);

            var succeeded = results.Count(result => result.Status == TuneActionStatus.Succeeded);
            var blocked = results.Count(result => result.Status == TuneActionStatus.Blocked);
            var failed = results.Count(result => result.Status == TuneActionStatus.Failed);
            var ledgerStatus = ledgerFailures == 0 ? "" : $"; {ledgerFailures} ledger write(s) failed";
            SetStatus($"Tune complete: {succeeded} succeeded, {blocked} blocked, {failed} failed{ledgerStatus}");
        }
        finally
        {
            SetTuneBusy(false);
        }
    }

    private async Task<bool> AppendLedgerEventAsync(BlackboardEvent evt)
    {
        try
        {
            await _blackboardStore.AppendAsync(evt);
            await RefreshDashboardAsync();
            return true;
        }
        catch (Exception ex)
        {
            SetStatus("Ledger write failed");
            await ShowDialogAsync("Ledger write failed", ex.Message);
            return false;
        }
    }

    private async Task RefreshDashboardAsync(bool showErrors = false)
    {
        RefreshDashboardButton.IsEnabled = false;
        try
        {
            var indexedCount = await _blackboardStore.GetIndexedEventCountAsync();
            LedgerEventCountText.Text = indexedCount.ToString();
        }
        catch
        {
            LedgerEventCountText.Text = "unavailable";
        }

        try
        {
            var state = await _dashboardReadModelService.GetDashboardStateAsync(recentEventLimit: 8);
            var dashboard = DashboardPresentation.FromState(state);
            RenderDashboard(dashboard);
            HostedModulesList.ItemsSource = dashboard.ModuleCards
                .Select(card => new ModuleStatusRow(
                    card.DisplayName,
                    card.Availability,
                    card.Reason,
                    card.NextAction))
                .ToList();
        }
        catch (Exception ex)
        {
            DashboardOverallStateText.Text = "Unavailable";
            if (showErrors)
            {
                await ShowDialogAsync("Dashboard refresh failed", ex.Message);
            }
        }
        finally
        {
            RefreshDashboardButton.IsEnabled = true;
        }
    }

    private async void VerifyTune_Click(object sender, RoutedEventArgs e)
    {
        if (ConsentCheckBox.IsChecked != true)
        {
            await ShowDialogAsync("Consent required", "Please confirm that AppLens can rescan this machine locally.");
            return;
        }

        await RunScanAsync(preserveActionLog: true);
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
        ReadinessText.Text = DashboardPresentation.FormatReadinessScore(snapshot);
        ReadinessRatingText.Text = DashboardPresentation.FormatReadinessRating(snapshot);
        PlanText.Text = $"{snapshot.TunePlan.Count} item(s)";
        StartupText.Text = $"{snapshot.Readiness.StartupEnabledCount}/{snapshot.Readiness.StartupTotalCount} enabled";
        StorageText.Text = Formatting.Size(snapshot.Readiness.StorageHotspotBytes);
        AdminText.Text = $"{snapshot.Readiness.AdminRequiredCount} item(s)";
        ReadinessHighlightsList.ItemsSource = snapshot.Readiness.Highlights;
        FindingsList.ItemsSource = snapshot.Findings;
        TunePlanList.ItemsSource = snapshot.TunePlan;
        ActionLogList.ItemsSource = snapshot.ActionLog;
        var activeAppRows = DashboardPresentation.BuildActiveAppRows(snapshot);
        ActiveAppsList.ItemsSource = activeAppRows;
        ActiveAppsEmptyText.Visibility = activeAppRows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        AppsList.ItemsSource = snapshot.Inventory.DesktopApplications
            .Concat(snapshot.Inventory.StoreApplications)
            .Concat(snapshot.Inventory.RuntimesAndFrameworks)
            .ToList();
        DiagnosticsList.ItemsSource = BuildDiagnostics(snapshot);

        ExportJsonButton.IsEnabled = true;
        ExportMarkdownButton.IsEnabled = true;
        ExportHtmlButton.IsEnabled = true;
        ExportBundleButton.IsEnabled = true;
        ApplyTuneActionsButton.IsEnabled = snapshot.TunePlan.Count > 0;
        VerifyTuneButton.IsEnabled = true;
    }

    private void RenderDashboard(DashboardPresentation dashboard)
    {
        DashboardOverallStateText.Text = dashboard.Summary.OverallState;
        DashboardModuleCoverageText.Text = dashboard.Summary.ModuleCoverage;
        DashboardPendingApprovalsText.Text = dashboard.Summary.PendingApprovals;
        DashboardRecentEventsText.Text = dashboard.Summary.RecentEvents;
        DashboardLastEventText.Text = dashboard.Summary.LastEvent;
        DashboardRailBadgeText.Text = dashboard.Rail.DashboardBadge;
        InventoryRailBadgeText.Text = dashboard.Rail.InventoryBadge;
        TunePlanRailBadgeText.Text = dashboard.Rail.TunePlanBadge;
        ReportsRailBadgeText.Text = dashboard.Rail.ReportsBadge;

        ModuleRailList.ItemsSource = dashboard.Rail.Modules;
        ModuleCardsList.ItemsSource = dashboard.ModuleCards;
        PendingApprovalsList.ItemsSource = dashboard.PendingActions;
        RecentLedgerEventsList.ItemsSource = dashboard.RecentLedgerEvents;

        ModuleCardsEmptyText.Visibility = dashboard.ModuleCards.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        PendingApprovalsEmptyText.Visibility = dashboard.PendingActions.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        LedgerEventsEmptyText.Visibility = dashboard.RecentLedgerEvents.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
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
        ApplyTuneActionsButton.IsEnabled = !isBusy && _snapshot?.TunePlan.Count > 0;
        VerifyTuneButton.IsEnabled = !isBusy && _snapshot is not null;
    }

    private void SetTuneBusy(bool isBusy)
    {
        ApplyTuneActionsButton.IsEnabled = !isBusy && _snapshot?.TunePlan.Count > 0;
        VerifyTuneButton.IsEnabled = !isBusy && _snapshot is not null;
        RunButton.IsEnabled = !isBusy;
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

    private static AuditSnapshot WithActionLog(AuditSnapshot snapshot, List<TuneActionRecord> actionLog) =>
        new()
        {
            SchemaVersion = snapshot.SchemaVersion,
            GeneratedAt = snapshot.GeneratedAt,
            Machine = snapshot.Machine,
            Inventory = snapshot.Inventory,
            Tune = snapshot.Tune,
            Readiness = snapshot.Readiness,
            Findings = snapshot.Findings,
            TunePlan = snapshot.TunePlan,
            ActionLog = actionLog,
            ProbeStatuses = snapshot.ProbeStatuses
        };

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hwnd);
}

public sealed record DiagnosticRow(string Name, string Value, string Detail);

public sealed record ModuleStatusRow(string Name, string Status, string Reason, string NextAction);
