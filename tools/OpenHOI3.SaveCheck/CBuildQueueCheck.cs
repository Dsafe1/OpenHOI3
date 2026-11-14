using System;
using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Content.Clausewitz;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Construction;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;

/// <summary>
/// The build queue beside the save's: <c>docs/mechanics/buildqueue.md</c>.
///
/// Two things are set side by side. What a line costs and how long it takes are what
/// the country's laws and technology make them the last time they were worked out,
/// which a change of law does - so they are compared under the laws the save shows.
/// How far a line has got is what two days of the production share did to it, which
/// is run under the laws the two days are judged to have had, with the units.
/// </summary>
internal static class CBuildQueueCheck
{
	private const string Block = "military_construction";

	private sealed record Kept(CFixedPoint Cost, CFixedPoint Duration, CFixedPoint Progress, CFixedPoint Status,
		CFixedPoint AccumulatedExperience, CFixedPoint AccumulatedProgress, CFixedPoint ReservesFactor, int Size, int UnitId);

	/// <summary>A country's lines as its file gave them.</summary>
	public sealed class CKept
	{
		private readonly Dictionary<int, Kept> _figures = new();

		public CKept(CCountry country)
		{
			Lines = new List<CConstruction>(country.Constructions);
			foreach (CConstruction construction in Lines)
			{
				_figures[construction.Id] = new Kept(construction.Cost, construction.Duration, construction.Progress,
					construction.Status, construction.AccumulatedExperience, construction.AccumulatedProgress,
					construction.ReservesFactor, construction.Size, construction.UnitId);
			}
		}

		public List<CConstruction> Lines { get; }

		/// <summary>The days a line's file says it still has to go.</summary>
		public CFixedPoint DaysToGo(CConstruction construction)
		{
			return _figures[construction.Id].Duration;
		}

		public void PutBack(CConstruction construction)
		{
			Kept was = _figures[construction.Id];
			construction.Cost = was.Cost;
			construction.Duration = was.Duration;
			construction.Progress = was.Progress;
			construction.Status = was.Status;
			construction.AccumulatedExperience = was.AccumulatedExperience;
			construction.AccumulatedProgress = was.AccumulatedProgress;
			construction.ReservesFactor = was.ReservesFactor;
			construction.Size = was.Size;
			construction.UnitId = was.UnitId;
		}
	}

	/// <summary>
	/// Puts a country's lines back as its file gave them and does to them what a new
	/// game does, with the laws and technology the country has now.
	/// </summary>
	public static void SetUpAsAtStart(CGameState state, CCountry country, CKept kept)
	{
		country.Constructions.Clear();
		country.Deployments.Clear();
		foreach (CConstruction construction in kept.Lines)
		{
			kept.PutBack(construction);
			country.Constructions.Add(construction);

			foreach (CBrigadeOrder order in construction.Brigades)
			{
				CBuildQueue.SetModelToHistorical(state, country, order, order.HistoricalModel);
			}
			long left = construction.Duration.Raw;
			CBuildQueue.Recalculate(country, construction);
			construction.Progress = new CFixedPoint(Math.Max(0, construction.Duration.Raw - left));
		}
		CBuildQueue.UpdateCost(country);
	}

	/// <summary>One day of the production share, with nothing charged.</summary>
	public static void RunDay(CGameState state, CCountry country)
	{
		CFixed15 allocation = country.ProductionShares[(int)EProductionCategory.Production] * CFixed15.FromInt(country.TotalIc);
		country.ProductionSpent = CBuildQueue.RunDaily(state, country, allocation);
	}

	/// <summary>The save's lines for one country, by number.</summary>
	public static Dictionary<int, ClausewitzNode> Collect(ClausewitzNode country)
	{
		var found = new Dictionary<int, ClausewitzNode>();
		foreach (ClausewitzNode child in country.Children)
		{
			if (child.IsBlock && Block.Equals(child.Key, StringComparison.OrdinalIgnoreCase)
				&& child.Get("id") is { } id)
			{
				found[id.GetInt("id")] = child;
			}
		}
		return found;
	}

	/// <summary>How many of a country's lines are not as far on as the save has them.</summary>
	public static int Faults(CCountry country, IReadOnlyDictionary<int, ClausewitzNode> saved)
	{
		int faults = 0;
		foreach (CConstruction construction in country.Constructions)
		{
			if (saved.TryGetValue(construction.Id, out ClausewitzNode? line)
				&& (Number(line.GetString("progress")) != construction.Progress
					|| Number(line.GetString("accumulated_progress")) != construction.AccumulatedProgress))
			{
				faults++;
			}
		}
		return faults;
	}

