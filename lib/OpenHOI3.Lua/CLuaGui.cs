using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenHOI3.Content.Windows;
using OpenHOI3.Lua.Native;

namespace OpenHOI3.Lua;

/// <summary>
/// <c>OpenHOI3.Gui</c>: what a mod's native page fills its window with (<c>docs/mechanics/nativepages.md</c>, section
/// 4), handed on to the window being filled now (<see cref="Canvas"/>). Between two fillings every function does
/// nothing and answers false or an empty text.
/// </summary>
public sealed class CLuaGui
{
	private const string GlobalPrefix = "__ohoi_gui_";

	/// <summary>The window being filled; null between two fillings.</summary>
	public INativeWindowCanvas? Canvas { get; set; }

	/// <summary>Puts <c>OpenHOI3.Gui</c> in the state; the global <c>OpenHOI3</c> is there already.</summary>
	public void Install(CLuaState lua)
	{
		var functions = new Dictionary<string, CLuaFunction>
		{
			["SetText"] = call => Do(call, canvas => canvas.SetText(call.String(1), call.String(2))),
			["Text"] = call => call.Return(Canvas?.Text(call.String(1)) ?? ""),
			["Edited"] = call => call.Return(Canvas?.Edited(call.String(1)) ?? false),
			["Pressed"] = call => call.Return(Canvas?.Pressed(call.String(1)) ?? false),
			["Checked"] = call => call.Return(Canvas?.Checked(call.String(1)) ?? false),
			["SetChecked"] = call => Do(call, canvas => canvas.SetChecked(call.String(1), call.Boolean(2))),
			["SetVisible"] = call => Do(call, canvas => canvas.SetVisible(call.String(1), call.Boolean(2))),
			["SetEnabled"] = call => Do(call, canvas => canvas.SetEnabled(call.String(1), call.Boolean(2))),
			["SetTooltip"] = call => Do(call, canvas => canvas.SetTooltip(call.String(1), call.String(2))),
			["SetFrame"] = call => Do(call, canvas => canvas.SetFrame(call.String(1), call.Integer(2))),
			["SetColour"] = call => Do(call, canvas => canvas.SetColour(call.String(1), (float)call.Number(2), (float)call.Number(3),
				(float)call.Number(4), call.ArgumentCount >= 5 ? (float)call.Number(5) : 255f)),
			["SetRows"] = call => Do(call, canvas => canvas.SetRows(call.String(1), call.String(2), call.Integer(3))),
			["Close"] = call => Do(call, canvas => canvas.Close()),
		};
		var glue = new StringBuilder("OpenHOI3.Gui = {} ");
		foreach ((string name, CLuaFunction function) in functions.OrderBy(pair => pair.Key, System.StringComparer.Ordinal))
		{
			lua.SetGlobal(GlobalPrefix + name, function);
			glue.Append("OpenHOI3.Gui.").Append(name).Append(" = ").Append(GlobalPrefix).Append(name).Append(' ')
				.Append(GlobalPrefix).Append(name).Append(" = nil ");
		}
		// The American spelling beside ours, as the files' own keys have both.
		glue.Append("OpenHOI3.Gui.SetColor = OpenHOI3.Gui.SetColour");
		lua.Run(Encoding.Latin1.GetBytes(glue.ToString()), "=OpenHOI3");
	}

	private int Do(CLuaCall call, System.Action<INativeWindowCanvas> act)
	{
		if (Canvas is { } canvas)
		{
			act(canvas);
		}
		return 0;
	}
}
