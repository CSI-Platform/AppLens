using AppLens.Backend;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.Storage.Pickers;
using Windows.Graphics;

namespace AppLens.Desktop;

public sealed partial class MainWindow : Window
{
    private readonly AuditService _auditService = new();
    private readonly ReportWriter _reportWriter = new();
    private readonly RemovalService _removalService = new();
    private List<RemovalRecord> _actions = [];
    private ProbeStatus? _historyWarning;
    private bool _actionBusy;
    private InventorySnapshot? _snapshot;
    private InventorySnapshot? _displaySnapshot;
    private CancellationTokenSource? _scanCancellation;

    public MainWindow()
    {
        InitializeComponent();
        var area = Microsoft.UI.Windowing.DisplayArea.GetFromWindowId(AppWindow.Id, Microsoft.UI.Windowing.DisplayAreaFallback.Primary).WorkArea;
        AppWindow.Resize(new SizeInt32(Math.Min(1280, area.Width - 48), Math.Min(880, area.Height - 48)));
    }

    private void ClientLayout_Changed(object sender, SizeChangedEventArgs e)
    {
        if (DetailsPanel is null || ResultsPanel is null) return;
        // Keep the virtualized list bounded and usable. Smaller windows and expanded
        // details scroll the page instead of consuming the entire results viewport.
        var surroundingHeight = HeaderPanel.ActualHeight + StoragePanel.ActualHeight +
            FiltersPanel.ActualHeight + DetailsPanel.ActualHeight +
            RootGrid.Padding.Top + RootGrid.Padding.Bottom + 4 * RootGrid.RowSpacing;
        ResultsPanel.Height = Math.Max(240, PageScroll.ActualHeight - surroundingHeight);
    }

    private async void RunScan_Click(object sender, RoutedEventArgs e)
    {
        if (_actionBusy || _scanCancellation is not null) return;
        _scanCancellation = new CancellationTokenSource();
        SetBusy(true);
        StatusText.Text = "Scanning installed apps and storage…";
        try
        {
            var cancellation = _scanCancellation;
            var progress = new Progress<InventorySnapshot>(partial =>
            {
                if (_scanCancellation != cancellation || cancellation.IsCancellationRequested) return;
                RenderSnapshot(partial, collecting: true);
            });
            var snapshot = WithHistory(await _auditService.RunAsync(_scanCancellation.Token, progress));
            _snapshot = snapshot;
            RenderSnapshot(snapshot, collecting: false);
        }
        catch (OperationCanceledException)
        {
            if (_snapshot is not null) RenderSnapshot(_snapshot, collecting: false);
            else { _displaySnapshot = null; AppInventoryList.ItemsSource = null; CountText.Text = ""; EmptyText.Visibility = Visibility.Visible; EmptyText.Text = "Scan cancelled. Run a new scan when ready."; }
            StatusText.Text = "Scan cancelled. Previous completed results retained.";
        }
        catch (Exception ex)
        {
            if (_snapshot is not null) RenderSnapshot(_snapshot, collecting: false);
            else { _displaySnapshot = null; AppInventoryList.ItemsSource = null; }
            StatusText.Text = $"Scan failed: {ex.Message}";
        }
        finally { _scanCancellation.Dispose(); _scanCancellation = null; SetBusy(false); }
    }

    private void CancelScan_Click(object sender, RoutedEventArgs e) => _scanCancellation?.Cancel();

    private void RenderSnapshot(InventorySnapshot snapshot, bool collecting)
    {
        _displaySnapshot = snapshot;
        ApplyFilters();
        StatusText.Text = $"{snapshot.Applications.Count()} inventory entries · {(collecting ? "Collecting device details…" : snapshot.IsPartial ? "Partial scan — see report coverage" : "Scan complete")} · {snapshot.Duration.TotalSeconds:N2}s";
        DeviceText.Text = snapshot.Machine.Disks.Count == 0 ? "Disk readings unavailable" : string.Join("   |   ", snapshot.Machine.Disks.Select(d => d.Summary + (d.UsedPercent is { } percent ? $" · {percent:N1}% used" : "")));
        var machine = snapshot.Machine;
        MachineDetailsText.Text = $"{machine.OSDescription}\n{machine.Manufacturer} {machine.Model}\nCPU: {string.Join("; ", machine.Processors)}\nGPU: {string.Join("; ", machine.Graphics)}\nRAM: {InventoryFormatting.Size(machine.TotalMemoryBytes)} · {(machine.MemoryUsedPercent is { } used ? $"{used:N1}% used" : "utilization unknown")}\nUptime: {(machine.LastBootAt is { } boot ? (snapshot.GeneratedAt - boot).ToString(@"d\.hh\:mm\:ss") : "Unknown")}\nCaptured: {snapshot.GeneratedAt:g}\n" +
            string.Join("\n", snapshot.ProbeStatuses.Select(p => $"{p.Name}: {p.State} {p.Message}"));
    }

