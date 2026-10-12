namespace OpenHOI3.Content.Messages;

/// <summary>
/// A kind of message and what is done with one of its kind: logged, popped up, shown on the map,
/// the clock stopped. Declared by name in <c>interface/messagetypes.txt</c>; its text is in the
/// localisation under that name. See <c>docs/mechanics/messages.md</c>, section 1.
/// </summary>
public sealed class CMessageType
{
	public CMessageType(string name)
	{
		Name = name;
	}

	/// <summary>The type that stands for one nobody declared: it logs, and nothing else.</summary>
	public static CMessageType Null { get; } = new("NULL_MESSAGE");

	public string Name { get; }

	public bool Log { get; set; } = true;

	public bool OnMap { get; set; }

	public bool Popup { get; set; }

	/// <summary>Read and kept, and read by nothing, in the original too.</summary>
	public bool Idle { get; set; }

	/// <summary>Whether a popup has two buttons, the second a "Goto".</summary>
	public bool Option { get; set; }

	/// <summary>A popup as well, and the clock stopped in a game of one player.</summary>
	public bool PausePopup { get; set; }

	public EMessageCategory Category { get; set; } = EMessageCategory.Other;

	/// <summary>Whether a message of the type opens a popup: either key does.</summary>
	public bool PopsUp => Popup || PausePopup;

	public override string ToString()
	{
		return Name;
	}
}
