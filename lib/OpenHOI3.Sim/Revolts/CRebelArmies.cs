using System;
using System.Collections.Generic;
using System.Globalization;
using OpenHOI3.Sim.Agreements;
using OpenHOI3.Sim.Combat;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Revolts;

/// <summary>
/// The army a rising makes: whose it is, how many brigades, what they are called, what they know and how well they
/// fight - and who is told. See <c>docs/mechanics/rebels.md</c>, section 7.
/// </summary>
public static class CRebelArmies
{
	/// <summary>The fewest and the most brigades in one rising's army.</summary>
	public const int Fewest = 1;
	public const int Most = 4;

	/// <summary>The experience a rebel brigade starts from before its target's partisan efficiency: -1.0.</summary>
	public static readonly CFixedPoint FirstExperience = CFixedPoint.FromInt(-1);

	/// <summary>The modifier a brigade's experience is raised by, the first target's.</summary>
	public const string Efficiency = "partisan_efficiency";

	/// <summary>Who reports a rising.</summary>
	public const string Messenger = "MESSAGE_HEAD_MARS";

	/// <summary>
	/// The original's <c>CreateRebelsForFaction</c>: nothing for nought brigades; the army the rebels', the underground's
	/// country's or - for an <c>auto_convert</c> type in the land of an exile that does not hold it - the exile's; one to
	/// four brigades of the type's unit, each at -1.0 experience plus the first target's partisan efficiency, its
	/// technologies drawn below the first target's levels, at full strength and organisation; no leader. It stands in the
	/// province and looks for a battle there, and the holder or the army's owner is told.
	/// </summary>
	public static CUnit? Make(CGameState state, CRebelFaction faction, CProvince province, int count, bool forActor,
		CCountryTag? actor)
	{
		if (count == 0)
		{
			return null;
		}
		CCountryTag owner = CRebelFactions.Tag;
		if (forActor && actor is { } underground)
		{
			owner = underground;
		}
		else if (faction.Type.AutoConvert && province.Owner is { } exile && state.Country(exile) is { InExile: true }
			&& province.Owner != province.Controller)
		{
			owner = exile;
		}
		count = Math.Clamp(count, Fewest, Most);
		if (state.Country(owner) is not { } ownerCountry || !state.SubUnitTypes.TryGetValue(faction.Type.Unit, out CSubUnitType? type))
		{
			return null;
		}
		CCountry namer = state.Country(faction.Country) ?? ownerCountry;
		var army = new CUnit(EUnitKind.Division, owner)
		{
			Id = ++state.LastUnitId,
			Name = ArmyName(state, faction),
			Location = province.Id,
		};
		ownerCountry.Units.Add(army);
		faction.Armies.Add(army.Id);
		// The army and each brigade also take the next numbers of the rebels' counter, after the faction's.
		state.RebelSerials[army.Id] = ++state.LastRebelId;
		CCountry? target = faction.Targets.Count > 0 ? state.Country(faction.Targets[0]) : null;
		for (int made = 0; made < count; made++)
		{
			var brigade = new CSubUnit(type) { Id = ++state.LastUnitId, Unit = army, Home = province.Id };
			state.RebelSerials[brigade.Id] = ++state.LastRebelId;
			brigade.Name = CUnitNames.ForSubUnit(state, namer, type);
			CUnitNames.Register(namer, brigade.Name);
			CFixedPoint efficiency = target?.Modifier[Efficiency] ?? CFixedPoint.Zero;
			brigade.Experience = CFixedPoint.Min(FirstExperience + efficiency, CUnits.MostExperience);
			foreach (Research.CTechnology technology in type.Technologies)
			{
				int level = target?.TechnologyLevels.GetValueOrDefault(technology.Name) ?? 0;
				brigade.TechnologyLevels[technology.Name] = level > 0 ? state.Random.Next() % level : 0;
			}
			SetFull(state, ownerCountry, brigade);
			army.AddSubUnit(brigade);
		}
		// Put in its province from nowhere, it brings its days of supplies and fuel - out of its owner's stockpile, which
		// the rebels have none of (supply.md, section 14).
		Supply.CSupplyCarry.Bring(state, ownerCountry, army, from: 0, province.Id, beforeGame: state.IsBeingMade);
		state.ForgetPositions();
		CCountryTag? holder = province.Controller;
		CCombats.CheckForCombat(state, army, province.Id);
		Tell(state, faction, province, holder, owner);
		return army;
	}

	/// <summary>
	/// A new brigade's figures from its technologies, and its strength and organisation at the most they may be - the
	/// original's <c>ApplyTechnologies</c>, which holds both to the new figures.
	/// </summary>
	public static void SetFull(CGameState state, CCountry owner, CSubUnit brigade)
	{
		CUnits.RebuildStats(brigade);
		brigade.Strength = CFixedPoint.Min(brigade.Strength, CUnits.MaxStrength(state, owner, brigade));
		brigade.Highest = brigade.Strength;
		brigade.Organisation = CFixedPoint.Max(CFixedPoint.Zero,
			CFixedPoint.Min(brigade.Organisation, Units.COrganisation.Ceiling(state, owner, brigade)));
	}

	/// <summary>
	/// The original's <c>RebelFaction_MakeArmyName</c>: "1st", "2nd" and on, a space and the type's <c>_army</c> text,
	/// the first none of the faction's armies is called.
	/// </summary>
	public static string ArmyName(CGameState state, CRebelFaction faction)
	{
		string text = CRebelFactions.Render(state, faction, faction.Type.ArmyText);
		for (int number = 1; ; number++)
		{
			string name = number.ToString(CultureInfo.InvariantCulture) + CUnitNames.Ordinal(state.NameTexts, number) + " " + text;
			if (!IsTaken(state, faction, name))
			{
				return name;
			}
		}
	}

	private static bool IsTaken(CGameState state, CRebelFaction faction, string name)
	{
		foreach (int id in faction.Armies)
		{
			if (CRebelFactions.FindUnit(state, id) is { } army && army.Name == name)
			{
				return true;
			}
		}
		return false;
	}

	/// <summary>
	/// <c>REVOLT</c> where the holder is told, and otherwise <c>PARTISANS</c> where the army's owner is - an exile's or an
	/// underground's partisans. Nobody else.
	/// </summary>
	private static void Tell(CGameState state, CRebelFaction faction, CProvince province, CCountryTag? holder, CCountryTag owner)
	{
		if (holder is not { } held)
		{
			return;
		}
		state.RaisePactMessage(new CPactMessage("REVOLT", "", "", CRebelFactions.Tag, held)
		{
			Told = held,
			Province = province.Id,
			Messenger = Messenger,
			// The holder hears an army's selection with it, the original's choice (sound.md, section 7).
			Sound = "army_selected",
			Values = new[]
			{
				("REBELS", faction.Name),
				("REBELDESC", CRebelFactions.Render(state, faction, faction.Type.DescText)),
				("PROV", province.Name),
			},
		});
		if (owner != held && !CRebelFactions.IsRebels(owner))
		{
			state.RaisePactMessage(new CPactMessage("PARTISANS", "", "", owner, held)
			{
				Told = owner,
				Province = province.Id,
				Messenger = Messenger,
				Values = new[] { ("PROV", province.Name), ("EVIL", state.Country(held)?.Name ?? held.Value) },
			});
		}
	}
}
