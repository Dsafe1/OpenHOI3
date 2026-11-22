using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenHOI3.Content.Windows;
using OpenHOI3.Lua.Native;

namespace OpenHOI3.Lua;

/// <summary>
/// <c>OpenHOI3.ImGui</c>: the widgets a mod's page draws itself with (<c>docs/mechanics/modwindows.md</c>, section 4),
/// each under the name Dear ImGui gives it and handed on to whatever is drawing now (<see cref="Canvas"/>). A widget
/// that takes a value answers <c>changed, value</c>, as that library's Lua bindings do.
///
/// It also keeps the library's pairing for the page: what a page began and did not end - because it forgot, or because
/// its Lua failed half way - is ended when the page's drawing is over (<see cref="CloseAll"/>), and an <c>End...</c>
/// with nothing of its kind open is passed over. The library itself forgives neither.
/// </summary>
public sealed class CLuaImGui
{
	private enum EScope
	{
		Table,
		TabBar,
		TabItem,
		Child,
		Tree,
		Id,
		Disabled,
	}

	/// <summary>The functions, as <c>OpenHOI3.ImGui</c> names them.</summary>
	private static readonly string[] Names =
	{
		"Text", "TextDisabled", "TextWrapped", "TextColored", "Separator", "SeparatorText", "SameLine", "Spacing", "NewLine",
		"SetNextItemWidth", "Button", "SmallButton", "Selectable", "Checkbox", "InputText", "InputInt", "InputTextMultiline", "GetContentRegionAvail", "SliderInt", "SliderFloat",
		"Combo", "CollapsingHeader", "IsItemHovered", "SetTooltip", "BeginTable", "BeginPanes", "TableSetupColumn", "TableHeadersRow",
		"IsItemActive", "IsItemDeactivatedAfterEdit", "WindowSetting", "SetWindowSetting", "ProgressBar", "PlotLines", "InputFloat", "IsWindowFocused", "IsKeyPressed", "SetScrollHereY", "SetClipboardText", "ImageSize", "Image",
		"TableNextRow", "TableNextColumn", "EndTable", "BeginTabBar", "EndTabBar", "BeginTabItem", "EndTabItem",
		"BeginChild", "EndChild", "TreeNode", "TreePop", "PushID", "PopID", "BeginDisabled", "EndDisabled",
	};

	private const string GlobalPrefix = "__ohoi_ig_";

	private readonly List<EScope> _open = new();

	/// <summary>What is drawing now; null between two drawings, when every widget does nothing and answers false.</summary>
	public IModWindowCanvas? Canvas { get; set; }

	/// <summary>How many times an <c>End...</c> came with nothing of its kind open, since the count was last taken.</summary>
	public int Unmatched { get; private set; }

