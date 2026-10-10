using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using OpenHOI3.Content.Clausewitz;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Convoys;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;

/// <summary>
/// Sets the convoys a new game makes beside those a savegame of the start date has:
/// which there are, the way each goes, the transports and escorts on each, and what
/// every country has left over. It is run by the <c>supplypass</c> check once that has
/// made the game the way the original does. See <c>docs/mechanics/convoys.md</c>,
/// section 12.
/// </summary>
internal static class CConvoyCheck
{
	public static void Compare(CGameState state, List<(ClausewitzNode Node, CCountry Country)> savedCountries,
		int examples, string? onlyCountry)
	{
		int saved = 0;
		int ours = 0;
		int both = 0;
		int samePath = 0;
		int sameLength = 0;
		int sameTransports = 0;
		int sameEscorts = 0;
		int countriesWithConvoys = 0;
		int sameFreeTransports = 0;
		int sameFreeEscorts = 0;
		int sameSet = 0;
		var notes = new List<string>();

		foreach ((ClausewitzNode node, CCountry country) in savedCountries)
		{
			var theirs = new Dictionary<string, (List<int> Path, int Transports, int Escorts)>();
			foreach (ClausewitzNode convoy in node.Children)
			{
				if (convoy.Key != "convoy" || !convoy.IsBlock)
				{
					continue;
				}
				var path = new List<int>();
				foreach (ClausewitzNode step in convoy.Get("path")?.Children ?? new List<ClausewitzNode>())
				{
					if (int.TryParse(step.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int id))
					{
						path.Add(id);
					}
				}
				string goods = string.Concat((convoy.Get("ship")?.Children ?? new List<ClausewitzNode>())
					.Select(entry => entry.Value == "1" ? "1" : "0"));
				string key = $"{convoy.GetString("start")}->{convoy.GetString("end")} {goods}";
				theirs[key] = (path, Whole(convoy.GetString("convoys")), Whole(convoy.GetString("escorts")));
			}

			var mine = new Dictionary<string, CConvoy>();
			foreach (CConvoy convoy in country.Convoys)
			{
				string goods = string.Concat(convoy.Carries.Select(carried => carried ? "1" : "0"));
				mine[$"{convoy.Start}->{convoy.End} {goods}"] = convoy;
			}
			if (theirs.Count == 0 && mine.Count == 0)
			{
				continue;
			}

			countriesWithConvoys++;
			saved += theirs.Count;
			ours += mine.Count;
			bool show = onlyCountry is null ? notes.Count < examples : country.Tag.Value == onlyCountry;
			int matched = 0;
			foreach ((string key, (List<int> path, int transports, int escorts)) in theirs)
			{
				if (!mine.TryGetValue(key, out CConvoy? convoy))
				{
					if (show)
					{
						notes.Add($"  {country.Tag}: the save has {key} ({transports} transports), we have not");
					}
					continue;
				}
				matched++;
				both++;
				bool path1 = convoy.Path.SequenceEqual(path);
				samePath += path1 ? 1 : 0;
				sameLength += convoy.Path.Count == path.Count ? 1 : 0;
				sameTransports += convoy.Transports == transports ? 1 : 0;
				sameEscorts += convoy.Escorts == escorts ? 1 : 0;
				if (show && (!path1 || convoy.Transports != transports || convoy.Escorts != escorts))
				{
					notes.Add($"  {country.Tag}: {key}: transports {convoy.Transports} for the save's {transports}, "
						+ $"escorts {convoy.Escorts} for {escorts}, way of {convoy.Path.Count} for {path.Count}"
						+ (path1 ? "" : $"\n      ours   {string.Join(' ', convoy.Path)}\n      theirs {string.Join(' ', path)}"));
				}
			}
			foreach ((string key, CConvoy convoy) in mine)
			{
				if (!theirs.ContainsKey(key) && show)
				{
					notes.Add($"  {country.Tag}: we have {key} ({convoy.Transports} transports), the save has not");
				}
			}
			if (matched == theirs.Count && matched == mine.Count)
			{
				sameSet++;
			}

			int freeTransports = Whole(node.GetString("convoys"));
			int freeEscorts = Whole(node.GetString("escorts"));
			sameFreeTransports += freeTransports == country.Transports ? 1 : 0;
			sameFreeEscorts += freeEscorts == country.Escorts ? 1 : 0;
			if ((freeTransports != country.Transports || freeEscorts != country.Escorts) && show)
			{
				notes.Add($"  {country.Tag}: {country.Transports} transports free for the save's {freeTransports}, "
					+ $"{country.Escorts} escorts for {freeEscorts} (base IC {country.BaseIc}, before the resource cap {country.MaxIc})");
			}
		}

		Console.WriteLine("convoys");
		Console.WriteLine($"  the save has {saved}, we have {ours}; {both} are the same run with the same goods");
		Console.WriteLine($"  {sameSet} of {countriesWithConvoys} countries with convoys have just the save's");
		Console.WriteLine($"  of the {both}: {samePath} go the same way province for province, {sameLength} through as many provinces;");
		Console.WriteLine($"  {sameTransports} have the save's transports, {sameEscorts} its escorts");
		Console.WriteLine($"  free transports as the save's for {sameFreeTransports} of {countriesWithConvoys} countries, free escorts for {sameFreeEscorts}");
		foreach (string note in notes.Take(onlyCountry is null ? examples : int.MaxValue))
		{
			Console.WriteLine(note);
		}
		Console.WriteLine();
	}

