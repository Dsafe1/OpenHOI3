namespace OpenHOI3.Content.BattlePlans;

/// <summary>A <c>battlearrow</c> of <c>interface/battlearrow.gfx</c>: an arrow's or a border's textures and sizes.</summary>
public sealed record CBattleArrowType(string Name, string Body, string Head, float Start, float Stop, float HeadLength, float HeadHalfHeight,
	string Font, float TextScale, float TextureLoop, bool NoFade);
