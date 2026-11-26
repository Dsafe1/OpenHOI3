using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using OpenHOI3.Content.Windows;

/// <summary>
/// A canvas that draws nothing and keeps what a page asked of it, for looking at a mod's page with no screen
/// (<see cref="CPagesLook"/>): every call by its name and what it was given. Whatever begins a part - a table, a
/// child, a tab, a header - answers that it is open, so that all of a page is drawn; nothing is pressed, ticked or
/// typed; and there is room of 800 by 600 left.
/// </summary>
public class CPrintingCanvas : DispatchProxy
{
	/// <summary>The calls in the order made, each its name and its arguments.</summary>
	public List<(string Name, object?[] Arguments)> Calls { get; } = new();

	public static (IModWindowCanvas Canvas, CPrintingCanvas Kept) Create()
	{
		IModWindowCanvas canvas = Create<IModWindowCanvas, CPrintingCanvas>();
		return (canvas, (CPrintingCanvas)(object)canvas);
	}

	protected override object? Invoke(MethodInfo? method, object?[]? arguments)
	{
		if (method is null)
		{
			return null;
		}
		Calls.Add((method.Name, arguments?.ToArray() ?? Array.Empty<object?>()));
		Type answer = method.ReturnType;
		if (answer == typeof(bool))
		{
			return method.Name.StartsWith("Begin", StringComparison.Ordinal) || method.Name is "TableNextColumn" or "CollapsingHeader" or "TreeNode";
		}
		if (answer == typeof((float, float)))
		{
			return (800f, 600f);
		}
		return answer == typeof(void) || !answer.IsValueType ? null : Activator.CreateInstance(answer);
	}

	/// <summary>What was drawn as lines of text: a table a row a line, its cells apart by a bar.</summary>
	public IEnumerable<string> Lines()
	{
		var row = new List<string>();
		bool inTable = false;
		foreach ((string name, object?[] arguments) in Calls)
		{
			string text = string.Join(" ", arguments.Select(Shown).Where(part => part.Length != 0));
			switch (name)
			{
				case "BeginTable":
					inTable = true;
					break;
				case "EndTable":
					if (row.Count != 0)
					{
						yield return "   " + string.Join(" | ", row);
						row.Clear();
					}
					inTable = false;
					break;
				case "TableNextRow" or "TableHeadersRow":
					if (row.Count != 0)
					{
						yield return "   " + string.Join(" | ", row);
						row.Clear();
					}
					break;
				case "TableSetupColumn":
					row.Add("[" + text + "]");
					break;
				case "TableNextColumn":
					break;
				case "Text" or "TextDisabled" or "TextWrapped" or "TextColored":
					if (inTable)
					{
						row.Add(arguments.OfType<string>().LastOrDefault() ?? "");
					}
					else
					{
						yield return (name == "Text" ? "" : "(" + name + ") ") + (arguments.OfType<string>().LastOrDefault() ?? "");
					}
					break;
				case "SeparatorText":
					yield return "---- " + text + " ----";
					break;
				case "Button" or "SmallButton" or "Checkbox" or "InputText" or "PlotLines" or "PlotMany" or "ProgressBar" or "Combo" or "Selectable":
					yield return "<" + name + "> " + text;
					break;
			}
		}
	}

	private static string Shown(object? argument)
	{
		return argument switch
		{
			null => "",
			string text => text,
			System.Collections.IEnumerable list => "(" + list.Cast<object?>().Count() + " values)",
			IFormattable number => number.ToString(null, System.Globalization.CultureInfo.InvariantCulture),
			_ => argument.ToString() ?? "",
		};
	}
}
