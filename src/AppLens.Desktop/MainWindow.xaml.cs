using AppLens.Backend;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.Storage.Pickers;
using Windows.Graphics;

namespace AppLens.Desktop;

public sealed partial class MainWindow : Window
{
    private readonly AuditService _auditService = new();
    private readonly RemovalService _removalService = new();
    private ReportShareService? _reportShare;
    private bool _reportBusy;
    private List<RemovalRecord> _actions = [];
    private ProbeStatus? _historyWarning;
    private bool _actionBusy;
    private bool _launchTaskActive;
    private bool _checkingHistory;
    private bool _inventoryRefreshPending;
    private bool _loaded;
    private bool _scanStarted;
    private bool _closed;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly DispatcherTimer _statusTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private InventorySnapshot? _snapshot;
    private InventorySnapshot? _displaySnapshot;
    private CancellationTokenSource? _scanCancellation;

    public MainWindow()
    {
        InitializeComponent();
        _removalService.ActionStarted += action => DispatcherQueue.TryEnqueue(() =>
        {
            if (_closed) return;
            UpdateAction(action);
            _actionBusy = false;
            SetBusy(_scanCancellation is not null);
        });
        _statusTimer.Tick += async (_, _) =>
        {
            if (_actions.Any(a => RemovalTracking.IsDue(a, DateTimeOffset.UtcNow)))
                await RefreshActionsAsync(RemovalCheckMode.Due);
        };
        Activated += async (_, args) =>
        {
            if (_loaded && args.WindowActivationState != WindowActivationState.Deactivated)
                await RefreshActionsAsync(RemovalCheckMode.Unresolved);
        };
        Closed += (_, _) => { _closed = true; _statusTimer.Stop(); _lifetime.Cancel(); _scanCancellation?.Cancel(); _reportShare?.Dispose(); };
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

    private async void RunScan_Click(object sender, RoutedEventArgs e) => await RunScanAsync(diagnostics: false);

    private async void RunDiagnostics_Click(object sender, RoutedEventArgs e) => await RunScanAsync(diagnostics: true);

    private async Task RunScanAsync(bool diagnostics)
    {
        if (_closed || _actionBusy || _scanCancellation is not null) return;
        _scanStarted = true;
        _scanCancellation = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
        SetBusy(true);
        StatusText.Text = "Collecting PC specifications and installed tools…";
        try
        {
            var cancellation = _scanCancellation;
            var progress = new Progress<InventorySnapshot>(partial =>
            {
                if (_closed || _scanCancellation != cancellation || cancellation.IsCancellationRequested) return;
                RenderSnapshot(partial, collecting: true);
            });
            var snapshot = await _auditService.RunAsync(cancellation.Token, progress);
            if (diagnostics)
            {
                StatusText.Text = "Collecting local diagnostics…";
                snapshot = await new DiagnosticsService().RunAsync(snapshot, cancellation.Token);
            }
            snapshot = WithHistory(snapshot);
            _snapshot = snapshot;
            if (_closed) return;
            RenderSnapshot(snapshot, collecting: false);
        }
        catch (OperationCanceledException)
        {
            if (_closed) return;
            if (_snapshot is not null) RenderSnapshot(_snapshot, collecting: false);
            else { _displaySnapshot = null; AppInventoryList.ItemsSource = null; CountText.Text = ""; EmptyText.Visibility = Visibility.Visible; EmptyText.Text = "Scan cancelled. Run a new scan when ready."; IntakeSummaryText.Text = "Scan cancelled. Scan this PC to prepare a workflow report."; }
            StatusText.Text = _snapshot is not null
                ? "Scan cancelled. Previous completed results retained."
                : "Scan cancelled. No completed results are available. Run a new scan when ready.";
        }
        catch (Exception ex)
        {
            if (_closed) return;
            if (_snapshot is not null) RenderSnapshot(_snapshot, collecting: false);
            else { _displaySnapshot = null; AppInventoryList.ItemsSource = null; IntakeSummaryText.Text = "No completed snapshot is available. Try Scan this PC again."; }
            StatusText.Text = $"Scan failed: {ex.Message}";
        }
        finally { _scanCancellation.Dispose(); _scanCancellation = null; SetBusy(_actionBusy); }
        await RefreshActionsAsync(RemovalCheckMode.All, refreshInventory: false);
        RefreshInventoryIfNeeded();
    }

    private void CancelScan_Click(object sender, RoutedEventArgs e)
    {
        _inventoryRefreshPending = false;
        _scanCancellation?.Cancel();
    }

    private void RefreshInventoryIfNeeded()
    {
        if (!_inventoryRefreshPending || _closed || _actionBusy || _launchTaskActive || _checkingHistory || _scanCancellation is not null) return;
        _inventoryRefreshPending = false;
        RunScan_Click(this, new RoutedEventArgs());
    }

    private void RenderSnapshot(InventorySnapshot snapshot, bool collecting)
    {
        _displaySnapshot = snapshot;
        ApplyFilters();
        StatusText.Text = $"{snapshot.Applications.Count()} inventory entries · {(collecting ? "Collecting device details…" : snapshot.IsPartial ? $"Partial {snapshot.Mode.ToLowerInvariant()} — see report coverage" : $"{snapshot.Mode} complete")} · {snapshot.Duration.TotalSeconds:N2}s";
        DiskList.ItemsSource = snapshot.Machine.Disks.Select(d => new DiskRow(d)).ToList();
        DeviceText.Text = "Disk readings unavailable";
        DeviceText.Visibility = snapshot.Machine.Disks.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        var machine = snapshot.Machine;
        IntakeSummaryText.Text = snapshot.WorkflowIntake.ToDisplayText();
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
            .Select(a => new InventoryRow(a, _actions.LastOrDefault(action => action.AppId == a.Id),
                _actionBusy || _launchTaskActive || _checkingHistory || _scanCancellation is not null || _actions.Any(action => action.ProcessState == RemovalProcessState.Running))).ToList();
        AppInventoryList.ItemsSource = rows;
        AppInventoryList.SelectedItem = rows.FirstOrDefault(a => a.App.Id == selected);
        CountText.Text = $"{rows.Count} of {_displaySnapshot.Applications.Count()} entries · {rows.Count(r => !r.App.ReportedSizeBytes.HasValue)} shown sizes unknown";
        EmptyText.Text = "No apps match these filters.";
        EmptyText.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void App_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (AppDetailsText is not null) AppDetailsText.Text = AppInventoryList.SelectedItem is InventoryRow row ? InventoryPresentation.Details(row.App) : "Select an app to inspect its installation and removal details";
    }

    private async void ExportMarkdown_Click(object sender, RoutedEventArgs e)
    {
        if (!CanPrepareReport) return;
        _reportBusy = true;
        SetBusy(false);
        try
        {
            var report = PrepareReport();
            var picker = new FileSavePicker(AppWindow.Id)
            {
                SuggestedFolder = Windows.Storage.UserDataPaths.GetDefault().Downloads,
                SuggestedStartLocation = PickerLocationId.Downloads,
                SuggestedFileName = Path.GetFileNameWithoutExtension(report.FileName)
            };
            picker.FileTypeChoices.Add("Workflow intake report", [report.Extension]);
            var file = await picker.PickSaveFileAsync();
            if (_closed) return;
            if (file is null) { StatusText.Text = "Save cancelled. Results are still available."; return; }
            // The picker obtains the user's explicit destination/overwrite decision.
            await File.WriteAllTextAsync(file.Path, report.Content);
            if (!_closed) StatusText.Text = $"Report saved: {file.Path}. You can attach it to an email to your consultant.";
        }
        catch (Exception ex) { if (!_closed) await ShowDialogAsync("Could not save report", ex.Message); }
        finally { _reportBusy = false; SetBusy(_actionBusy || _scanCancellation is not null); }
    }

    private bool CanPrepareReport => !_closed && !_reportBusy && _snapshot is not null && _scanCancellation is null && !_actionBusy;

    private PreparedIntakeReport PrepareReport()
    {
        var source = _snapshot!;
        var snapshot = source with
        {
            ClientContext = new WorkflowClientContext
            {
                Goal = WorkflowGoalBox.Text.Trim(), Services = WorkflowServicesBox.Text.Trim(),
                Constraints = WorkflowConstraintsBox.Text.Trim()
            }
        };
        return PreparedIntakeReport.Create(snapshot, (IntakeReportFormat)ReportFormat.SelectedIndex,
            IncludeRawDetailsCheckBox.IsChecked == true);
    }

    private async void ReviewReport_Click(object sender, RoutedEventArgs e) => await ReviewReportAsync(share: false);
    private async void ShareReport_Click(object sender, RoutedEventArgs e) => await ReviewReportAsync(share: true);

    private async Task ReviewReportAsync(bool share)
    {
        if (!CanPrepareReport) return;
        _reportBusy = true;
        SetBusy(false);
        try
        {
            var report = PrepareReport();
            var content = new StackPanel { Spacing = 12 };
            content.Children.Add(new TextBlock
            {
                Text = report.IncludesPersonalIdentifiers
                    ? "Personal identifiers are included. Review the report before choosing a recipient."
                    : "Known computer/user identifiers and your profile path are redacted. Installed app names and your optional notes remain in the report.",
                TextWrapping = TextWrapping.Wrap
            });
            if (_snapshot?.Mode == "Diagnostics")
                content.Children.Add(new TextBlock
                {
                    Text = "This report includes diagnostic readings such as process and startup names.",
                    TextWrapping = TextWrapping.Wrap
                });
            var preview = new StackPanel { Spacing = 12 };
            preview.Children.Add(new TextBlock
            {
                Text = report.ReviewSummary, TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = true
            });
            preview.Children.Add(new Expander
            {
                Header = $"Complete report ({report.Extension.TrimStart('.')})",
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                Content = new TextBlock { Text = report.Content, TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = true, FontSize = 13 }
            });
            content.Children.Add(new ScrollViewer { MaxHeight = 400, Content = preview });
            if (share) content.Children.Add(new TextBlock
            {
                Text = "Choose a sharing app and recipient in Windows. If your email app is unavailable, use Download report and attach the file to an email.",
                TextWrapping = TextWrapping.Wrap
            });
            var dialog = new ContentDialog
            {
                XamlRoot = RootGrid.XamlRoot, Title = "Review your workflow report", Content = content,
                CloseButtonText = share ? "Cancel" : "Close",
                PrimaryButtonText = share ? "Choose sharing app" : "", DefaultButton = ContentDialogButton.Close
            };
            if (await dialog.ShowAsync() != ContentDialogResult.Primary || !share || _closed) return;
            _reportShare ??= new ReportShareService(WinRT.Interop.WindowNative.GetWindowHandle(this));
            await _reportShare.ShowAsync(report);
            if (!_closed) StatusText.Text = "Report handed to Windows sharing. Choose your recipient there; delivery is not verified by Installed.";
        }
        catch (Exception ex)
        {
            if (!_closed) await ShowDialogAsync("Could not open report sharing", $"{ex.Message}\nUse Download report and attach the saved file to your email.");
        }
        finally { _reportBusy = false; SetBusy(_actionBusy || _scanCancellation is not null); }
    }

    private void SetBusy(bool busy)
    {
        if (_closed) return;
        RunButton.IsEnabled = !busy;
        DiagnosticsButton.IsEnabled = !busy;
        CancelButton.IsEnabled = _scanCancellation is not null && !_actionBusy;
        DownloadReportButton.IsEnabled = !busy && !_reportBusy && _snapshot is not null;
        ReviewReportButton.IsEnabled = DownloadReportButton.IsEnabled;
        ShareReportButton.IsEnabled = DownloadReportButton.IsEnabled;
        ScanProgress.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        CheckStatusButton.IsEnabled = !busy && !_checkingHistory && !_launchTaskActive && (_actions.Count > 0 || _historyWarning is not null);
        ApplyFilters();
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        if (_loaded) return;
        _loaded = true;
        try
        {
            _actions = await _removalService.ReadHistoryAsync();
            if (_closed) return;
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
        if (_closed) return;
        _statusTimer.Start();
        await RefreshActionsAsync(RemovalCheckMode.Unresolved);
        // Opening the client is sufficient to get a read-only intake snapshot.
        if (!_closed && !_scanStarted) RunScan_Click(this, new RoutedEventArgs());
    }

    private async void CheckStatus_Click(object sender, RoutedEventArgs e) => await RefreshActionsAsync(RemovalCheckMode.All);

    private async Task RefreshActionsAsync(RemovalCheckMode mode, bool refreshInventory = true)
    {
        if (_closed || !_loaded || _checkingHistory || _launchTaskActive || _actionBusy || _scanCancellation is not null) return;
        _checkingHistory = true;
        SetBusy(false);
        try
        {
            var previous = _actions;
            var actions = await _removalService.ReconcileAsync(mode, _lifetime.Token);
            if (_closed) return;
            _actions = actions;
            _historyWarning = null;
            RenderHistory();
            if (_snapshot is not null) _snapshot = WithHistory(_snapshot);
            ApplyFilters();
            var newlyAbsent = InventoryPresentation.HasNewlyAbsentAction(previous, actions);
            if (refreshInventory && newlyAbsent && _snapshot is not null) _inventoryRefreshPending = true;
        }
        catch (OperationCanceledException) when (_closed) { }
        catch (Exception ex)
        {
            _historyWarning = new ProbeStatus { Name = "Action history", State = ProbeState.Failed, Message = "Could not check action history: " + ex.Message };
            if (!_closed) { RenderHistory(); if (_snapshot is not null) _snapshot = WithHistory(_snapshot); }
        }
        finally { _checkingHistory = false; SetBusy(_actionBusy || _scanCancellation is not null); RefreshInventoryIfNeeded(); }
    }

    private void UpdateAction(RemovalRecord action)
    {
        if (_actions.Any(a => a.Id == action.Id && a.Revision > action.Revision)) return;
        if (_snapshot is not null && InventoryPresentation.HasNewlyAbsentAction(_actions, [action])) _inventoryRefreshPending = true;
        _actions.RemoveAll(a => a.Id == action.Id);
        _actions.Add(action);
        _actions = _actions.OrderBy(a => a.StartedAt).ToList();
        RenderHistory();
        if (_snapshot is not null) _snapshot = WithHistory(_snapshot);
        ApplyFilters();
    }

    private InventorySnapshot WithHistory(InventorySnapshot snapshot, ProbeStatus? warning = null) => snapshot with
    {
        ProbeStatuses = snapshot.ProbeStatuses.Where(p => p.Name != "Action history")
            .Concat(new[] { warning, _historyWarning }.OfType<ProbeStatus>()).ToList(), Actions = _actions.ToList()
    };

    private void RenderHistory()
    {
        var unsaved = _actions.FirstOrDefault(a => a.PersistenceWarning is not null)?.PersistenceWarning;
        if (unsaved is not null) _historyWarning = new ProbeStatus { Name = "Action history", State = ProbeState.Failed, Message = unsaved };
        var pending = _actions.Count(RemovalTracking.NeedsFollowUp);
        ActionStatusText.Text = _historyWarning?.Message ?? (pending == 0 ? "" :
            $"{pending} action(s) awaiting verification. Use Check status to refresh; this never starts another uninstall.");
        ActionStatusText.Visibility = ActionStatusText.Text.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
        HistoryText.Text = _historyWarning is not null ? _historyWarning.Message : _actions.Count == 0 ? "No recorded actions." : string.Join("\n\n", _actions.AsEnumerable().Reverse().Select(a =>
            $"{a.StartedAt:g} · {a.AppName} · {a.StatusDisplay}\n{a.ExecutionDetail ?? a.Detail}\nOriginal result: {a.Outcome} · Checked: {a.LastCheckedAt?.ToLocalTime().ToString("g") ?? "Not yet"}\n{(a.DiskChangeDisplay.Length == 0 ? "Disk change unknown" : a.DiskChangeDisplay)}\nIdentity: {a.AppId}"));
    }

    private async void Remove_Click(object sender, RoutedEventArgs e)
    {
        if (_closed || _actionBusy || _launchTaskActive || _checkingHistory || _scanCancellation is not null || _snapshot is null || sender is not Button { DataContext: InventoryRow row }) return;
        _actionBusy = true;
        _launchTaskActive = true;
        SetBusy(true);
        StatusText.Text = "Checking this installation and its removal route…";
        try
        {
            PreparedRemoval plan;
            try { plan = await _removalService.PrepareAsync(row.App.Id, cancellationToken: _lifetime.Token); }
            catch (RemovalRetryRequiredException)
            {
                var finished = new CheckBox { Content = "I have checked that the previous uninstaller has finished." };
                var recovery = new ContentDialog { Title = "Review the previous uninstall", Content = finished,
                    PrimaryButtonText = "Prepare another attempt", IsPrimaryButtonEnabled = false,
                    SecondaryButtonText = "Review in Windows", CloseButtonText = "Cancel", DefaultButton = ContentDialogButton.Close, XamlRoot = Content.XamlRoot };
                finished.Checked += (_, _) => recovery.IsPrimaryButtonEnabled = true;
                finished.Unchecked += (_, _) => recovery.IsPrimaryButtonEnabled = false;
                var decision = await recovery.ShowAsync();
                if (decision == ContentDialogResult.None) { StatusText.Text = "Retry cancelled. No new uninstall was started."; return; }
                plan = await _removalService.PrepareAsync(row.App.Id,
                    previousUninstallerFinished: decision == ContentDialogResult.Primary && finished.IsChecked == true,
                    reviewInWindows: decision == ContentDialogResult.Secondary, cancellationToken: _lifetime.Token);
            }
            var handoff = plan.Command.Route == RemovalRoute.WindowsSettings;
            var admin = new CheckBox { Content = "Request administrator approval", IsEnabled = plan.App.Scope == InstallationScope.AllUsers && plan.Command.Route is RemovalRoute.Msi or RemovalRoute.Vendor,
                Visibility = plan.App.Scope == InstallationScope.AllUsers && plan.Command.Route is RemovalRoute.Msi or RemovalRoute.Vendor ? Visibility.Visible : Visibility.Collapsed };
            var content = new StackPanel { Spacing = 12, MaxWidth = 560 };
            content.Children.Add(new TextBlock { Text = InventoryPresentation.Details(plan.App), TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = true });
            content.Children.Add(new TextBlock { Text = handoff ? "This opens Windows Installed Apps. Installed will not remove the app or claim removal succeeded." :
                "The app may close and its local data may be deleted. Save your work first. Windows or the vendor may request administrator approval. Installed will not request a restart. Cancel in the vendor or Windows dialog where supported; Store removal cannot be cancelled once started.", TextWrapping = TextWrapping.Wrap });
            if (plan.Command.Executable.Length > 0) content.Children.Add(new TextBlock { Text = "Uninstaller: " + plan.Command.Executable, TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = true });
            content.Children.Add(admin);
            var dialog = new ContentDialog { Title = handoff ? "Review this app in Windows?" : "Uninstall this app?",
                Content = new ScrollViewer { Content = content, MaxHeight = 440 }, PrimaryButtonText = handoff ? "Open Windows" : "Uninstall",
                CloseButtonText = "Cancel", DefaultButton = ContentDialogButton.Close, XamlRoot = Content.XamlRoot };
            var approved = await dialog.ShowAsync() == ContentDialogResult.Primary;
            StatusText.Text = approved ? "Removal in progress. Complete any Windows or vendor approval dialog…" : "Cancelling…";
            var result = await _removalService.ExecuteAsync(plan.Token, approved, admin.IsChecked == true);
            if (_closed) return;
            UpdateAction(result);
            if (_scanCancellation is null) StatusText.Text = $"{result.AppName}: {result.StatusDisplay}. {result.ExecutionDetail ?? result.Detail}";
        }
        catch (OperationCanceledException) when (_closed) { }
        catch (Exception ex) { if (!_closed) { StatusText.Text = "Removal did not complete: " + ex.Message; await ShowDialogAsync("Removal unavailable", ex.Message); } }
        finally { _actionBusy = false; _launchTaskActive = false; SetBusy(_scanCancellation is not null); RefreshInventoryIfNeeded(); }
    }

    private async Task ShowDialogAsync(string title, string message) =>
        await new ContentDialog { Title = title, Content = message, CloseButtonText = "OK", XamlRoot = Content.XamlRoot }.ShowAsync();
}

// Bar proportions for the drive gauge; kept out of InventoryPresentation so tests compile without WinUI.
public sealed partial class DiskRow
{
    public GridLength UsedWidth => new(UsedPercent, GridUnitType.Star);
    public GridLength FreeWidth => new(100 - UsedPercent, GridUnitType.Star);
}
