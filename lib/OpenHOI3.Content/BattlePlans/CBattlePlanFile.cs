using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using OpenHOI3.Content.Clausewitz;
using OpenHOI3.Sim.Plans;

namespace OpenHOI3.Content.BattlePlans;

/// <summary>
/// The battle plan format, both ways, a <c>.pln</c> file's and a saved game's <c>battle_plan</c> block's. It is read by
/// position: each key is compared with the next one expected and a key missing is not consumed. Numbers are whole
/// thousandths. See <c>docs/mechanics/battleplans.md</c>, section 7.
/// </summary>
public static class CBattlePlanFile
{
	private const string Line = "\r\n";

	/// <summary>A plan's title and country, for a row of a list; a file that fails them keeps <see cref="CBattlePlan.HeaderGood"/> false.</summary>
	public static CBattlePlan ReadHeader(string path)
	{
		var plan = new CBattlePlan { FilePath = path };
		var root = ClausewitzParser.ParseFile(path);
		var cursor = new Cursor(root);
		ReadHeader(cursor, plan);
		return plan;
	}

	private static void ReadHeader(Cursor cursor, CBattlePlan plan)
	{
		if (cursor.Take("title") is not { } title)
		{
			return;
		}
		plan.Title = title.Value ?? "?";
		if (cursor.Take("country") is not { } country)
		{
			return;
		}
		plan.Country = country.Value ?? "---";
		plan.HeaderGood = plan.Title.Length > 0 && plan.Country != "---";
	}

	public static CBattlePlan ReadFile(string path)
	{
		CBattlePlan plan = Read(ClausewitzParser.ParseFile(path));
		plan.FilePath = "";
		return plan;
	}

	/// <summary>A whole plan out of its keys; the keys of a saved game's block or of a file.</summary>
	public static CBattlePlan Read(ClausewitzNode root)
	{
		var plan = new CBattlePlan { IsLive = true, IsDirty = true };
		var cursor = new Cursor(root);
		ReadHeader(cursor, plan);
		if (cursor.Take("visibility") is { } visibility)
		{
			plan.IsVisible = visibility.Value == "yes";
		}
		ReadStrokes(cursor, "arrows_count", plan.Arrows);

		int icons = Count(cursor, "icons_count");
		if (icons > 0)
		{
			float[] positions = Numbers(cursor, "positions");
			float[] origins = Numbers(cursor, "matrix_transforms");
			string[] types = Words(cursor, "type");
			int[] colours = Whole(cursor, "color");
			float[] rotations = Numbers(cursor, "rotation");
			for (int index = 0; index < icons; index++)
			{
				plan.Icons.Add(new CPlanIcon
				{
					X = At(positions, index * 2), Y = At(positions, index * 2 + 1),
					OriginX = At(origins, index * 2), OriginY = At(origins, index * 2 + 1),
					Type = index < types.Length ? types[index] : "", Colour = index < colours.Length ? colours[index] : 0,
					Rotation = At(rotations, index),
				});
			}
		}

		int texts = Count(cursor, "texts_count");
		if (texts > 0)
		{
			float[] positions = Numbers(cursor, "positions");
			float[] origins = Numbers(cursor, "matrix_transforms");
			var words = new List<string>();
			for (int index = 0; index < texts; index++)
			{
				words.Add(cursor.Take("text")?.Value ?? "");
			}
			float[] scales = Numbers(cursor, "scale");
			float[] rotations = Numbers(cursor, "rotation");
			for (int index = 0; index < texts; index++)
			{
				plan.Texts.Add(new CPlanText
				{
					Text = words[index], X = At(positions, index * 2), Y = At(positions, index * 2 + 1),
					OriginX = At(origins, index * 2), OriginY = At(origins, index * 2 + 1),
					Size = At(scales, index), Rotation = At(rotations, index),
				});
			}
		}

		plan.IsShared = cursor.Take("is_battle_plan_shared")?.Value == "yes";
		ReadStrokes(cursor, "borders_count", plan.Borders);
		return plan;
	}

	private static void ReadStrokes(Cursor cursor, string countKey, List<CPlanStroke> strokes)
	{
		int count = Count(cursor, countKey);
		if (count <= 0)
		{
			return;
		}
		for (int index = 0; index < count; index++)
		{
			var stroke = new CPlanStroke();
			float[] points = Numbers(cursor, "control_points");
			for (int at = 0; at + 1 < points.Length; at += 2)
			{
				stroke.Points.Add((points[at], points[at + 1]));
			}
			stroke.Label = cursor.Take("text")?.Value ?? "";
			strokes.Add(stroke);
		}
		float[] origins = Numbers(cursor, "matrix_transforms");
		int[] prefixes = Whole(cursor, "ignore_smoothing");
		string[] types = Words(cursor, "type");
		int[] colours = Whole(cursor, "color");
		for (int index = 0; index < count; index++)
		{
			CPlanStroke stroke = strokes[strokes.Count - count + index];
			stroke.OriginX = At(origins, index * 2);
			stroke.OriginY = At(origins, index * 2 + 1);
			stroke.SnapPrefix = index < prefixes.Length ? prefixes[index] : -1;
			stroke.Type = index < types.Length ? types[index] : "";
			stroke.Colour = index < colours.Length ? colours[index] : 0;
		}
	}