	/// <summary>Puts <c>OpenHOI3.ImGui</c> in the state; the global <c>OpenHOI3</c> is there already.</summary>
	public void Install(CLuaState lua)
	{
		var functions = new Dictionary<string, CLuaFunction>
		{
			["Text"] = call => Do(call, canvas => canvas.Text(call.String(1))),
			["TextDisabled"] = call => Do(call, canvas => canvas.TextDisabled(call.String(1))),
			["TextWrapped"] = call => Do(call, canvas => canvas.TextWrapped(call.String(1))),
			["TextColored"] = call => Do(call, canvas => canvas.TextColored((float)call.Number(1), (float)call.Number(2),
				(float)call.Number(3), (float)call.Number(4), call.String(5))),
			["Separator"] = call => Do(call, canvas => canvas.Separator()),
			["SeparatorText"] = call => Do(call, canvas => canvas.SeparatorText(call.String(1))),
			["SameLine"] = call => Do(call, canvas => canvas.SameLine()),
			["Spacing"] = call => Do(call, canvas => canvas.Spacing()),
			["NewLine"] = call => Do(call, canvas => canvas.NewLine()),
			["SetNextItemWidth"] = call => Do(call, canvas => canvas.SetNextItemWidth((float)call.Number(1))),
			["Button"] = call => Ask(call, canvas => canvas.Button(call.String(1))),
			["SmallButton"] = call => Ask(call, canvas => canvas.SmallButton(call.String(1))),
			["Selectable"] = call => Ask(call, canvas => canvas.Selectable(call.String(1), call.ArgumentCount >= 2 && call.Boolean(2))),
			["Checkbox"] = Checkbox,
			["InputText"] = InputText,
			["InputInt"] = InputInt,
			["InputTextMultiline"] = InputTextMultiline,
			["GetContentRegionAvail"] = call =>
			{
				(float width, float height) = Canvas?.ContentRegionAvail() ?? (0f, 0f);
				return call.ReturnMany((double)width, (double)height);
			},
			["SliderInt"] = SliderInt,
			["SliderFloat"] = SliderFloat,
			["Combo"] = Combo,
			["CollapsingHeader"] = call => Ask(call, canvas => canvas.CollapsingHeader(call.String(1))),
			["IsItemHovered"] = call => Ask(call, canvas => canvas.IsItemHovered()),
			["IsItemActive"] = call => Ask(call, canvas => canvas.IsItemActive()),
			["IsItemDeactivatedAfterEdit"] = call => Ask(call, canvas => canvas.IsItemDeactivatedAfterEdit()),
			["ProgressBar"] = call => Do(call, canvas => canvas.ProgressBar((float)call.Number(1),
				call.ArgumentCount >= 2 ? (float)call.Number(2) : 0f, call.ArgumentCount >= 3 ? call.String(3) : "")),
			["InputFloat"] = InputFloat,
			["WindowSetting"] = call => call.Return(Canvas?.WindowSetting(call.String(1)) ?? ""),
			["SetWindowSetting"] = call => Do(call, canvas => canvas.SetWindowSetting(call.String(1), call.String(2))),
			["PlotLines"] = PlotLines,
			["SetTooltip"] = call => Do(call, canvas => canvas.SetTooltip(call.String(1))),
			["BeginTable"] = call => Begin(call, EScope.Table, canvas => canvas.BeginTable(call.String(1), call.Integer(2), false)),
			// Columns side by side with a divider to drag; ended by EndTable, as a table is.
			["BeginPanes"] = call => Begin(call, EScope.Table, canvas => canvas.BeginTable(call.String(1), call.Integer(2), true)),
			["TableSetupColumn"] = call => Do(call, canvas => canvas.TableSetupColumn(call.String(1),
				call.ArgumentCount >= 2 ? (float)call.Number(2) : 0f)),
			["IsWindowFocused"] = call => Ask(call, canvas => canvas.IsWindowFocused()),
			["IsKeyPressed"] = call => Ask(call, canvas => canvas.IsKeyPressed(call.String(1))),
			["SetScrollHereY"] = call => Do(call, canvas => canvas.SetScrollHereY(call.ArgumentCount >= 1 ? (float)call.Number(1) : 0.5f)),
			["SetClipboardText"] = call => Do(call, canvas => canvas.SetClipboardText(call.String(1))),
			["ImageSize"] = call =>
			{
				(float width, float height) = Canvas?.ImageSize(call.String(1)) ?? (0f, 0f);
				return call.ReturnMany((double)width, (double)height);
			},
			["Image"] = call => Do(call, canvas => canvas.Image(call.String(1), (float)call.Number(2), (float)call.Number(3))),
			["TableHeadersRow"] = call => Do(call, canvas => canvas.TableHeadersRow()),
			["TableNextRow"] = call => Do(call, canvas => canvas.TableNextRow()),
			["TableNextColumn"] = call => Ask(call, canvas => canvas.TableNextColumn()),
			["EndTable"] = call => End(call, EScope.Table),
			["BeginTabBar"] = call => Begin(call, EScope.TabBar, canvas => canvas.BeginTabBar(call.String(1))),
			["EndTabBar"] = call => End(call, EScope.TabBar),
			["BeginTabItem"] = call => Begin(call, EScope.TabItem, canvas => canvas.BeginTabItem(call.String(1))),
			["EndTabItem"] = call => End(call, EScope.TabItem),
			["BeginChild"] = BeginChild,
			["EndChild"] = call => End(call, EScope.Child),
			["TreeNode"] = call => Begin(call, EScope.Tree, canvas => canvas.TreeNode(call.String(1))),
			["TreePop"] = call => End(call, EScope.Tree),
			["PushID"] = PushId,
			["PopID"] = call => End(call, EScope.Id),
			["BeginDisabled"] = BeginDisabled,
			["EndDisabled"] = call => End(call, EScope.Disabled),
		};
		var glue = new StringBuilder("OpenHOI3.ImGui = { ");
		foreach (string name in Names)
		{
			lua.SetGlobal(GlobalPrefix + name, functions[name]);
			glue.Append(name).Append(" = ").Append(GlobalPrefix).Append(name).Append(", ");
		}
		glue.Append("} ");
		foreach (string name in Names)
		{
			glue.Append(GlobalPrefix).Append(name).Append(" = nil ");
		}
		// A combo's choices come as a list, which crosses as so many texts.
		// The window's own settings, for a page that offers them (modwindows.md, section 6.4). They are the window's and
		// not the library's, so they stand beside the widgets and not among them.
		glue.Append("local get, set = OpenHOI3.ImGui.WindowSetting, OpenHOI3.ImGui.SetWindowSetting "
			+ "OpenHOI3.ImGui.WindowSetting, OpenHOI3.ImGui.SetWindowSetting = nil, nil "
			+ "OpenHOI3.Window = { "
			+ "Scale = function() return tonumber(get('scale')) or 1 end, "
			+ "SetScale = function(scale) set('scale', string.format('%.2f', tonumber(scale) or 1)) end, "
			+ "Theme = function() return get('theme') end, "
			+ "Themes = function() local list = {} for name in string.gmatch(get('themes'), '%S+') do list[#list + 1] = name end return list end, "
			+ "SetTheme = function(name) set('theme', tostring(name)) end, "
			+ "Key = function() return get('key') end, "
			+ "Capturing = function() return get('capturing') == 'yes' end, "
			+ "CaptureKey = function() set('key', 'capture') end, "
			+ "CancelCapture = function() set('key', 'cancel') end, "
			+ "ResetLayout = function() set('layout', 'reset') end } ");
		// The values cross as one text: a series may be longer than a call takes arguments.
		glue.Append("local plot = OpenHOI3.ImGui.PlotLines OpenHOI3.ImGui.PlotLines = function(label, values, width, height, overlay) "
			+ "return plot(label, table.concat(values or {}, ' '), width or 0, height or 0, overlay or '') end ");
		glue.Append("local combo = OpenHOI3.ImGui.Combo OpenHOI3.ImGui.Combo = function(label, index, items) "
			+ "return combo(label, index, unpack(items or {})) end");
		lua.Run(Encoding.Latin1.GetBytes(glue.ToString()), "=OpenHOI3");
	}

