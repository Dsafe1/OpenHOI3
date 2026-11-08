namespace OpenHOI3.Content.BattlePlans;

/// <summary>One choice of a battle plan tool: its name's key and the object type or the colour it stands for.</summary>
public sealed record CPlanToolOption(string Name, string Type, float Red, float Green, float Blue);