    private void Search_Changed(object sender, TextChangedEventArgs e) => ApplyFilters();
    private void Filter_Changed(object sender, SelectionChangedEventArgs e) => ApplyFilters();
    private void ApplyFilters()
    {
        if (_displaySnapshot is null || RemovalFilter is null) return;
        var selected = (AppInventoryList.SelectedItem as InventoryRow)?.App.Id;
        var rows = InventoryPresentation.Filter(_displaySnapshot.Applications, SearchBox.Text,
            SortFilter.SelectedIndex, ScopeFilter.SelectedIndex, KindFilter.SelectedIndex - 1, RemovalFilter.SelectedIndex)
            .Select(a => new InventoryRow(a, _actions.LastOrDefault(action => action.AppId == a.Id), _actionBusy || _scanCancellation is not null)).ToList();
        AppInventoryList.ItemsSource = rows;
        AppInventoryList.SelectedItem = rows.FirstOrDefault(a => a.App.Id == selected);
        CountText.Text = $"{rows.Count} of {_displaySnapshot.Applications.Count()} entries";
        EmptyText.Text = "No apps match these filters.";
        EmptyText.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void App_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (AppDetailsText is not null) AppDetailsText.Text = AppInventoryList.SelectedItem is InventoryRow row ? InventoryPresentation.Details(row.App) : "Select an app to inspect its installation and removal details.";
    }

    private async void ExportMarkdown_Click(object sender, RoutedEventArgs e)
    {
        if (_snapshot is not { } snapshot || _scanCancellation is not null || _actionBusy) return;
        try
        {
            var format = ReportFormat.SelectedIndex;
            var extension = format == 1 ? ".json" : format == 2 ? ".html" : ".md";
            var raw = IncludeRawDetailsCheckBox.IsChecked == true;
            var content = format == 1 ? _reportWriter.WriteJson(snapshot, raw) :
                format == 2 ? _reportWriter.WriteHtml(snapshot, raw) : _reportWriter.WriteMarkdown(snapshot, raw);
            var picker = new FileSavePicker(AppWindow.Id)
            {
                SuggestedFolder = Windows.Storage.UserDataPaths.GetDefault().Downloads,
                SuggestedStartLocation = PickerLocationId.Downloads,
                SuggestedFileName = $"AppLens-{snapshot.GeneratedAt:yyyyMMdd-HHmmss-fff}"
            };
            picker.FileTypeChoices.Add(format == 1 ? "JSON report" : format == 2 ? "HTML report" : "Markdown report", [extension]);
            var file = await picker.PickSaveFileAsync();
            if (file is null) { StatusText.Text = "Save cancelled. Results are still available."; return; }
            // The picker obtains the user's explicit destination/overwrite decision.
            await File.WriteAllTextAsync(file.Path, content);
            StatusText.Text = $"Report saved: {file.Path}";
        }
        catch (Exception ex) { await ShowDialogAsync("Could not save report", ex.Message); }
    }

