namespace AppLens.Backend.Tests;

public sealed class RuntimeStorageTests
{
    [Fact]
    public void Explicit_root_resolves_ledger_paths()
    {
        var root = Path.Combine(Path.GetTempPath(), "AppLens-RuntimeStorageTests", Guid.NewGuid().ToString("N"));

        var storage = AppLensRuntimeStorage.FromRoot(root);

        Assert.Equal(root, storage.Root);
        Assert.Equal(Path.Combine(root, "ledger"), storage.LedgerDirectory);
        Assert.Equal(Path.Combine(root, "ledger", "events.jsonl"), storage.EventsJsonl);
        Assert.Equal(Path.Combine(root, "ledger", "index.sqlite"), storage.IndexSqlite);
    }
}