	/// <summary>
	/// Everything a page left open is ended, the last begun first: called when a page's drawing is over, however it
	/// ended. Answers how many there were.
	/// </summary>
	public int CloseAll()
	{
		int count = _open.Count;
		for (int i = _open.Count - 1; i >= 0; i--)
		{
			Close(_open[i]);
		}
		_open.Clear();
		return count;
	}

	/// <summary>The count of unmatched ends, taken and set to nought.</summary>
	public int TakeUnmatched()
	{
		int count = Unmatched;
		Unmatched = 0;
		return count;
	}

	private void Close(EScope scope)
	{
		if (Canvas is not { } canvas)
		{
			return;
		}
		switch (scope)
		{
			case EScope.Table:
				canvas.EndTable();
				break;
			case EScope.TabBar:
				canvas.EndTabBar();
				break;
			case EScope.TabItem:
				canvas.EndTabItem();
				break;
			case EScope.Child:
				canvas.EndChild();
				break;
			case EScope.Tree:
				canvas.TreePop();
				break;
			case EScope.Id:
				canvas.PopId();
				break;
			case EScope.Disabled:
				canvas.EndDisabled();
				break;
		}
	}

	private int Do(CLuaCall call, Action<IModWindowCanvas> draw)
	{
		if (Canvas is { } canvas)
		{
			draw(canvas);
		}
		return 0;
	}

	private int Ask(CLuaCall call, Func<IModWindowCanvas, bool> ask)
	{
		return call.Return(Canvas is { } canvas && ask(canvas));
	}

	/// <summary>A scope begun: open where the library says it is, which is where it has to be ended.</summary>
	private int Begin(CLuaCall call, EScope scope, Func<IModWindowCanvas, bool> begin)
	{
		bool open = Canvas is { } canvas && begin(canvas);
		if (open)
		{
			_open.Add(scope);
		}
		return call.Return(open);
	}

	/// <summary>A scope ended where it is the last one open; anything else is counted and passed over.</summary>
	private int End(CLuaCall call, EScope scope)
	{
		if (_open.Count > 0 && _open[^1] == scope)
		{
			_open.RemoveAt(_open.Count - 1);
			Close(scope);
		}
		else
		{
			Unmatched++;
		}
		return 0;
	}

