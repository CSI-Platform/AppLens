namespace AppLens.Backend;

public sealed class AppLensRuntimeStorage
{
    private AppLensRuntimeStorage(string root)
    {
        Root = Path.GetFullPath(root);
        LedgerDirectory = Path.Combine(Root, "ledger");
        EventsJsonl = Path.Combine(LedgerDirectory, "events.jsonl");
        IndexSqlite = Path.Combine(LedgerDirectory, "index.sqlite");

        SnapshotsDirectory = Path.Combine(Root, "snapshots");
        LatestSnapshotJson = Path.Combine(SnapshotsDirectory, "latest.json");
    }

    public string Root { get; }

    public string LedgerDirectory { get; }

    public string EventsJsonl { get; }

    public string IndexSqlite { get; }

    public string SnapshotsDirectory { get; }

    public string LatestSnapshotJson { get; }

    public static AppLensRuntimeStorage Default()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return FromRoot(Path.Combine(localAppData, "AppLens"));
    }

    public static AppLensRuntimeStorage FromRoot(string root)
    {
        if (string.IsNullOrWhiteSpace(root))
        {
            throw new ArgumentException("Runtime storage root is required.", nameof(root));
        }

        return new AppLensRuntimeStorage(root);
    }
}
