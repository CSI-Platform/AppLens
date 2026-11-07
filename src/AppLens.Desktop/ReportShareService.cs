using System.Runtime.InteropServices;
using System.Text;
using AppLens.Backend;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Streams;

namespace AppLens.Desktop;

/// <summary>Hands an explicitly reviewed report to Windows; never chooses a recipient or sends mail.</summary>
internal sealed class ReportShareService : IDisposable
{
    [ComImport, Guid("3A3DCD6C-3EAB-43DC-BCDE-45671CE800C8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IDataTransferManagerInterop
    {
        IntPtr GetForWindow(IntPtr window, ref Guid iid);
        void ShowShareUIForWindow(IntPtr window);
    }

    private readonly IntPtr _window;
    private readonly IDataTransferManagerInterop _interop;
    private readonly DataTransferManager _manager;
    private StorageFile? _file;

    public ReportShareService(IntPtr window)
    {
        _window = window;
        _interop = DataTransferManager.As<IDataTransferManagerInterop>();
        var iid = new Guid("A5CAEE9B-8708-49D1-8D36-67D25A8DA00C");
        var pointer = _interop.GetForWindow(window, ref iid);
        try { _manager = WinRT.MarshalInterface<DataTransferManager>.FromAbi(pointer); }
        finally { Marshal.Release(pointer); }
        _manager.DataRequested += OnDataRequested;
    }

    public async Task ShowAsync(PreparedIntakeReport report)
    {
        // Stream the frozen report on demand; do not leave an extra plaintext file on disk.
        var bytes = Encoding.UTF8.GetBytes(report.Content);
        _file = await StorageFile.CreateStreamedFileAsync(report.FileName, async stream =>
        {
            try
            {
                using var writer = new DataWriter(stream);
                writer.WriteBytes(bytes);
                await writer.StoreAsync();
                await writer.FlushAsync();
                writer.DetachStream();
                stream.Dispose();
            }
            catch { stream.FailAndClose(StreamedFileFailureMode.Incomplete); }
        }, null);
        _interop.ShowShareUIForWindow(_window);
    }

    private void OnDataRequested(DataTransferManager sender, DataRequestedEventArgs args)
    {
        if (_file is null)
        {
            args.Request.FailWithDisplayText("Review a report before sharing it.");
            return;
        }
        args.Request.Data.Properties.Title = "Installed workflow intake report";
        args.Request.Data.Properties.Description = "PC specifications and installed tools for workflow planning";
        args.Request.Data.RequestedOperation = DataPackageOperation.Copy;
        args.Request.Data.SetStorageItems(new[] { _file }, readOnly: true);
    }

    public void Dispose() => _manager.DataRequested -= OnDataRequested;
}
