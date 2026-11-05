using AppLens.Backend;
using Microsoft.Win32;
using System.Diagnostics;
using System.Text.Json;

// Explicit test entry point. Only run-specific, marker-owned registrations are targets.
if (args.Length == 4 && args[0] is "--remove" or "--child" or "--cleanup")
{
    var root = Path.GetFullPath(args[1]);
    var run = args[2]; var kind = args[3];
    Validate(root, run, kind);
    if (args[0] == "--child")
    {
        var child = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false, CreateNoWindow = true };
        foreach (var value in new[] { "--remove", root, run, kind }) child.ArgumentList.Add(value);
        using var process = Process.Start(child) ?? throw new InvalidOperationException("Child did not start.");
        return 0;
    }
    if (kind == "failure" && args[0] != "--cleanup") return 13;
    await Task.Delay(args[0] == "--cleanup" ? TimeSpan.FromSeconds(1) : kind == "slow" ? TimeSpan.FromSeconds(185) : kind == "delayed" ? TimeSpan.FromSeconds(6) : TimeSpan.FromSeconds(1));
    var key = Key(run, kind);
    using (var registration = Registry.CurrentUser.OpenSubKey(key))
        if (registration?.GetValue("AppLensReliabilityFixture")?.ToString() != run) throw new InvalidOperationException("Fixture ownership mismatch.");
    Registry.CurrentUser.DeleteSubKey(key, throwOnMissingSubKey: false);
    return 0;
}
if (args.Length != 2 || args[0] is not ("--vendor" or "--handoff" or "--native" or "--msi" or "--msi-retry" or "--store" or "--status" or "--history" or "--cleanup-caller"))
    throw new ArgumentException("Use --vendor <new evidence directory>, --handoff <new evidence directory>, or --native <generated development plan>.");

if (args[0] is "--native" or "--msi" or "--msi-retry" or "--store" or "--status" or "--history" or "--cleanup-caller")
{
    await NativeAsync(Path.GetFullPath(args[1]), args[0]);
    return 0;
}
var output = Path.GetFullPath(args[1]);
if (Directory.Exists(output)) throw new IOException("Refusing to reuse an evidence directory.");
Directory.CreateDirectory(output);
var runId = Guid.NewGuid().ToString("N");
await File.WriteAllTextAsync(Path.Combine(output, "ownership.txt"), runId);
// A distinct executable keeps the application's self-uninstall guard intact.
File.Copy(Environment.ProcessPath!, Helper(runId), overwrite: false);
var store = new ProtectedRemovalHistoryStore(AppLensRuntimeStorage.FromRoot(Path.Combine(output, "history")));
RemovalService Service() => new(new WindowsRemovalPlatform(), store);

if (args[0] == "--handoff")
{
    Register(output, runId, "fast");
    var service = Service();
    var plan = await service.PrepareAsync(Id(runId, "fast"), reviewInWindows: true);
    var result = await service.ExecuteAsync(plan.Token, true);
    Require(result.ExecutionOutcome == "Handed off to Windows", "Windows Settings did not open.");
    Emit(output, "Windows handoff", result);
    var cleanup = await service.ExecuteAsync((await service.PrepareAsync(Id(runId, "fast"))).Token, true);
    await WaitAsync(service, cleanup.Id, true);
    return 0;
}

