using System.Globalization;

namespace OpenHOI3.Sim.Multiplayer;

/// <summary>
/// An address a player types to join a host, read into the host and the port (<c>docs/multiplayer.md</c>, section 6):
/// a name or an IPv4 address, which may end in a colon and a port; or an IPv6 address - bare, where every colon is
/// the address's own, or in square brackets, which may be followed by a colon and a port.
/// </summary>
public static class CNetAddress
{
	/// <summary>The host and the port; the port given where the text names none.</summary>
	public static (string Host, int Port) Parse(string text, int defaultPort = CLineLink.DefaultPort)
	{
		text = text.Trim();
		if (text.StartsWith('['))
		{
			// [address] or [address]:port.
			int close = text.IndexOf(']');
			if (close > 0)
			{
				string rest = text[(close + 1)..];
				return (text[1..close], rest.StartsWith(':') && TryPort(rest[1..], out int bracketed) ? bracketed : defaultPort);
			}
			return (text, defaultPort);
		}
		int colon = text.LastIndexOf(':');
		// More than one colon and no brackets: an IPv6 address, whole.
		if (colon > 0 && text.IndexOf(':') == colon && TryPort(text[(colon + 1)..], out int port))
		{
			return (text[..colon], port);
		}
		return (text, defaultPort);
	}

	private static bool TryPort(string text, out int port)
	{
		return int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out port) && port is > 0 and <= 65535;
	}
}
