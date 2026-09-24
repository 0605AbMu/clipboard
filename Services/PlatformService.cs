using System;

namespace MacDesktopApp.Services;

public static class PlatformService
{
    private static IPlatformService? _current;

    public static IPlatformService Current
    {
        get
        {
            if (_current == null)
            {
                if (OperatingSystem.IsMacOS())
                {
                    _current = new MacPlatformService();
                }
                else if (OperatingSystem.IsWindows())
                {
                    _current = new WindowsPlatformService();
                }
                else
                {
                    _current = new LinuxPlatformService();
                }
            }
            return _current;
        }
    }
}
