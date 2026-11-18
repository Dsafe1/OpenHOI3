using System;

namespace OpenHOI3.Launcher;

/// <summary>Steam's own folder on this machine, as Steam writes it in the registry; null off Windows or without Steam.</summary>
public static class CSteamFolder
{
	public static string? Find()
	{
		if (!OperatingSystem.IsWindows())
		{
			return null;
		}
		try
		{
			return Microsoft.Win32.Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath", null) as string;
		}
		catch (Exception exception) when (exception is System.Security.SecurityException or UnauthorizedAccessException)
		{
			return null;
		}
	}
}
