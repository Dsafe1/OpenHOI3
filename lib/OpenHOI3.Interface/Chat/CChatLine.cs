namespace OpenHOI3.Interface.Chat;

/// <summary>
/// One line of the chat in play as a machine holds it (<c>docs/mechanics/chat.md</c>, section 4).
/// </summary>
/// <param name="Kind">What the line is.</param>
/// <param name="Channel">The channel it is shown in.</param>
/// <param name="Name">The player it is by - of a whisper sent, the player it went to; of a system line, the player it speaks of.</param>
/// <param name="Text">What was typed; of a system line, its localisation key.</param>
/// <param name="Clock">The receiving machine's own time of day as the line came, <c>HH:MM</c>.</param>
public sealed record CChatLine(EChatLineKind Kind, int Channel, string Name, string Text, string Clock);
