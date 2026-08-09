using System;
using Godot;
using OpenHOI3.Gfx;

namespace OpenHOI3.Gui;

/// <summary>
/// A text label from a <c>textBoxType</c> or <c>instantTextBoxType</c> entry. The
/// distinction between the two is when the game refreshes the contents, which does
/// not matter until anything writes to them.
/// </summary>
public partial class CTextBox : CGuiObject
{
	public Label Label { get; private set; } = null!;

	public void Initialise(CTextBoxType type, CGuiContext context)
	{
		ApplyLayout(type, type.MaxSize);
		MouseFilter = MouseFilterEnum.Ignore;

		Label = new Label
		{
			Name = "Text",
			Text = context.Localisation.Localise(type.Text),
			HorizontalAlignment = ParseAlignment(type.Format),
			VerticalAlignment = VerticalAlignment.Top,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		Label.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

		CBitmapFont? font = context.Fonts.Find(type.FontName);
		if (font?.Font is not null)
		{
			Label.AddThemeFontOverride("font", font.Font);
			Label.AddThemeColorOverride("font_color", font.Color);
		}
		AddChild(Label);
	}

	public void SetText(string text)
	{
		Label.Text = text;
	}

	private static HorizontalAlignment ParseAlignment(string format)
	{
		return format.ToLowerInvariant() switch
		{
			"centre" or "center" => HorizontalAlignment.Center,
			"right" => HorizontalAlignment.Right,
			_ => HorizontalAlignment.Left,
		};
	}
}
