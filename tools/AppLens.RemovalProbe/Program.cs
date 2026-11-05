using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text.Json;
using Microsoft.Win32;
using Windows.ApplicationModel;
using Windows.Management.Deployment;

// Development-only probe: generated fixture identities, never imported scan entries.
var plan = JsonSerializer.Deserialize<ProbePlan>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "probe-plan.json")))!;
if (!Guid.TryParseExact(plan.RunId, "N", out _) || !Guid.TryParse(plan.ProductCode, out _) ||
    !plan.FixtureName.StartsWith("CSI.AppLens.Disposable.", StringComparison.Ordinal) ||
    !plan.CallerName.StartsWith("CSI.AppLens.RemovalProbe.", StringComparison.Ordinal))
    throw new InvalidDataException("Invalid fixture plan.");
var fixtureRoot = Path.GetFullPath(plan.FixtureDirectory);
if (!File.Exists(Path.Combine(fixtureRoot, plan.RunId + ".fixture")))
    throw new InvalidDataException("Missing fixture ownership marker.");
var vendorKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\AppLens.Probe." + plan.RunId;

if (args.Contains("--remove-vendor"))
{
    using var key = Registry.CurrentUser.OpenSubKey(vendorKey);
    if (key?.GetValue("AppLensProbe")?.ToString() != plan.RunId) return;
    key.Close();
    Registry.CurrentUser.DeleteSubKeyTree(vendorKey, false);
    return;
}

var results = new List<object>();
var resultPath = Path.Combine(fixtureRoot, "results.json");
if (File.Exists(resultPath)) throw new InvalidOperationException("Probe already ran; preserve its evidence and build a fresh fixture.");
void Record(object result)
{
    results.Add(result);
    File.WriteAllText(resultPath, JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }));
}
try
{
    var current = Package.Current;
    if (current.Id.Name == plan.FixtureName) return;
    if (current.Id.Name != plan.CallerName) throw new InvalidOperationException("Caller package identity mismatch.");
    var administrator = new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);
    Record(new { Step = "Caller identity", current.Id.Name, current.Id.Publisher, Administrator = administrator });
    if (administrator) throw new InvalidOperationException("Probe must run as the ordinary, unelevated user.");
    var manager = new PackageManager();
    var fixture = manager.FindPackagesForUser("").SingleOrDefault(p => p.Id.Name == plan.FixtureName)
        ?? throw new InvalidOperationException("Disposable MSIX package is not registered for this user.");
    if (fixture.Id.Publisher == current.Id.Publisher) throw new InvalidOperationException("Different-publisher fixture required.");
    var removal = await manager.RemovePackageAsync(fixture.Id.FullName);
    Record(new { Step = "Current-user MSIX removal", Error = removal.ExtendedErrorCode?.HResult,
        removal.ErrorText, StillInstalled = manager.FindPackagesForUser("").Any(p => p.Id.Name == plan.FixtureName) });
    using (var key = Registry.CurrentUser.CreateSubKey(vendorKey))
    {
        key.SetValue("DisplayName", "AppLens disposable vendor fixture");
        key.SetValue("AppLensProbe", plan.RunId);
        key.SetValue("UninstallString", "\"" + Environment.ProcessPath + "\" --remove-vendor");
    }
    var vendor = await Run(Environment.ProcessPath!, ["--remove-vendor"], false);
    using (var remaining = Registry.CurrentUser.OpenSubKey(vendorKey))
        Record(new { Step = "Vendor executable removal", ExitCode = vendor, StillInstalled = remaining is not null });
    if (MsiQueryProductState(plan.ProductCode) is not (-1 or 2))
        throw new InvalidOperationException("MSI fixture already registered; refusing to assume ownership.");
    var msi = Path.Combine(fixtureRoot, "fixture.msi");
    if (!File.Exists(msi)) throw new FileNotFoundException("Missing generated MSI fixture.");
    var msiExe = Path.Combine(Environment.SystemDirectory, "msiexec.exe");
    var installed = await Run(msiExe, ["/i", msi, "/qn", "/norestart"], false);
    Record(new { Step = "Install per-user MSI fixture", ExitCode = installed, State = MsiQueryProductState(plan.ProductCode) });
    if (installed != 0) throw new InvalidOperationException("MSI fixture installation failed.");
    var removed = await Run(msiExe, ["/x", plan.ProductCode, "/qn", "/norestart"], false);
    Record(new { Step = "Per-user MSI removal", ExitCode = removed, State = MsiQueryProductState(plan.ProductCode) });
    installed = await Run(msiExe, ["/i", msi, "/qn", "/norestart"], false);
    if (installed != 0) throw new InvalidOperationException("MSI fixture reinstallation failed.");
    Record(new { Step = "Administrator handoff pending", Detail = "Windows UAC approval is required for this explicit test." });
    try
    {
        removed = await Run(msiExe, ["/x", plan.ProductCode, "/passive", "/norestart"], true);
        Record(new { Step = "Elevated MSI removal handoff", ExitCode = removed, State = MsiQueryProductState(plan.ProductCode) });
    }
    catch (System.ComponentModel.Win32Exception ex)
    {
        Record(new { Step = "Elevated MSI handoff unavailable", ex.NativeErrorCode, ex.Message });
    }
}
catch (Exception ex) { Record(new { Step = "Probe failed", Error = ex.GetType().Name, ex.HResult, ex.Message }); }
finally { File.WriteAllText(Path.Combine(fixtureRoot, "probe-complete.txt"), DateTimeOffset.Now.ToString("O")); }

static async Task<int> Run(string executable, string[] arguments, bool elevated)
{
    var start = new ProcessStartInfo(executable) { UseShellExecute = elevated, CreateNoWindow = !elevated };
    if (elevated) start.Verb = "runas";
    foreach (var argument in arguments) start.ArgumentList.Add(argument);
    using var process = Process.Start(start) ?? throw new InvalidOperationException("Process did not start.");
    await process.WaitForExitAsync().WaitAsync(TimeSpan.FromMinutes(3));
    return process.ExitCode;
}
[DllImport("msi.dll", CharSet = CharSet.Unicode)]
static extern int MsiQueryProductState(string product);
sealed class ProbePlan
{
    public string RunId { get; set; } = "";
    public string CallerName { get; set; } = "";
    public string FixtureName { get; set; } = "";
    public string FixtureDirectory { get; set; } = "";
    public string ProductCode { get; set; } = "";
}