    private void SetBusy(bool busy)
    {
        RunButton.IsEnabled = !busy;
        CancelButton.IsEnabled = _scanCancellation is not null && !_actionBusy;
        DownloadReportButton.IsEnabled = !busy && _snapshot is not null;
        ScanProgress.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        ApplyFilters();
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            _actions = await _removalService.ReadHistoryAsync();
            if (_snapshot is not null) _snapshot = WithHistory(_snapshot);
            RenderHistory(); ApplyFilters();
        }
        catch (Exception ex)
        {
            _historyWarning = new ProbeStatus { Name = "Action history", State = ProbeState.Failed,
                Message = "Previous action history unavailable: " + ex.Message };
            if (_snapshot is not null)
            {
                _snapshot = WithHistory(_snapshot);
                if (_scanCancellation is null) RenderSnapshot(_snapshot, collecting: false);
            }
            StatusText.Text = _historyWarning.Message;
            RenderHistory();
        }
    }

    private InventorySnapshot WithHistory(InventorySnapshot snapshot, ProbeStatus? warning = null) => new()
    {
        SchemaVersion = snapshot.SchemaVersion, GeneratedAt = snapshot.GeneratedAt, Machine = snapshot.Machine,
        Inventory = snapshot.Inventory, ProbeStatuses = snapshot.ProbeStatuses.Where(p => p.Name != "Action history")
            .Concat(new[] { warning, _historyWarning }.OfType<ProbeStatus>()).ToList(), Duration = snapshot.Duration,
        FirstResultsDuration = snapshot.FirstResultsDuration, Actions = _actions.ToList()
    };

    private void RenderHistory()
    {
        HistoryText.Text = _historyWarning is not null ? _historyWarning.Message : _actions.Count == 0 ? "No recorded actions." : string.Join("\n\n", _actions.AsEnumerable().Reverse().Select(a =>
            $"{a.StartedAt:g} · {a.AppName} · {a.Outcome}\n{a.Detail}\n{(a.DiskChangeDisplay.Length == 0 ? "Disk change unknown" : a.DiskChangeDisplay)}\nIdentity: {a.AppId}"));
    }

    private async void Remove_Click(object sender, RoutedEventArgs e)
    {
        if (_actionBusy || _scanCancellation is not null || _snapshot is null || sender is not Button { DataContext: InventoryRow row }) return;
        _actionBusy = true;
        SetBusy(true);
        StatusText.Text = "Checking this installation and its removal route…";
        try
        {
            var plan = await _removalService.PrepareAsync(row.App.Id);
            var handoff = plan.Command.Route == RemovalRoute.WindowsSettings;
            var admin = new CheckBox { Content = "Request administrator approval", IsEnabled = plan.App.Scope == InstallationScope.AllUsers && plan.Command.Route is RemovalRoute.Msi or RemovalRoute.Vendor,
                Visibility = plan.App.Scope == InstallationScope.AllUsers && plan.Command.Route is RemovalRoute.Msi or RemovalRoute.Vendor ? Visibility.Visible : Visibility.Collapsed };
            var content = new StackPanel { Spacing = 12, MaxWidth = 560 };
            content.Children.Add(new TextBlock { Text = InventoryPresentation.Details(plan.App), TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = true });
            content.Children.Add(new TextBlock { Text = handoff ? "This opens Windows Installed Apps. AppLens will not remove the app or claim removal succeeded." :
                "The app may close and its local data may be deleted. Save your work first. Windows or the vendor may request administrator approval. AppLens will not request a restart. Cancel in the vendor or Windows dialog where supported; Store removal cannot be cancelled once started.", TextWrapping = TextWrapping.Wrap });
            if (plan.Command.Executable.Length > 0) content.Children.Add(new TextBlock { Text = "Uninstaller: " + plan.Command.Executable, TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = true });
            content.Children.Add(admin);
            var dialog = new ContentDialog { Title = handoff ? "Review this app in Windows?" : "Uninstall this app?",
                Content = new ScrollViewer { Content = content, MaxHeight = 440 }, PrimaryButtonText = handoff ? "Open Windows" : "Uninstall",
                CloseButtonText = "Cancel", DefaultButton = ContentDialogButton.Close, XamlRoot = Content.XamlRoot };
            var approved = await dialog.ShowAsync() == ContentDialogResult.Primary;
            StatusText.Text = approved ? "Removal in progress. Complete any Windows or vendor approval dialog…" : "Cancelling…";
            var result = await _removalService.ExecuteAsync(plan.Token, approved, admin.IsChecked == true);
            _actions.Add(result);
            RenderHistory();
            _snapshot = WithHistory(_snapshot);
            if (approved)
            {
                try { _snapshot = WithHistory(await _auditService.RunAsync()); }
                catch (Exception ex) { _snapshot = WithHistory(_snapshot, new ProbeStatus { Name = "Post-removal refresh", State = ProbeState.Failed, Message = "Previous capture retained: " + ex.Message }); }
            }
            RenderSnapshot(_snapshot, collecting: false);
            StatusText.Text = $"{result.AppName}: {result.Outcome}. {result.Detail}" + (_snapshot.IsPartial ? " Some readings are unavailable; see report coverage." : "");
        }
        catch (Exception ex) { StatusText.Text = "Removal did not complete: " + ex.Message; await ShowDialogAsync("Removal unavailable", ex.Message); }
        finally { _actionBusy = false; SetBusy(false); }
    }

    private async Task ShowDialogAsync(string title, string message) =>
        await new ContentDialog { Title = title, Content = message, CloseButtonText = "OK", XamlRoot = Content.XamlRoot }.ShowAsync();
}