	private static int Count(Cursor cursor, string key)
	{
		return cursor.Take(key)?.Value is { } text && int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int count) ? count : 0;
	}

	private static float At(float[] values, int index) => index < values.Length ? values[index] : 0f;

	private static float[] Numbers(Cursor cursor, string key)
	{
		return Items(cursor, key).Select(item => long.Parse(item, CultureInfo.InvariantCulture) / 1000f).ToArray();
	}

	private static int[] Whole(Cursor cursor, string key)
	{
		return Items(cursor, key).Select(item => int.Parse(item, CultureInfo.InvariantCulture)).ToArray();
	}

	private static string[] Words(Cursor cursor, string key) => Items(cursor, key).ToArray();

	private static IEnumerable<string> Items(Cursor cursor, string key)
	{
		return cursor.Take(key) is { } block ? block.Children.Select(child => child.Value ?? child.Key ?? "") : Enumerable.Empty<string>();
	}

	/// <summary>A file's text; with <paramref name="visibility"/>, a saved game's.</summary>
	public static string Write(CBattlePlan plan, bool visibility = false)
	{
		var text = new StringBuilder();
		Pair(text, "title", Quote(plan.Title));
		Pair(text, "country", Quote(plan.Country));
		if (visibility)
		{
			Pair(text, "visibility", plan.IsVisible ? "yes" : "no");
		}
		WriteStrokes(text, "arrows_count", plan.Arrows);
		Pair(text, "icons_count", plan.Icons.Count.ToString(CultureInfo.InvariantCulture));
		if (plan.Icons.Count > 0)
		{
			Block(text, "positions", plan.Icons.SelectMany(icon => new[] { Thousandths(icon.X), Thousandths(icon.Y) }));
			Block(text, "matrix_transforms", plan.Icons.SelectMany(icon => new[] { Thousandths(icon.OriginX), Thousandths(icon.OriginY) }));
			Block(text, "type", plan.Icons.Select(icon => icon.Type));
			Block(text, "color", plan.Icons.Select(icon => icon.Colour.ToString(CultureInfo.InvariantCulture)));
			Block(text, "rotation", plan.Icons.Select(icon => Thousandths(icon.Rotation)));
		}
		Pair(text, "texts_count", plan.Texts.Count.ToString(CultureInfo.InvariantCulture));
		if (plan.Texts.Count > 0)
		{
			Block(text, "positions", plan.Texts.SelectMany(sign => new[] { Thousandths(sign.X), Thousandths(sign.Y) }));
			Block(text, "matrix_transforms", plan.Texts.SelectMany(sign => new[] { Thousandths(sign.OriginX), Thousandths(sign.OriginY) }));
			foreach (CPlanText sign in plan.Texts)
			{
				Pair(text, "text", Quote(sign.Text));
			}
			Block(text, "scale", plan.Texts.Select(sign => Thousandths(sign.Size)));
			Block(text, "rotation", plan.Texts.Select(sign => Thousandths(sign.Rotation)));
		}
		Pair(text, "is_battle_plan_shared", plan.IsShared ? "yes" : "no");
		WriteStrokes(text, "borders_count", plan.Borders);
		return text.ToString();
	}

	private static void WriteStrokes(StringBuilder text, string countKey, List<CPlanStroke> strokes)
	{
		Pair(text, countKey, strokes.Count.ToString(CultureInfo.InvariantCulture));
		if (strokes.Count == 0)
		{
			return;
		}
		foreach (CPlanStroke stroke in strokes)
		{
			Block(text, "control_points", stroke.Points.SelectMany(point => new[] { Thousandths(point.X), Thousandths(point.Y) }));
			Pair(text, "text", Quote(stroke.Label));
		}
		Block(text, "matrix_transforms", strokes.SelectMany(stroke => new[] { Thousandths(stroke.OriginX), Thousandths(stroke.OriginY) }));
		Block(text, "ignore_smoothing", strokes.Select(stroke => stroke.SnapPrefix.ToString(CultureInfo.InvariantCulture)));
		Block(text, "type", strokes.Select(stroke => stroke.Type));
		Block(text, "color", strokes.Select(stroke => stroke.Colour.ToString(CultureInfo.InvariantCulture)));
	}

	/// <summary>floor((value + 0.0005) * 1000), as an integer.</summary>
	private static string Thousandths(float value)
	{
		return ((long)Math.Floor((value + 0.0005f) * 1000f)).ToString(CultureInfo.InvariantCulture);
	}

	private static string Quote(string value) => "\"" + value + "\"";

	private static void Pair(StringBuilder text, string key, string value)
	{
		text.Append(key).Append('=').Append(value).Append(Line);
	}

	private static void Block(StringBuilder text, string key, IEnumerable<string> values)
	{
		text.Append(key).Append('=').Append(Line).Append('{').Append(Line);
		text.Append(string.Join(" ", values)).Append(" }").Append(Line);
	}

	/// <summary>The keys in order, one taken only where it is the next.</summary>
	private sealed class Cursor
	{
		private readonly List<ClausewitzNode> _children;
		private int _next;

		public Cursor(ClausewitzNode root)
		{
			_children = root.Children;
		}

		public ClausewitzNode? Take(string key)
		{
			if (_next < _children.Count && _children[_next].Key == key)
			{
				return _children[_next++];
			}
			return null;
		}
	}
}
