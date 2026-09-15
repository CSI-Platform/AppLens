using System.Runtime.InteropServices;
using System.Text;

namespace AppLens.Backend;

public static class MsiInstallation
{
    // 1: current-user managed, 2: current-user unmanaged, 4: machine.
    // Multiple contexts for the same product GUID are ambiguous for msiexec /x.
    public static uint ReadUniqueContext(string product)
    {
        if (!Guid.TryParse(product, out var code)) return 0;
        var buffer = new StringBuilder(39);
        var result = MsiEnumProductsEx(code.ToString("B").ToUpperInvariant(), null, 7, 0, buffer, out var context, IntPtr.Zero, IntPtr.Zero);
        if (result != 0) return 0;
        var next = MsiEnumProductsEx(code.ToString("B").ToUpperInvariant(), null, 7, 1, buffer, out _, IntPtr.Zero, IntPtr.Zero);
        return next == 259 ? context : 0;
    }

    [DllImport("msi.dll", CharSet = CharSet.Unicode)]
    private static extern uint MsiEnumProductsEx(string product, string? userSid, uint contexts, uint index,
        StringBuilder productCode, out uint context, IntPtr sid, IntPtr sidLength);
}
