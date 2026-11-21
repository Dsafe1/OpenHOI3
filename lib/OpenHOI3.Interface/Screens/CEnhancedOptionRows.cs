using System.Collections.Generic;
using OpenHOI3.Content.Gui;

namespace OpenHOI3.Interface.Screens;

/// <summary>
/// The elements of the enhanced rows on the game options' Game tab, which no interface file declares: each row is the
/// tab's own autosave row over again - its label, its value and its two arrows as the file in play draws them - a set
/// distance further down. So the rows wear whatever a mod made of that tab, and a mod's file needs nothing added, which
/// the original would draw as well. <c>docs/mechanics/gameoptions.md</c>, section 5.
/// </summary>
public static class CEnhancedOptionRows
{
	/// <summary>The four elements of the autosave row, which each new row copies.</summary>
	public const string LabelSource = "autosave_label";
	public const string ValueSource = "autosave_value";
	public const string DecreaseSource = "decrease_autosave_button";
	public const string IncreaseSource = "increase_autosave_button";

	/// <summary>How far below the row before it each row stands, in the file's pixels: the autosave row's height and a gap.</summary>
	public const int Pitch = 46;

	public static string LabelName(string row) => $"openhoi_{row}_label";

	public static string ValueName(string row) => $"openhoi_{row}_value";

	public static string DecreaseName(string row) => $"decrease_openhoi_{row}_button";

	public static string IncreaseName(string row) => $"increase_openhoi_{row}_button";

	/// <summary>
	/// The rows' elements, in the order they are to be added to the tab; none where the tab lacks one of the four they
	/// are copied from - a mod that took the autosave row out has no row to copy.
	/// </summary>
	public static List<CGuiType> Build(CGuiWindowType gameTab, IReadOnlyList<string> rows)
	{
		var made = new List<CGuiType>();
		if (gameTab.Find<CGuiTextBoxType>(LabelSource) is not { } label
			|| gameTab.Find<CGuiTextBoxType>(ValueSource) is not { } value
			|| gameTab.Find<CGuiButtonType>(DecreaseSource) is not { } decrease
			|| gameTab.Find<CGuiButtonType>(IncreaseSource) is not { } increase)
		{
			return made;
		}
		for (int i = 0; i < rows.Count; i++)
		{
			int down = Pitch * (i + 1);
			made.Add(Copy(label, LabelName(rows[i]), down));
			made.Add(Copy(value, ValueName(rows[i]), down));
			made.Add(Copy(decrease, DecreaseName(rows[i]), down));
			made.Add(Copy(increase, IncreaseName(rows[i]), down));
		}
		return made;
	}

	/// <summary>A text box over again under another name, lower down, with no text of its own: its screen sets it.</summary>
	private static CGuiTextBoxType Copy(CGuiTextBoxType from, string name, int down) => new()
	{
		Name = name,
		Keyword = from.Keyword,
		Position = new CGuiPoint(from.Position.X, from.Position.Y + down),
		Orientation = from.Orientation,
		Font = from.Font,
		Text = "",
		MaxWidth = from.MaxWidth,
		MaxHeight = from.MaxHeight,
		BorderSize = from.BorderSize,
		TextureFile = from.TextureFile,
		Format = from.Format,
		FixedSize = from.FixedSize,
		IsInstant = from.IsInstant,
		IsEdit = from.IsEdit,
	};

	/// <summary>A button over again under another name, lower down, with no shortcut: two buttons of one key would both take it.</summary>
	private static CGuiButtonType Copy(CGuiButtonType from, string name, int down) => new()
	{
		Name = name,
		Keyword = from.Keyword,
		Position = new CGuiPoint(from.Position.X, from.Position.Y + down),
		Orientation = from.Orientation,
		Sprite = from.Sprite,
		Parent = from.Parent,
		Text = from.Text,
		Font = from.Font,
		Shortcut = "",
		ClickSound = from.ClickSound,
		UpSound = from.UpSound,
		DownSound = from.DownSound,
		OverSound = from.OverSound,
		Rotation = from.Rotation,
		IsCheckbox = from.IsCheckbox,
		Size = from.Size,
		Format = from.Format,
		BorderSize = from.BorderSize,
	};
}
