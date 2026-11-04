namespace OpenHOI3.Interface.Screens;

/// <summary>
/// What the game options window's controls stand at when Apply is pressed, for what the window keeps in no field of its
/// own: the sliders' positions and the two boxes it reads as they are. See <c>docs/mechanics/gameoptions.md</c>, section 2.
/// </summary>
public sealed record CGameOptionsSliders(
	float Gamma,
	float MasterVolume,
	float EffectVolume,
	float MusicVolume,
	float ScrollSpeed,
	float ZoomSpeed,
	bool CountersTicked,
	bool LegacyCameraTicked);
