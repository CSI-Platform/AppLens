namespace AppLens.Backend;

// Presentation evidence only. Execution always resolves and validates the live target again.
public sealed record RemovalAssessment(RemovalRoute Route, bool CanAct, string Explanation, string AdministratorGuidance)
{
    public string Label => Route switch
    {
        RemovalRoute.Msi => "Windows Installer",
        RemovalRoute.Vendor => "Vendor uninstaller",
        RemovalRoute.Msix => "Current user · Store",
        RemovalRoute.WindowsSettings => "Review in Windows",
        _ => "Removal restricted"
    };

    public static RemovalAssessment Assess(AppEntry app)
    {
        var command = RemovalCommand.Resolve(app);
        if (command.Route == RemovalRoute.Msix && WindowsRemovalPlatform.IsOwnPackage(app.PackageFullName))
            return new(RemovalRoute.None, false, "Installed cannot remove its own running package. Use Windows Installed Apps.", app.AdminRequirement);
        return new(command.Route, command.Route != RemovalRoute.None, command.Reason, app.AdminRequirement);
    }
}
