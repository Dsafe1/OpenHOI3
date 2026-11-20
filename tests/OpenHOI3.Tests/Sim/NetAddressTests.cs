using OpenHOI3.Sim.Multiplayer;
using Xunit;

namespace OpenHOI3.Tests.Sim;

public class NetAddressTests
{
	[Theory]
	[InlineData("127.0.0.1", "127.0.0.1", CLineLink.DefaultPort)]
	[InlineData(" host.example ", "host.example", CLineLink.DefaultPort)]
	[InlineData("127.0.0.1:2000", "127.0.0.1", 2000)]
	[InlineData("host.example:2000", "host.example", 2000)]
	[InlineData("::1", "::1", CLineLink.DefaultPort)]
	[InlineData("2001:db8::5", "2001:db8::5", CLineLink.DefaultPort)]
	[InlineData("[::1]", "::1", CLineLink.DefaultPort)]
	[InlineData("[2001:db8::5]:2000", "2001:db8::5", 2000)]
	[InlineData("fe80::1%12", "fe80::1%12", CLineLink.DefaultPort)]
	[InlineData("host.example:", "host.example:", CLineLink.DefaultPort)]
	[InlineData("host.example:99999", "host.example:99999", CLineLink.DefaultPort)]
	[InlineData("", "", CLineLink.DefaultPort)]
	public void AnAddressIsReadIntoItsHostAndPort(string typed, string host, int port)
	{
		Assert.Equal((host, port), CNetAddress.Parse(typed));
	}
}
