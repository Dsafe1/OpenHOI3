using System;
using System.Globalization;
using System.Linq;
using System.Text;
using OpenHOI3.Interface.Map;
using OpenHOI3.Interface.Text;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Combat;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Intelligence;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Supply;
using OpenHOI3.Sim.Theatres;

namespace OpenHOI3.Interface.Screens;

/// <summary>
/// What the province's tooltip adds in the map modes built on 2026-11-07, each after the province's name and owner, as the
/// original writes it. See <c>docs/mechanics/mapmodes.md</c>; the readings are
/// <c>reversing/findings/FINDINGS-mapmodesfigures.md</c>, <c>FINDINGS-mapmodescountries.md</c> and
/// <c>FINDINGS-mapmodesrest.md</c>.
/// </summary>
public static class CMapModeText
{
	/// <summary>The four goods the resources mode lists, in its order: crude oil last.</summary>
	private static readonly (EGoods Good, string Key)[] Goods =
	{
		(EGoods.Metal, "METAL_IRO"), (EGoods.Energy, "ENERGY_IRO"), (EGoods.RareMaterials, "RAREMAT_IRO"), (EGoods.CrudeOil, "CRUDE_OIL_IRO"),
	};

	/// <summary>The infrastructure as a percentage to two places, with no label, where the player knows the province at all.</summary>
	public static string Infrastructure(CGameState state, CProvince province, CCountryTag? player)
	{
		if (Known(state, province, player) < 1)
		{
			return "";
		}
		var percent = new CFixedPoint(province.Modifier["infrastructure"].Raw * 100);
		return "\n" + CNumberText.Format(percent, 2) + "%";
	}

	/// <summary>"Points: N", always - at sea and for none.</summary>
	public static string VictoryPoints(Func<string, string> localise, CProvince province)
	{
		return "\n" + localise("PROV_POINTS") + ": " + province.Points.ToString(CultureInfo.InvariantCulture);
	}

	/// <summary>Each good the province can yield - now and at most, a place each - then its strategic resource in green.</summary>
	public static string Resources(Func<string, string> localise, CGameState state, CProvince province, CCountryTag? player)
	{
		var text = new StringBuilder();
		if (Known(state, province, player) >= 1)
		{
			foreach ((EGoods good, string key) in Goods)
			{
				long most = province.MaxProducing[good].Raw;
				if (most > 0)
				{
					long now = province.CurrentProducing[good].Raw;
					text.Append('\n').Append(localise(key)).Append(": ").Append(OnePlace(now)).Append('/').Append(OnePlace(most));
				}
			}
		}
		if (province.StrategicResource.Length > 0)
		{
			text.Append("\n§G").Append(localise(province.StrategicResource)).Append("§W");
		}
		return text.ToString();
	}

	/// <summary>The owner and how the player stands towards it, on land with an owner; the player's own says nothing after it.</summary>
	public static string Diplomatic(Func<string, string> localise, CGameState state, CProvince province, CCountryTag? player)
	{
		if (province.IsSea || province.Owner is not { } ownerTag || state.Country(ownerTag) is not { } owner
			|| player is not { } us || state.Country(us) is not { } ours)
		{
			return "";
		}
		string status;
		if (CCountryLists.AtWar(state, us, ownerTag))
		{
			status = localise("ATWARWITHUS");
		}
		else if (CCountryLists.FightingTogether(state, us, ownerTag))
		{
			status = localise("OURWARALLY");
		}
		else if (ours.Overlord == ownerTag)
		{
			// The bond whose overlord is the owner: the player is its puppet, whatever the key's name says.
			status = localise("OURVASSAL");
		}
		else if (owner.Overlord == us)
		{
			status = CTextTemplate.Fill(localise("DIP_THEY_PUPPET"), ("OVERLORD", ours.Name));
		}
		else if (CCountryLists.HasAlliance(state, us, ownerTag))
		{
			status = localise("ALLIEDWITHTHEM");
		}
		else if (state.Guarantees.Any(g => g.Guarantor == us && g.Guaranteed == ownerTag))
		{
			status = localise("WEGUARANTEED");
		}
		else if (state.Guarantees.Any(g => g.Guarantor == ownerTag && g.Guaranteed == us))
		{
			status = localise("THEYGUARANTING");
		}
		else
		{
			status = ownerTag == us ? "" : localise("NEUTRAL");
		}
		return "\n " + owner.Name + ": " + status;
	}

	/// <summary>The name of what the player knows of the province, on the sea too.</summary>
	public static string Intel(Func<string, string> localise, CGameState state, CProvince province, CCountryTag? player)
	{
		if (player is null)
		{
			return "";
		}
		int known = Known(state, province, player);
		string key = known switch
		{
			<= 0 => "INTEL_NONE",
			1 => "INTEL_STRUCTURES",
			2 => "INTEL_UNITS",
			>= 9 => "MAX_INTEL_LEVEL",
			_ => "INTEL_" + (known - 1).ToString(CultureInfo.InvariantCulture),
		};
		return "\n" + localise(key);
	}

