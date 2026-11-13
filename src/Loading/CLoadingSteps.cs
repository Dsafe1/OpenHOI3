namespace OpenHOI3.Loading;

/// <summary>
/// The lines the loading screen shows, by their localisation keys - the original's own, from its <c>frontend.csv</c>, but
/// for <see cref="MapModes"/>, which is ours (<c>docs/mechanics/loadingscreen.md</c>).
/// </summary>
public static class CLoadingSteps
{
	public const string Databases = "LOADING_DATABASES";
	public const string SecondDatabases = "LOADING_DATABASES2";
	public const string MapLogic = "INIT_MAP_LOGIC";
	public const string History = "LOADING_HISTORY";
	public const string Checksum = "CREATING_CHKSUM";
	public const string GuiDefinitions = "LOADING_GUI_DEF";
	public const string Graphics = "LOAD_GFX";
	public const string World = "CREATING_THE_WORLD";
	public const string Sounds = "LOADING_SOUNDS";
	public const string MapSprites = "LOADING_MAP_SPRITES";
	public const string AdaptingHistory = "ADAPTING_HISTORY";

	/// <summary>
	/// Ours: each map mode's surface made and drawn once before the game is shown, so that the first choice of a mode does
	/// not stop the map. The original has no such step and no line for it, so the line is ours too, in English
	/// (<see cref="MapModesText"/>) where no table has the key.
	/// </summary>
	public const string MapModes = "OPENHOI3_PREPARING_MAP_MODES";

	/// <summary>The line for <see cref="MapModes"/> where no localisation table has it.</summary>
	public const string MapModesText = "Preparing the Map Modes...";
}