foreach (var kind in new[] { "fast", "delayed", "failure", "slow" })
{
    Register(output, runId, kind);
    var service = Service();
    if (kind == "fast")
    {
        var cancelled = await service.ExecuteAsync((await service.PrepareAsync(Id(runId, kind))).Token, false);
        Require(cancelled.Outcome == "Cancelled" && Present(runId, kind), "Declined confirmation changed the fixture.");
        Emit(output, "Declined confirmation", cancelled);
    }
    var watch = Stopwatch.StartNew();
    var action = await service.ExecuteAsync((await service.PrepareAsync(Id(runId, kind))).Token, true);
    Require(watch.Elapsed < TimeSpan.FromSeconds(15), "Launch blocked the caller too long.");
    Emit(output, kind + " launch", action);
    if (kind == "slow")
    {
        await Task.Delay(TimeSpan.FromSeconds(181));
        service = Service(); // Recover using only durable history and exact PID/start time.
        var recovered = (await service.ReconcileAsync()).Single(a => a.Id == action.Id);
        Require(recovered.ProcessState == RemovalProcessState.Running, "Slow process was lost after reopening.");
        bool blocked = false;
        try { await service.PrepareAsync(Id(runId, kind), previousUninstallerFinished: true); }
        catch (InvalidOperationException) { blocked = true; }
        Require(blocked, "Running process allowed a second launch.");
        Emit(output, "slow recovered after three minutes", recovered);
    }
    var final = await WaitAsync(service, action.Id, kind != "failure");
    Require(kind != "failure" || final.ExitCode == 13 && final.ExecutionOutcome == "Failed", "Failure evidence was lost.");
    Emit(output, kind + " verified", final);
    if (kind == "failure")
    {
        // Change only this marker-owned fixture's command to remove its registration.
        using var registration = Registry.CurrentUser.OpenSubKey(Key(runId, kind), writable: true)!;
        registration.SetValue("UninstallString", Command(output, runId, "fast", kind));
        registration.Close();
        var cleanup = await service.ExecuteAsync((await service.PrepareAsync(Id(runId, kind))).Token, true);
        // Cleanup still verifies the run-specific ownership marker before removing this key.
        await WaitAsync(service, cleanup.Id, true);
    }
}
var history = await Service().ReadHistoryAsync();
using var report = JsonDocument.Parse(new ReportWriter().WriteJson(new InventorySnapshot { Actions = history }));
Require(report.RootElement.GetProperty("Actions").GetArrayLength() == history.Count, "Report lost action records.");
Console.WriteLine(JsonSerializer.Serialize(new { Result = "PASS", Actions = history.Count, Evidence = output }));
return 0;

static string Key(string run, string kind) => InventoryCollector.UninstallKey + "\\AppLens.Reliability." + run + "." + kind;
static string Id(string run, string kind) => "registry:CurrentUser:Default:AppLens.Reliability." + run + "." + kind;
static string Helper(string run) => Path.Combine(AppContext.BaseDirectory, "AppLens.ReliabilityFixture." + run + ".exe");
static void Validate(string root, string run, string kind)
{
    if (!Guid.TryParseExact(run, "N", out _) || kind is not ("fast" or "delayed" or "failure" or "slow") ||
        File.ReadAllText(Path.Combine(root, "ownership.txt")) != run) throw new InvalidDataException("Invalid fixture identity.");
}
static bool Present(string run, string kind) { using var key = Registry.CurrentUser.OpenSubKey(Key(run, kind)); return key is not null; }
static string Command(string root, string run, string kind, string? targetKind = null) =>
    $"\"{Helper(run)}\" {(targetKind is not null ? "--cleanup" : kind == "delayed" ? "--child" : "--remove")} \"{root}\" {run} {targetKind ?? kind}";
static void Register(string root, string run, string kind)
{
    Validate(root, run, kind);
    if (Present(run, kind)) throw new InvalidOperationException("Fixture already registered.");
    using var key = Registry.CurrentUser.CreateSubKey(Key(run, kind));
    key.SetValue("AppLensReliabilityFixture", run);
    key.SetValue("DisplayName", "AppLens reliability " + kind + " fixture");
    key.SetValue("DisplayVersion", "1.0"); key.SetValue("Publisher", "CSI development tests");
    key.SetValue("UninstallString", Command(root, run, kind));
}
static async Task<RemovalRecord> WaitAsync(RemovalService service, string id, bool absent)
{
    for (var i = 0; i < 600; i++)
    {
        var action = (await service.ReconcileAsync(RemovalCheckMode.All)).Single(a => a.Id == id);
        if (action.ProcessState == RemovalProcessState.Exited && action.Verification == (absent ? RemovalVerification.Absent : RemovalVerification.Present)) return action;
        if (i % 30 == 29) Console.WriteLine(JsonSerializer.Serialize(new { Step = "Waiting for native completion", Seconds = i + 1,
            action.ExecutionOutcome, action.ExecutionDetail, action.ProcessState, action.Verification }));
        await Task.Delay(1000);
    }
    throw new InvalidOperationException("Fixture did not reach its expected verified state.");
}
static void Emit(string root, string step, RemovalRecord action)
{
    var line = JsonSerializer.Serialize(new { Step = step, action.Id, action.Outcome, action.ExecutionOutcome, action.ExitCode,
        action.ExecutionDetail, action.ProcessState, action.Verification, action.Revision, action.PersistenceWarning });
    File.AppendAllText(Path.Combine(root, "checks.jsonl"), line + Environment.NewLine);
    Console.WriteLine(line);
    Require(action.PersistenceWarning is null, "Fixture history failed to persist.");
}
static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }

