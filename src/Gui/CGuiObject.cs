using Godot;

namespace OpenHOI3.Gui;

/// <summary>
/// Base of the live GUI objects, mirroring the original's <c>CGuiObject</c>. The
/// parsed <c>C*Type</c> classes describe what the file said; these are the instances
/// on screen.
/// </summary>
public partial class CGuiObject : Control
{
	public CGuiType? GuiType { get; private set; }

	/// <summary>
	/// Places the control at its type's offset from the anchor named by its
	/// orientation. Anchors are used rather than fixed positions so the layout still
	/// resolves correctly when the viewport is wider than the 4:3 the art assumes.
	/// </summary>
	protected void ApplyLayout(CGuiType type, Vector2 size)
	{
		GuiType = type;
		Name = string.IsNullOrEmpty(type.Name) ? GetType().Name : type.Name;

		Vector2 anchor = type.Orientation switch
		{
			EOrientation.UpperRight => new Vector2(1f, 0f),
			EOrientation.LowerLeft => new Vector2(0f, 1f),
			EOrientation.LowerRight => new Vector2(1f, 1f),
			EOrientation.Center => new Vector2(0.5f, 0.5f),
			EOrientation.CenterUp => new Vector2(0.5f, 0f),
			EOrientation.CenterDown => new Vector2(0.5f, 1f),
			_ => new Vector2(0f, 0f),
		};

		AnchorLeft = anchor.X;
		AnchorRight = anchor.X;
		AnchorTop = anchor.Y;
		AnchorBottom = anchor.Y;

		OffsetLeft = type.Position.X;
		OffsetTop = type.Position.Y;
		OffsetRight = type.Position.X + size.X;
		OffsetBottom = type.Position.Y + size.Y;
	}

	/// <summary>Creates the control for a parsed element, or null if unsupported.</summary>
	public static CGuiObject? Create(CGuiType type, CGuiContext context)
	{
		switch (type)
		{
			case CWindowType window:
				var windowObject = new TWindow();
				windowObject.Initialise(window, context);
				return windowObject;
			case CButtonType button:
				var buttonObject = new TButton();
				buttonObject.Initialise(button, context);
				return buttonObject;
			case CIconType icon:
				var iconObject = new CIcon();
				iconObject.Initialise(icon, context);
				return iconObject;
			case CTextBoxType textBox:
				var textObject = new CTextBox();
				textObject.Initialise(textBox, context);
				return textObject;
			default:
				return null;
		}
	}
}