	/// <summary>Sets a country's lines, two days on, beside the save's.</summary>
	/// <param name="startedAt">
	/// Where the new game put each line, by its number: priced under the laws the new game brought forward and the
	/// ministers its history gave, before the Lua's changes of law, which come after the two days.
	/// </param>
	public static void CompareProgress(CCountry country, IReadOnlyDictionary<int, ClausewitzNode> saved, bool isPlayer,
		CKept kept, IReadOnlyDictionary<int, CFixedPoint> startedAt, Action<string, bool, Func<string>> compare)
	{
		const int daysBehind = 2;
		string tag = country.Tag.Value;
		string whose = isPlayer ? "the player's country" : "the others, whose shares an AI moves";
		compare("the build queue: as many lines as the save has", saved.Count == country.Constructions.Count,
			() => $"{tag}: save {saved.Count}, ours {country.Constructions.Count}");

		foreach (CConstruction construction in country.Constructions)
		{
			if (!saved.TryGetValue(construction.Id, out ClausewitzNode? line))
			{
				compare("the build queue: each line has the number the save gives it", false,
					() => $"{tag} {construction.Id} {construction}: the save has no line of that number");
				continue;
			}
			compare("the build queue: each line has the number the save gives it", true, () => "");
			string who = $"{tag} {construction.Id} '{construction.Brigades.FirstOrDefault()?.Name}' {construction.Brigades.FirstOrDefault()?.Type.Key}";
			string facts = $"(cost {construction.Cost}, {construction.Duration} days, the share ran to " +
				$"{country.ProductionSpent} of {country.ProductionCost})";

			CFixedPoint progress = Number(line.GetString("progress"));
			compare($"the build queue, two days on: how far each line has got, {whose}", progress == construction.Progress,
				() => $"{who}: save {progress}, ours {construction.Progress}  {facts}");

			// What the shares did in the two days aside: the save's line is where a new
			// game put it, and at most two days' work on. A new game prices its lines
			// under the laws it brought forward, before the Lua's changes come after the
			// days and price them again, the work done left alone (buildqueue.md, section 5).
			CFixedPoint started = startedAt.GetValueOrDefault(construction.Id);
			compare("the build queue: where each line stood as the game started, by the save's less at most two days",
				progress >= started && progress <= started + CFixedPoint.FromInt(daysBehind),
				() => $"{who}: save {progress} two days on, ours started at {started}  " +
					$"({construction.Duration} days in all under the save's laws, {kept.DaysToGo(construction)} to go by its file)");

			CFixedPoint counted = Number(line.GetString("accumulated_progress"));
			compare($"the build queue, two days on: how much of the building has been counted, {whose}",
				counted == construction.AccumulatedProgress,
				() => $"{who}: save {counted}, ours {construction.AccumulatedProgress}  {facts}");

			CFixedPoint experience = Number(line.GetString("accumulated_experiance"));
			compare($"the build queue, two days on: the experience gathered, {whose}",
				experience == construction.AccumulatedExperience,
				() => $"{who}: save {experience}, ours {construction.AccumulatedExperience}  {facts}");

			CFixedPoint status = Number(line.GetString("status"));
			compare($"the build queue, two days on: how much of a day each line was last paid, {whose}",
				status == construction.Status,
				() => $"{who}: save {status}, ours {construction.Status}  {facts}");
		}
	}

	/// <summary>
	/// Sets what each line costs and takes beside the save's, under the laws the
	/// country has now, and the equipment and the numbers of what it builds.
	/// </summary>
	public static void ComparePrices(CGameState state, CCountry country, IReadOnlyDictionary<int, ClausewitzNode> saved,
		Action<string, bool, Func<string>> compare)
	{
		string tag = country.Tag.Value;
		foreach (CConstruction construction in country.Constructions)
		{
			if (!saved.TryGetValue(construction.Id, out ClausewitzNode? line))
			{
				continue;
			}
			CBrigadeOrder? first = construction.Brigades.FirstOrDefault();
			string who = $"{tag} {construction.Id} '{first?.Name}' {first?.Type.Key}";

			CFixedPoint cost = Number(line.GetString("cost"));
			CFixedPoint duration = Number(line.GetString("duration"));
			CFixedPoint wasCost = construction.Cost;
			CFixedPoint wasDuration = construction.Duration;
			CBuildQueue.Recalculate(country, construction);
			CFixedPoint ourCost = construction.Cost;
			CFixedPoint ourDuration = construction.Duration;
			construction.Cost = wasCost;
			construction.Duration = wasDuration;

			string facts = first is null ? "" :
				$"(practical {first.Type.OnCompletion} {country.Ability(first.Type.OnCompletion)}, model {string.Join(" ", first.Model)})";
			compare("the build queue: what a line costs a day, under the laws the save shows", cost == ourCost,
				() => $"{who}: save {cost}, ours {ourCost}  {facts}");
			compare("the build queue: the days a line takes, under the laws the save shows", duration == ourDuration,
				() => $"{who}: save {duration}, ours {ourDuration}  {facts}");

			compare("the build queue: the number of the unit a line will make",
				line.Get("unit")?.GetInt("id") == construction.UnitId,
				() => $"{who}: save {line.Get("unit")?.GetInt("id")}, ours {construction.UnitId}");

			foreach (CBrigadeOrder order in construction.Brigades)
			{
				if (line.Get(order.Type.Key) is not { IsBlock: true } brigade)
				{
					compare("the build queue: each brigade of a line is in the save's", false,
						() => $"{who}: the save's line has no {order.Type.Key}");
					continue;
				}
				compare("the build queue: the number of each brigade a line will make",
					brigade.Get("id")?.GetInt("id") == order.Id,
					() => $"{who}: save {brigade.Get("id")?.GetInt("id")}, ours {order.Id}");

				List<int> model = brigade.Get("model")?.Children.Select(level => (int)Number(level.Value).ToInt()).ToList()
					?? new List<int>();
				compare("the build queue: the equipment each brigade of a line will have", model.SequenceEqual(order.Model),
					() => $"{who}: save {string.Join(" ", model)}, ours {string.Join(" ", order.Model)}  " +
						$"(model {order.HistoricalModel}; the type lists {string.Join(" ", order.Type.Technologies.Select(technology => technology.Name))})");
			}
		}
	}

	private static CFixedPoint Number(string? text)
	{
		return CFixedPoint.TryParse(text, out CFixedPoint value) ? value : CFixedPoint.Zero;
	}
}
