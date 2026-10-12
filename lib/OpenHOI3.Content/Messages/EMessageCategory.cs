namespace OpenHOI3.Content.Messages;

/// <summary>
/// The six categories a message type is filed under, in the original's order, which is the order
/// of the log's filters: <c>docs/mechanics/messages.md</c>, section 1.
/// </summary>
public enum EMessageCategory
{
	Combat,
	Diplomacy,
	Units,
	Province,
	Events,
	Other,
}