	private static readonly EGoods[] Shipped =
	{
		EGoods.Money, EGoods.CrudeOil, EGoods.Metal, EGoods.Energy, EGoods.RareMaterials,
	};

	/// <summary>
	/// Sets the money and the resources lying in every province beside the save's, in
	/// three lots: a country's own capital, which is its stockpile; a capital somebody
	/// else holds, which is the stockpile of a country that does not exist; and any
	/// other province, where only what waits for a ship lies.
	/// </summary>
	public static void ComparePools(CGameState state, Dictionary<int, ClausewitzNode> savedProvinces, int examples)
	{
		var capitalOf = new Dictionary<int, List<CCountry>>();
		foreach (CCountry country in state.Countries)
		{
			if (country.Capital != 0)
			{
				if (!capitalOf.TryGetValue(country.Capital, out List<CCountry>? list))
				{
					capitalOf[country.Capital] = list = new List<CCountry>();
				}
				list.Add(country);
			}
		}

		string[] lots = { "a country's own capital", "a capital somebody else holds", "any other province" };
		var compared = new int[lots.Length];
		var same = new int[lots.Length];
		var near = new int[lots.Length];
		var notes = new List<string>[] { new(), new(), new() };
		foreach ((int id, ClausewitzNode node) in savedProvinces)
		{
			var province = state.Provinces[id];
			if (province.IsSea)
			{
				continue;
			}
			int lot = !capitalOf.TryGetValue(id, out List<CCountry>? owners) ? 2
				: owners.Exists(country => country.Tag == province.Controller) ? 0 : 1;
			ClausewitzNode? pool = node.Get("pool");
			foreach (EGoods goods in Shipped)
			{
				decimal theirs = decimal.TryParse(pool?.GetString(CGoodsPool.KeyOf(goods)), NumberStyles.Float,
					CultureInfo.InvariantCulture, out decimal parsed) ? parsed : 0m;
				decimal ours = province.Pool[goods].Raw / 1000m;
				if (theirs == 0m && ours == 0m)
				{
					continue;
				}
				compared[lot]++;
				decimal apart = Math.Abs(theirs - ours);
				if (apart < 0.0015m)
				{
					same[lot]++;
				}
				else if (notes[lot].Count < examples)
				{
					string of = owners is null ? "" : $", capital of {string.Join(',', owners.Select(country => country.Tag.Value))}";
					notes[lot].Add($"      {id} {province.Name} ({province.Controller}{of}) {CGoodsPool.KeyOf(goods)}: save {theirs:0.000}, ours {ours:0.000}");
				}
				if (apart <= Math.Max(0.0015m, Math.Abs(theirs) / 50m))
				{
					near[lot]++;
				}
			}
		}

		Console.WriteLine("money and resources lying in a province, where there are any in the save or here");
		for (int lot = 0; lot < lots.Length; lot++)
		{
			Console.WriteLine($"  {lots[lot],-30} {same[lot],4} of {compared[lot],4} the same to the thousandth, {near[lot],4} within a fiftieth");
			foreach (string note in notes[lot])
			{
				Console.WriteLine(note);
			}
		}
		Console.WriteLine();
	}

	private static int Whole(string? text)
	{
		return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) ? value : 0;
	}
}
