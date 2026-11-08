namespace OpenHOI3.Interface.Messages;

/// <summary>What a message's Goto does. See <c>docs/mechanics/messages.md</c>, section 4.1.</summary>
public enum EMessageGoto
{
	/// <summary>Nothing: the popup closes, and a row of the log does nothing.</summary>
	None,

	/// <summary>The map centred on the message's province, or else a capital.</summary>
	Province,

	/// <summary>The map centred on the acting capital of its first country.</summary>
	Capital,

	/// <summary>The selection cleared, and a screen opened or the map centred, by the message's type.</summary>
	Screen,

	/// <summary>As <see cref="Province"/>, the popup closing where there is nowhere to go.</summary>
	Invasion,

	/// <summary>The province's battle shown; with none the popup closes.</summary>
	Battle,

	/// <summary>The province's bombing shown; with none the popup closes.</summary>
	Bombing,
}
