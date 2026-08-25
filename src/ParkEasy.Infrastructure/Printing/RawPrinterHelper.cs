using System.Runtime.InteropServices;

namespace ParkEasy.Infrastructure.Printing;

/// <summary>
/// P/Invoke helper for sending raw data directly to a Windows printer via the spooler.
/// Used by thermal printer implementations (Bematech MP-4200 TH) to send ESC/POS commands.
/// </summary>
public static partial class RawPrinterHelper
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DOCINFOW
    {
        [MarshalAs(UnmanagedType.LPWStr)]
        public string pDocName;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string? pOutputFile;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string pDataType;
    }

    [LibraryImport("winspool.drv", EntryPoint = "OpenPrinterW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool OpenPrinter(string pPrinterName, out IntPtr phPrinter, IntPtr pDefault);

    [LibraryImport("winspool.drv", EntryPoint = "ClosePrinter", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool ClosePrinter(IntPtr hPrinter);

    [DllImport("winspool.drv", EntryPoint = "StartDocPrinterW", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern int StartDocPrinter(IntPtr hPrinter, int level, ref DOCINFOW pDocInfo);

    [LibraryImport("winspool.drv", EntryPoint = "EndDocPrinter", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool EndDocPrinter(IntPtr hPrinter);

    [LibraryImport("winspool.drv", EntryPoint = "StartPagePrinter", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool StartPagePrinter(IntPtr hPrinter);

    [LibraryImport("winspool.drv", EntryPoint = "EndPagePrinter", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool EndPagePrinter(IntPtr hPrinter);

    [LibraryImport("winspool.drv", EntryPoint = "WritePrinter", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool WritePrinter(IntPtr hPrinter, IntPtr pBytes, int dwCount, out int dwWritten);

    /// <summary>
    /// Sends raw bytes to the named printer.
    /// </summary>
    public static bool SendBytesToPrinter(string printerName, byte[] data, string documentName = "ParkEasy Ticket")
    {
        var printerHandle = IntPtr.Zero;

        var docInfo = new DOCINFOW
        {
            pDocName = documentName,
            pOutputFile = null,
            pDataType = "RAW"
        };

        bool success = false;

        if (OpenPrinter(printerName, out printerHandle, IntPtr.Zero))
        {
            if (StartDocPrinter(printerHandle, 1, ref docInfo) != 0)
            {
                if (StartPagePrinter(printerHandle))
                {
                    IntPtr pUnmanagedBytes = Marshal.AllocCoTaskMem(data.Length);
                    try
                    {
                        Marshal.Copy(data, 0, pUnmanagedBytes, data.Length);
                        success = WritePrinter(printerHandle, pUnmanagedBytes, data.Length, out _);
                    }
                    finally
                    {
                        Marshal.FreeCoTaskMem(pUnmanagedBytes);
                    }

                    EndPagePrinter(printerHandle);
                }
                EndDocPrinter(printerHandle);
            }
            ClosePrinter(printerHandle);
        }

        return success;
    }
}