	/// <summary>Where the controller is a friend of the player's, the headquarters of the province's theatre, in brackets.</summary>
	public static string Theatre(CGameState state, CProvince province, CCountryTag? player)
	{
		if (player is not { } us || province.Controller is not { } held || state.Country(held) is not { } holder
			|| !CCountryLists.IsFriend(state, holder, us, withAccess: false))
		{
			return "";
		}
		foreach (CTheatre theatre in state.Theatres.Values)
		{
			if (!theatre.AllProvinces.Contains(province.Id) || state.Country(theatre.Country) is not { } country)
			{
				continue;
			}
			if (country.AllUnits().FirstOrDefault(unit => unit.Id == theatre.Headquarters) is { } headquarters)
			{
				return "\n(" + headquarters.Name + ")";
			}
		}
		return "";
	}

	/// <summary>
	/// Each region that lists the province, peace regions starred - not with the editor's continent painting on - then its
	/// continent with a capital letter.
	/// </summary>
	public static string Region(Func<string, string> localise, CGameState state, CProvince province, bool continentPainting = false)
	{
		var text = new StringBuilder();
		foreach (int index in continentPainting ? System.Array.Empty<int>() : (System.Collections.Generic.IReadOnlyList<int>)province.Regions)
		{
			if (index < 0 || index >= state.Regions.Count)
			{
				continue;
			}
			CRegion region = state.Regions[index];
			text.Append("\n - ").Append(RegionName(localise, state, region));
			if (region.UseForPeace)
			{
				text.Append('*');
			}
		}
		if (province.Continent.Length > 0)
		{
			string name = localise(province.Continent);
			text.Append('\n').Append(name.Length > 0 ? char.ToUpperInvariant(name[0]) + name[1..] : name);
		}
		return text.ToString();
	}

	/// <summary>A region's own name where its key has one, else "$NAME$ Region" with its first province's name.</summary>
	public static string RegionName(Func<string, string> localise, CGameState state, CRegion region)
	{
		string name = localise(region.Name);
		if (name != region.Name || region.Provinces.Count == 0)
		{
			return name;
		}
		int first = region.Provinces[0];
		string province = first > 0 && first < state.Provinces.Count ? state.Provinces[first].Name : "";
		return CTextTemplate.Fill(localise("REGION_NAME"), ("NAME", province));
	}

	/// <summary>The past week's naval battles in the province that concern the player, counted four ways, each line above nought.</summary>
	public static string Naval(Func<string, string> localise, CGameState state, CProvince province, CCountryTag? player)
	{
		if (player is not { } us || state.Country(us) is not { } ours)
		{
			return "";
		}
		int raids = 0, sunk = 0, attacked = 0, defended = 0;
		foreach (CFinishedCombat entry in state.CombatHistory.Entries)
		{
			if (entry.Province != province.Id || entry.Kind != ECombatKind.Naval)
			{
				continue;
			}
			bool? attacking = Side(state, ours, entry);
			if (attacking is null)
			{
				continue;
			}
			if (entry.IsConvoy)
			{
				if (attacking.Value) raids++; else sunk++;
			}
			else if (attacking.Value)
			{
				attacked++;
			}
			else
			{
				defended++;
			}
		}
		var text = new StringBuilder();
		foreach ((int count, string key) in new[]
		{
			(raids, "NAVAL_TOOLTIP_RAIDS"), (sunk, "NAVAL_TOOLTIP_CONVOYS_SUNK"),
			(attacked, "NAVAL_TOOLTIP_RAIDER_ATTACKED"), (defended, "NAVAL_TOOLTIP_RAIDER_DEFENDED"),
		})
		{
			if (count > 0)
			{
				text.Append('\n').Append(CTextTemplate.Fill(localise(key), ("NUM", count.ToString(CultureInfo.InvariantCulture))));
			}
		}
		return text.ToString();
	}

	/// <summary>Whether the player's side attacked in a naval battle, defended in it, or neither: null where it is not counted.</summary>
	private static bool? Side(CGameState state, CCountry ours, CFinishedCombat entry)
	{
		if (entry.Attacker is { } attacker && (attacker == ours.Tag
			|| state.Country(attacker) is { } a && CSupplyRelations.CanOperateIn(state, ours, a)))
		{
			return true;
		}
		if (entry.Defender is { } defender && (defender == ours.Tag
			|| state.Country(defender) is { } d && CSupplyRelations.CanOperateIn(state, ours, d)))
		{
			return false;
		}
		if (CAirMapIcons.IsEnemy(state, ours.Tag, entry.Defender))
		{
			return true;
		}
		return null;
	}

	private static int Known(CGameState state, CProvince province, CCountryTag? player) =>
		player is { } tag ? CProvinceIntel.Level(state, province, tag) : 0;

	/// <summary>A figure in thousandths to one place, rounded, as the original's <c>%.1f</c> writes it.</summary>
	private static string OnePlace(long thousandths) =>
		(thousandths / 1000.0).ToString("0.0", CultureInfo.InvariantCulture);
}
