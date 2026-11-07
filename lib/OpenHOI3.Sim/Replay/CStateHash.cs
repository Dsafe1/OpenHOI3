using System;
using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Replay;

/// <summary>
/// A fingerprint of a game, part by part: two runs that have done the same have the same one, and where they part the
/// first part to differ says where to look. It reads what the game is - countries, what they hold of each other,
/// provinces, units, wars, and how far the generator has gone - and leaves out what the AI keeps for itself (its theatres,
/// agents and plans), which a replay does not make. Ours; see <c>docs/replay.md</c>.
/// </summary>
public static class CStateHash
{
	/// <summary>The parts, in the order they are told.</summary>
	public static readonly string[] Parts = { "generator", "countries", "diplomacy", "provinces", "units", "wars" };

	/// <summary>Each part's hash, in the order of <see cref="Parts"/>.</summary>
	public static ulong[] Of(CGameState state)
	{
		return new[]
		{
			Generator(state),
			Countries(state),
			Diplomacy(state),
			Provinces(state),
			Units(state),
			Wars(state),
		};
	}

	/// <summary>The parts' hashes as one.</summary>
	public static ulong Whole(ulong[] parts)
	{
		var hash = new CHasher();
		foreach (ulong part in parts)
		{
			hash.Add(part);
		}
		return hash.Value;
	}

	private static ulong Generator(CGameState state)
	{
		var hash = new CHasher();
		hash.Add(state.TotalHours);
		hash.Add(state.Random.Draws);
		return hash.Value;
	}

	private static ulong Countries(CGameState state)
	{
		var hash = new CHasher();
		foreach (KeyValuePair<string, ulong> field in CountryFields(state))
		{
			hash.Add(field.Value);
		}
		return hash.Value;
	}

	/// <summary>
	/// The countries part field by field, keyed by the tag and the field's name - which of a country's figures two runs
	/// part on.
	/// </summary>
	public static IEnumerable<KeyValuePair<string, ulong>> CountryFields(CGameState state)
	{
		foreach (CCountry country in state.Countries)
		{
			string tag = country.Tag.Value;
			yield return Field(tag, "manpower", h => h.Add(country.Manpower.Raw));
			yield return Field(tag, "officers", h => h.Add(country.Officers.Raw));
			yield return Field(tag, "national unity", h => h.Add(country.NationalUnity.Raw));
			yield return Field(tag, "dissent", h => h.Add(country.Dissent.Raw));
			yield return Field(tag, "war_exhaustion", h => h.Add(country.WarExhaustion.Raw));
			yield return Field(tag, "neutrality", h => h.Add(country.Neutrality.Raw));
			yield return Field(tag, "faction", h => h.Add(country.Faction));
			yield return Field(tag, "mobilised", h => h.Add(country.Mobilised));
			yield return Field(tag, "IC shares", h =>
			{
				foreach (CFixed15 share in country.ProductionShares)
				{
					h.Add(share.Raw);
				}
			});
			yield return Field(tag, "leadership shares", h =>
			{
				foreach (CFixed15 share in country.LeadershipShares)
				{
					h.Add(share.Raw);
				}
			});
			yield return Field(tag, "laws", h =>
			{
				foreach (KeyValuePair<string, CLaw> law in country.Laws.OrderBy(pair => pair.Key, StringComparer.Ordinal))
				{
					h.Add(law.Key);
					h.Add(law.Value.Name);
				}
			});
			yield return Field(tag, "ministers", h =>
			{
				foreach (KeyValuePair<int, CMinister> post in country.Ministers.OrderBy(pair => pair.Key))
				{
					h.Add(post.Key);
					h.Add(post.Value.Id);
				}
			});
			yield return Field(tag, "technology", h =>
			{
				foreach (KeyValuePair<string, int> level in country.TechnologyLevels.OrderBy(pair => pair.Key, StringComparer.Ordinal))
				{
					h.Add(level.Key);
					h.Add(level.Value);
				}
			});
			yield return Field(tag, "research progress", h =>
			{
				foreach (KeyValuePair<string, CFixed15> progress in country.ResearchProgress.OrderBy(pair => pair.Key, StringComparer.Ordinal))
				{
					h.Add(progress.Key);
					h.Add(progress.Value.Raw);
				}
			});
			yield return Field(tag, "research queue", h =>
			{
				foreach (string queued in country.ResearchQueue)
				{
					h.Add(queued);
				}
			});
			yield return Field(tag, "build queue", h =>
			{
				foreach (Construction.CConstruction construction in country.Constructions)
				{
					h.Add(construction.Id);
					h.Add(construction.Name);
					h.Add(construction.Progress.Raw);
					h.Add(construction.Location);
				}
			});
			yield return Field(tag, "stockpile", h =>
			{
				if (state.Stockpile(country) is { } pool)
				{
					for (int goods = 0; goods < CGoodsPool.Count; goods++)
					{
						h.Add(pool[(EGoods)goods].Raw);
					}
				}
			});
		}
	}

