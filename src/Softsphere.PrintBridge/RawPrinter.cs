using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Softsphere.PrintBridge;

internal static class RawPrinter
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private sealed class DocInfo
    {
        [MarshalAs(UnmanagedType.LPWStr)] public string DocumentName = string.Empty;
        [MarshalAs(UnmanagedType.LPWStr)] public string? OutputFile;
        [MarshalAs(UnmanagedType.LPWStr)] public string DataType = "RAW";
    }

    [DllImport("winspool.drv", EntryPoint = "OpenPrinterW", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool OpenPrinter(string printerName, out nint printerHandle, nint defaults);

    [DllImport("winspool.drv", EntryPoint = "ClosePrinter", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ClosePrinter(nint printerHandle);

    [DllImport("winspool.drv", EntryPoint = "StartDocPrinterW", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern int StartDocPrinter(nint printerHandle, int level, [In] DocInfo documentInfo);

    [DllImport("winspool.drv", EntryPoint = "EndDocPrinter", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EndDocPrinter(nint printerHandle);

    [DllImport("winspool.drv", EntryPoint = "StartPagePrinter", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool StartPagePrinter(nint printerHandle);

    [DllImport("winspool.drv", EntryPoint = "EndPagePrinter", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EndPagePrinter(nint printerHandle);

    [DllImport("winspool.drv", EntryPoint = "WritePrinter", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool WritePrinter(nint printerHandle, byte[] buffer, int count, out int written);

    public static void Send(string printerName, byte[] payload, string documentName)
    {
        if (string.IsNullOrWhiteSpace(printerName))
        {
            throw new InvalidOperationException("No Windows printer is configured.");
        }

        if (!OpenPrinter(printerName, out var printerHandle, 0))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), $"Cannot open printer '{printerName}'.");
        }

        try
        {
            var docInfo = new DocInfo { DocumentName = documentName };
            if (StartDocPrinter(printerHandle, 1, docInfo) == 0)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Cannot start printer document.");
            }

            try
            {
                if (!StartPagePrinter(printerHandle))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "Cannot start printer page.");
                }

                try
                {
                    if (!WritePrinter(printerHandle, payload, payload.Length, out var written) || written != payload.Length)
                    {
                        throw new Win32Exception(Marshal.GetLastWin32Error(), "The printer did not accept the complete document.");
                    }
                }
                finally
                {
                    EndPagePrinter(printerHandle);
                }
            }
            finally
            {
                EndDocPrinter(printerHandle);
            }
        }
        finally
        {
            ClosePrinter(printerHandle);
        }
    }
}