	/// <summary>A child is ended whether it is seen or not, as the library asks.</summary>
	private int BeginChild(CLuaCall call)
	{
		if (Canvas is not { } canvas)
		{
			return call.Return(false);
		}
		float width = call.ArgumentCount >= 2 ? (float)call.Number(2) : 0f;
		float height = call.ArgumentCount >= 3 ? (float)call.Number(3) : 0f;
		bool seen = canvas.BeginChild(call.String(1), width, height);
		_open.Add(EScope.Child);
		return call.Return(seen);
	}

	private int PushId(CLuaCall call)
	{
		if (Canvas is { } canvas)
		{
			canvas.PushId(call.String(1));
			_open.Add(EScope.Id);
		}
		return 0;
	}

	/// <summary>Greyed from here to its end where the page says so - with nothing given, greyed.</summary>
	private int BeginDisabled(CLuaCall call)
	{
		if (Canvas is { } canvas)
		{
			canvas.BeginDisabled(call.ArgumentCount < 1 || call.Boolean(1));
			_open.Add(EScope.Disabled);
		}
		return 0;
	}

	/// <summary><c>InputFloat(label, value [, decimals])</c>, answering <c>changed, value</c>.</summary>
	private int InputFloat(CLuaCall call)
	{
		double value = call.Number(2);
		int decimals = call.ArgumentCount >= 3 ? call.Integer(3) : 2;
		bool changed = Canvas is { } canvas && canvas.InputFloat(call.String(1), ref value, decimals);
		return call.ReturnMany(changed, value);
	}

	/// <summary><c>PlotLines(label, values [, width, height, overlay])</c>: the values a list of numbers.</summary>
	private int PlotLines(CLuaCall call)
	{
		if (Canvas is { } canvas)
		{
			var values = new List<float>();
			foreach (string part in call.String(2).Split(' ', StringSplitOptions.RemoveEmptyEntries))
			{
				if (float.TryParse(part, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float value))
				{
					values.Add(value);
				}
			}
			canvas.PlotLines(call.String(1), values, (float)call.Number(3), (float)call.Number(4), call.String(5));
		}
		return 0;
	}

	private int InputInt(CLuaCall call)
	{
		int value = call.Integer(2);
		bool changed = Canvas is { } canvas && canvas.InputInt(call.String(1), ref value);
		return call.ReturnMany(changed, (double)value);
	}

	/// <summary><c>InputTextMultiline(label, text [, width, height, read_only])</c>, answering <c>changed, text</c>.</summary>
	private int InputTextMultiline(CLuaCall call)
	{
		string value = call.ArgumentCount >= 2 ? call.String(2) : "";
		float width = call.ArgumentCount >= 3 ? (float)call.Number(3) : 0f;
		float height = call.ArgumentCount >= 4 ? (float)call.Number(4) : 0f;
		bool readOnly = call.ArgumentCount >= 5 && call.Boolean(5);
		bool changed = Canvas is { } canvas && canvas.InputTextMultiline(call.String(1), ref value, width, height, readOnly);
		return call.ReturnMany(changed, value);
	}

	private int Checkbox(CLuaCall call)
	{
		bool value = call.Boolean(2);
		bool changed = Canvas is { } canvas && canvas.Checkbox(call.String(1), ref value);
		return call.ReturnMany(changed, value);
	}

	private int InputText(CLuaCall call)
	{
		string value = call.ArgumentCount >= 2 ? call.String(2) : "";
		int most = call.ArgumentCount >= 3 ? call.Integer(3) : 256;
		bool changed = Canvas is { } canvas && canvas.InputText(call.String(1), ref value, Math.Clamp(most, 1, 4096));
		return call.ReturnMany(changed, value);
	}

	private int SliderInt(CLuaCall call)
	{
		int value = call.Integer(2);
		bool changed = Canvas is { } canvas && canvas.SliderInt(call.String(1), ref value, call.Integer(3), call.Integer(4));
		return call.ReturnMany(changed, (double)value);
	}

	private int SliderFloat(CLuaCall call)
	{
		float value = (float)call.Number(2);
		bool changed = Canvas is { } canvas
			&& canvas.SliderFloat(call.String(1), ref value, (float)call.Number(3), (float)call.Number(4));
		return call.ReturnMany(changed, (double)value);
	}

	/// <summary>The choice counted from one, as a Lua list counts.</summary>
	private int Combo(CLuaCall call)
	{
		int index = call.Integer(2) - 1;
		var items = Enumerable.Range(3, Math.Max(0, call.ArgumentCount - 2)).Select(i => call.String(i)).ToList();
		bool changed = Canvas is { } canvas && canvas.Combo(call.String(1), ref index, items);
		return call.ReturnMany(changed, (double)(index + 1));
	}
}