static async Task NativeAsync(string planFile, string mode)
{
    // Existing builder supplies exact generated MSI/MSIX identities; never accept an app name from a report.
    using var plan = JsonDocument.Parse(await File.ReadAllTextAsync(planFile));
    var root = Path.GetDirectoryName(planFile)!;
    using var probe = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(root, "Caller", "probe-plan.json")));
    var run = probe.RootElement.GetProperty("RunId").GetString()!;
    var fixtureRoot = probe.RootElement.GetProperty("FixtureDirectory").GetString()!;
    Require(Guid.TryParseExact(run, "N", out _) && File.Exists(Path.Combine(fixtureRoot, run + ".fixture")), "Native fixture ownership missing.");
    var productCode = probe.RootElement.GetProperty("ProductCode").GetString()!;
    Require(Guid.TryParse(productCode, out _), "Invalid MSI fixture identity.");
    var fixtureName = probe.RootElement.GetProperty("FixtureName").GetString()!;
    Require(fixtureName == "CSI.AppLens.Disposable." + run[..16], "Invalid Store fixture identity.");
    var service = new RemovalService(new WindowsRemovalPlatform(), new ProtectedRemovalHistoryStore(AppLensRuntimeStorage.FromRoot(Path.Combine(root, "reliability-history"))));
    if (mode == "--history")
    {
        foreach (var action in await service.ReadHistoryAsync()) Console.WriteLine(JsonSerializer.Serialize(new
        { action.AppName, action.Outcome, action.ExecutionOutcome, action.ExecutionDetail, action.ProcessState,
            action.Verification, action.ExecutionCompletedAt, action.LastCheckedAt, action.OperationId }));
        return;
    }
    if (mode == "--status")
    {
        foreach (var action in await service.ReconcileAsync(RemovalCheckMode.All)) Emit(root, "Native status", action);
        return;
    }
    var snapshot = await new AuditService().RunAsync();
    var callerName = "CSI.AppLens.RemovalProbe." + run[..16];
    var targets = snapshot.Applications.Where(a => mode == "--cleanup-caller" ?
        a.PackageFullName.StartsWith(callerName + "_", StringComparison.Ordinal) :
        (mode != "--store" && a.ProductCode.Equals(productCode, StringComparison.OrdinalIgnoreCase)) ||
        (mode is not ("--msi" or "--msi-retry") && a.PackageFullName.StartsWith(fixtureName + "_", StringComparison.Ordinal))).ToList();
    Require(targets.Count == (mode == "--native" ? 2 : 1), "The requested exact fixture is not installed.");
    foreach (var target in targets)
    {
        var cancelled = await service.ExecuteAsync((await service.PrepareAsync(target.Id, previousUninstallerFinished: mode == "--msi-retry")).Token, false);
        Require(cancelled.Outcome == "Cancelled", "Cancellation failed.");
        var action = await service.ExecuteAsync((await service.PrepareAsync(target.Id, previousUninstallerFinished: mode == "--msi-retry")).Token, true);
        var final = await WaitAsync(service, action.Id, true);
        Emit(root, target.CandidateRoute + " verified", final);
    }
}
