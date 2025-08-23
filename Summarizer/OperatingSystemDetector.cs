using System.Runtime.InteropServices;

/// <summary>
/// 作業系統檢測器
/// 用於檢測當前運行的作業系統
/// </summary>
public class OperatingSystemDetector
{
    /// <summary>
    /// 檢查作業系統是否為 macOS
    /// </summary>
    /// <returns></returns>
    public static bool IsMacOS()
    {
        return RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
    }

    /// <summary>
    /// 檢查作業系統是否為 Windows
    /// </summary>
    /// <returns></returns>
    public static bool IsWindows()
    {
        return RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
    }

    /// <summary>
    /// 檢查作業系統是否為 Linux
    /// </summary>
    /// <returns></returns>
    public static bool IsLinux()
    {
        return RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
    }
}
