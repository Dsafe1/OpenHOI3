namespace OpenHOI3.Interface.Tooltips;

/// <summary>
/// Where the tooltip box goes. See <c>docs/mechanics/tooltips.md</c>, sections 5, 7 and 8.
/// </summary>
public static class CTooltipPlacement
{
	/// <summary>How far below an element's corner a tooltip hangs.</summary>
	public const int BelowElement = 64;

	/// <summary>
	/// The part of the screen's height below which an element's tooltip stands above it
	/// instead. A single-precision 0.8, widened, as the original holds it.
	/// </summary>
	public const double LowestFifth = 0.800000011920929;

	/// <summary>How far left of the mouse a province's tooltip starts.</summary>
	public const int LeftOfMouse = 50;

	/// <summary>How far above the mouse a province's tooltip ends.</summary>
	public const int AboveMouse = 20;

	/// <summary>
	/// An element's tooltip with its point: the element's corner moved by the tooltip's own
	/// offset; then 64 down, unless the box would hang from the lowest fifth of the screen,
	/// when it stands on the point instead.
	/// </summary>
	public static CTooltip AtElement(CTooltip tooltip, int cornerX, int cornerY, int screenHeight)
	{
		int x = cornerX + tooltip.X;
		int y = cornerY + tooltip.Y;
		if (tooltip.Below && y > screenHeight * LowestFifth)
		{
			return tooltip with { X = x, Y = y, Below = false };
		}
		return tooltip with { X = x, Y = y + BelowElement };
	}

	/// <summary>A province's tooltip with its point: 50 left of the mouse and 20 above it, the box standing on it.</summary>
	public static CTooltip AtMouse(CTooltip tooltip, int mouseX, int mouseY)
	{
		return tooltip with { X = mouseX - LeftOfMouse, Y = mouseY - AboveMouse, Below = false };
	}

	/// <summary>
	/// The box's corner: at the point, or its height above it; then pulled back inside the
	/// right and the bottom of the screen. Nothing keeps it off the left or the top.
	/// </summary>
	public static (int X, int Y) Place(CTooltip tooltip, int width, int height, int screenWidth, int screenHeight)
	{
		int x = tooltip.X;
		int y = tooltip.Below ? tooltip.Y : tooltip.Y - height;
		if (x + width > screenWidth)
		{
			x = screenWidth - width;
		}
		if (y + height > screenHeight)
		{
			y = screenHeight - height;
		}
		return (x, y);
	}
}
