using System;
using System.Collections.Generic;
using OpenHOI3.Content;
using OpenHOI3.Content.Definitions;
using OpenHOI3.Content.Map;
using OpenHOI3.Interface.Script;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Script;

/// <summary>
/// Writes every event option's tooltip, every <c>immediate</c> block and every decision's two
/// tooltips as the country given would see them on the start date, and says how many lines came out,
/// how many keys no table has, and an example or two:
///
///   OpenHOI3.SaveCheck scripttext &lt;install folder&gt; [tag] [event id or decision name]
///
/// A look and not a check: nothing keeps what the original wrote. Germany by default; with an event's
/// id or a decision's name, that one is written out whole. See <c>docs/mechanics/scripttext.md</c>.
/// </summary>
internal static class CScriptTextTrial
{
	public static int Run(string[] args)
	{
		if (args.Length < 2)
		{
			Console.Error.WriteLine("usage: OpenHOI3.SaveCheck scripttext <install folder> [tag] [event id or decision name]");
			return 2;
		}
		var warnings = new List<string>();
		CDirectorySettings directories = CToolDirectories.At(args[1]);
		CLocalisation localisation = CLocalisation.Load(directories, warn: warnings.Add);
		CCountries countries = CCountries.Load(directories, localisation, warnings.Add);
		CMap map = CMap.Load(directories, warnings.Add);
		CGameState state = CGameStateLoader.LoadStart(directories, map, localisation, countries, warnings.Add);
		var tag = new CCountryTag(args.Length > 2 ? args[2].ToUpperInvariant() : "GER");
		string? only = args.Length > 3 ? args[3] : null;
		var missing = new SortedDictionary<string, int>(StringComparer.Ordinal);
		string Localise(string key)
		{
			string text = localisation.Localise(key);
			if (text == key && key.Length > 0 && IsKeyShaped(key) && localisation.Find(key) is null)
			{
				missing[key] = missing.GetValueOrDefault(key) + 1;
			}
			return text;
		}
		var words = new CScriptWords(Localise, state, tag);
		int texts = 0;
		int lines = 0;
		int failed = 0;
		var clock = System.Diagnostics.Stopwatch.StartNew();
		void Show(string what, Func<string> write)
		{
			string text;
			try
			{
				text = write();
			}
			catch (Exception error)
			{
				failed++;
				Console.WriteLine($"   {what}: {error.GetType().Name}: {error.Message}");
				return;
			}
			texts++;
			lines += text.Length == 0 ? 0 : text.Split('\n').Length;
			if (only is not null)
			{
				Console.WriteLine($"--- {what}");
				Console.WriteLine(text.Replace("§", "~"));
			}
		}
		foreach (CEvent shown in state.Events)
		{
			if (only is not null && shown.Id.ToString(System.Globalization.CultureInfo.InvariantCulture) != only)
			{
				continue;
			}
			CEventScope scope = CEventScope.Start(tag);
			Show($"event {shown.Id} immediate", () => CEffectText.For(words, shown.Immediate.Effects, scope));
			for (int i = 0; i < shown.Options.Count; i++)
			{
				CEventOption option = shown.Options[i];
				Show($"event {shown.Id} option {i} ({localisation.Localise(option.Name)})",
					() => CEffectText.For(words, option.Effects, CEventScope.Start(tag)));
			}
		}
		foreach (CDecision decision in state.Decisions)
		{
			if (only is not null && decision.Name != only)
			{
				continue;
			}
			Show($"decision {decision.Name} effects", () => CEffectText.For(words, decision.Effects, CEventScope.Start(tag)));
			Show($"decision {decision.Name} allow", () => CConditionText.For(words, decision.Allow, CEventScope.Start(tag)));
		}
		Console.WriteLine($"{texts} texts, {lines} lines, {failed} failed, in {clock.ElapsedMilliseconds} ms, as {tag} sees them");
		Console.WriteLine($"{missing.Count} keys no table has:");
		foreach ((string key, int count) in missing)
		{
			Console.WriteLine($"   {key} x{count}");
		}
		return failed == 0 ? 0 : 1;
	}

	/// <summary>A key as the code names one - capitals, digits and underscores - and not a name looked up as one.</summary>
	private static bool IsKeyShaped(string key)
	{
		foreach (char c in key)
		{
			if (!(char.IsAsciiLetterUpper(c) || char.IsAsciiDigit(c) || c == '_'))
			{
				return false;
			}
		}
		return true;
	}
}