	private static KeyValuePair<string, ulong> Field(string tag, string name, Action<CHasher> add)
	{
		var box = new CHasher();
		add(box);
		return new KeyValuePair<string, ulong>($"{tag} {name}", box.Value);
	}

	private static ulong Diplomacy(CGameState state)
	{
		var hash = new CHasher();
		foreach (CCountry country in state.Countries)
		{
			foreach (Diplomacy.CDiplomacyStatus status in country.Diplomacy)
			{
				if (status is null)
				{
					continue;
				}
				hash.Add(status.Other.Value);
				hash.Add(status.Relation.Raw);
				hash.Add(status.Threat.Raw);
				hash.Add(status.HasMilitaryAccess);
				hash.Add(status.Debt);
				hash.Add(status.OccupationPolicy);
			}
		}
		hash.Add(state.Alliances.Count);
		hash.Add(state.Guarantees.Count);
		hash.Add(state.Naps.Count);
		return hash.Value;
	}

	private static ulong Provinces(CGameState state)
	{
		return Of(ProvinceFields(state));
	}

	private static ulong Units(CGameState state)
	{
		return Of(UnitFields(state));
	}

	private static ulong Of(IEnumerable<KeyValuePair<string, ulong>> fields)
	{
		var hash = new CHasher();
		foreach (KeyValuePair<string, ulong> field in fields)
		{
			hash.Add(field.Key);
			hash.Add(field.Value);
		}
		return hash.Value;
	}

	/// <summary>
	/// The countries, provinces and units field by field, keyed by what each is - which figures two runs part on, where the
	/// parts' hashes say only that they do.
	/// </summary>
	public static IEnumerable<KeyValuePair<string, ulong>> Fields(CGameState state)
	{
		return CountryFields(state).Concat(ProvinceFields(state)).Concat(UnitFields(state));
	}

	/// <summary>Each province's holders and its pool.</summary>
	public static IEnumerable<KeyValuePair<string, ulong>> ProvinceFields(CGameState state)
	{
		foreach (Provinces.CProvince province in state.Provinces)
		{
			string key = $"province {province.Id}";
			yield return Field(key, "holders", h =>
			{
				h.Add(province.Owner?.Value ?? "");
				h.Add(province.Controller?.Value ?? "");
			});
			yield return Field(key, "pool", h =>
			{
				for (int goods = 0; goods < CGoodsPool.Count; goods++)
				{
					h.Add(province.Pool[(EGoods)goods].Raw);
				}
			});
		}
	}

	/// <summary>Each unit of each country, by its number: where it is and is going, its order and its brigades.</summary>
	public static IEnumerable<KeyValuePair<string, ulong>> UnitFields(CGameState state)
	{
		foreach (CCountry country in state.Countries)
		{
			foreach (CUnit unit in country.AllUnits().OrderBy(unit => unit.Id))
			{
				string key = $"{country.Tag.Value} {unit.Kind} {unit.Id} '{unit.Name}'";
				yield return Field(key, "hierarchy", h => h.Add(unit.Parent?.Id ?? 0));
				yield return Field(key, "place", h =>
				{
					h.Add(unit.Location);
					h.Add(unit.Base);
					h.Add(unit.MovementProgress.Raw);
					h.Add(unit.Path.Count);
					foreach (int step in unit.Path)
					{
						h.Add(step);
					}
					h.Add(unit.IsRetreating);
				});
				yield return Field(key, "order", h =>
				{
					h.Add((int)unit.Order);
					h.Add(unit.OrderProvince);
				});
				yield return Field(key, "attack delay", h => h.Add(unit.AttackDelay.Raw));
				yield return Field(key, "leader", h => h.Add(unit.LeaderId));
				yield return Field(key, "supplies", h => h.Add(unit.SuppliesReceived.Raw));
				yield return Field(key, "brigades", h =>
				{
					foreach (CSubUnit subUnit in unit.SubUnits)
					{
						h.Add(subUnit.Strength.Raw);
						h.Add(subUnit.Organisation.Raw);
						h.Add(subUnit.Experience.Raw);
					}
				});
			}
		}
	}

	private static ulong Wars(CGameState state)
	{
		var hash = new CHasher();
		foreach (Wars.CWar war in state.Wars)
		{
			hash.Add(war.Name);
			foreach (CCountryTag tag in war.Attackers)
			{
				hash.Add(tag.Value);
			}
			hash.Add("|");
			foreach (CCountryTag tag in war.Defenders)
			{
				hash.Add(tag.Value);
			}
		}
		return hash.Value;
	}
}
