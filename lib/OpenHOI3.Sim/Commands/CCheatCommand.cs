using System;
using System.Linq;
using OpenHOI3.Sim.Agreements;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Diplomacy;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Politics;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Revolts;
using OpenHOI3.Sim.Units;
using OpenHOI3.Sim.Wars;

namespace OpenHOI3.Sim.Commands;

/// <summary>
/// A console cheat. The original's write to the game directly; here each is a command, so that a game with cheats in it can
/// be replayed. See <c>docs/mechanics/console.md</c>.
/// </summary>
public sealed class CCheatCommand : CCommand
{
	/// <summary>A thousand points off, in thousandths, whatever a country's neutrality is.</summary>
	private const long NoNeutralityChange = -1_000_000;

	private readonly ECheat _cheat;
	private readonly CCountryTag? _country;
	private readonly CCountryTag? _other;
	private readonly long _amount;
	private readonly EGoods _good;
	private readonly int _province;
	private readonly string _text;

	public CCheatCommand(ECheat cheat, CCountryTag? country, CCountryTag? other, long amount, EGoods good, int province, string text)
	{
		_cheat = cheat;
		_country = country;
		_other = other;
		_amount = amount;
		_good = good;
		_province = province;
		_text = text;
	}

	public override void Execute(CGameState state)
	{
		CCountry? country = state.Country(_country);
		CCountry? other = state.Country(_other);
		switch (_cheat)
		{
			case ECheat.AddGood when country is not null && state.Stockpile(country) is { } pool:
				pool[_good] += new CFixedPoint(_amount);
				break;
			case ECheat.AddEveryGood when country is not null && state.Stockpile(country) is { } pool:
				foreach (EGoods good in Enum.GetValues<EGoods>())
				{
					pool[good] += new CFixedPoint(_amount);
				}
				break;
			case ECheat.AddNukes when country is not null:
				country.Nukes = new CFixedPoint(country.Nukes.Raw + _amount);
				break;
			case ECheat.AddManpower when country is not null:
				country.Manpower = new CFixedPoint(Math.Max(0, country.Manpower.Raw + _amount));
				break;
			case ECheat.AddOfficers when country is not null:
				country.Officers = new CFixedPoint(Math.Max(0, country.Officers.Raw + _amount));
				break;
			case ECheat.NoNeutrality when country is not null:
				CNeutrality.Change(state, country, new CFixedPoint(NoNeutralityChange));
				break;
			case ECheat.SetNationalUnity when country is not null:
				country.NationalUnity = new CFixedPoint(Math.Clamp(_amount, CFixedPoint.Scale, 100L * CFixedPoint.Scale));
				break;
			case ECheat.Election when country is not null:
				CElections.Hold(state, country);
				break;
			case ECheat.Reorganise when country is not null:
				foreach (CUnit unit in country.AllUnits().Where(unit => unit.Location == _province))
				{
					foreach (CSubUnit brigade in unit.SubUnits)
					{
						brigade.Organisation = COrganisation.Ceiling(state, country, brigade, penaltyAtWar: true, penaltyAlways: true);
					}
				}
				break;
			case ECheat.Surrender when country is not null && other is not null:
				CAnnexation.Annex(state, other, country);
				break;
			case ECheat.ToggleGuarantee when country is not null && other is not null:
				ToggleGuarantee(state, country.Tag, other.Tag);
				break;
			case ECheat.ConquerAll when country is not null && other is not null:
				foreach (CProvince province in state.Provinces.Where(province => province.Owner == country.Tag && province.Controller == country.Tag))
				{
					CProvinceHands.SetController(state, province, other.Tag);
				}
				break;
			case ECheat.ChangeOwner when country is not null:
				CProvinceHands.ChangeOwner(state, state.Province(_province), country.Tag);
				break;
			case ECheat.ChangeController when country is not null:
				CProvinceHands.SetController(state, state.Province(_province), country.Tag);
				break;
			case ECheat.Revolt:
				Revolt(state);
				break;
			case ECheat.ToggleInstantResearch:
				state.InstantResearch = !state.InstantResearch;
				break;
			case ECheat.ToggleInstantConstruction:
				state.InstantConstruction = !state.InstantConstruction;
				break;
			case ECheat.ToggleAlwaysPartisans:
				state.IgnorePartisanSpawnLimit = !state.IgnorePartisanSpawnLimit;
				break;
			case ECheat.ToggleNoPopups:
				state.NoPopups = !state.NoPopups;
				break;
			case ECheat.ToggleAiPause when country is not null:
				country.AiPaused = !country.AiPaused;
				break;
			case ECheat.SetEveryAiPause:
				foreach (CCountry each in state.Countries)
				{
					each.AiPaused = _amount != 0;
				}
				break;
			case ECheat.SetDate:
				state.SetClock(_amount);
				break;
			case ECheat.Teleport when country is not null && country.AllUnits().FirstOrDefault(unit => unit.Id == _amount) is { } moved:
				CUnitOrders.Clear(moved);
				CMovement.Halt(state, moved);
				CMovement.Enter(state, moved, _province);
				break;
			case ECheat.SetPlayed when country is not null && other is not null:
				SetPlayed(state, country, other);
				break;
			case ECheat.ToggleSpy:
				state.SpyCheat = !state.SpyCheat;
				break;
			case ECheat.ToggleYesmen:
				state.AiYesmen = !state.AiYesmen;
				break;
			case ECheat.SetColour when country is not null:
				country.Color = new CColor((_amount >> 16 & 0xFF) / 255f, (_amount >> 8 & 0xFF) / 255f, (_amount & 0xFF) / 255f);
				break;
		}
	}

	/// <summary>
	/// The player takes up another country and the AI the one left. Its ministers' countdowns were left at nought while it
	/// was played; they start again at their next hour, <i>ours</i>.
	/// </summary>
	private static void SetPlayed(CGameState state, CCountry taken, CCountry left)
	{
		state.Played.Remove(left.Tag);
		state.Played.Add(taken.Tag);
		left.UnitAi.RunsUnits = true;
		taken.UnitAi.RunsUnits = false;
		left.PoliticsCountdown = Math.Max(left.PoliticsCountdown, 1);
		left.IntelligenceCountdown = Math.Max(left.IntelligenceCountdown, 1);
		left.ForeignCountdown = Math.Max(left.ForeignCountdown, 1);
		left.ProductionCountdown = Math.Max(left.ProductionCountdown, 1);
		left.TechCountdown = Math.Max(left.TechCountdown, 1);
	}

	private static void ToggleGuarantee(CGameState state, CCountryTag guarantor, CCountryTag recipient)
	{
		if (state.Guarantees.Find(pact => pact.Guarantor == guarantor && pact.Guaranteed == recipient) is { } had)
		{
			state.Guarantees.Remove(had);
			return;
		}
		state.Guarantees.Add(new CGuarantee(guarantor, recipient, state.TotalHours) { Made = state.NextPact() });
	}

	/// <summary>A rising of the type named, or of the type the province chooses.</summary>
	private void Revolt(CGameState state)
	{
		CProvince province = state.Province(_province);
		CRebelType? type = _text.Length == 0 ? CRebelChoice.TypeFor(state, province) : state.RebelTypes.FirstOrDefault(rebel => rebel.Name == _text);
		CRisings.Rise(state, province, type, CRisings.Brigades(province, CFixedPoint.One));
	}
}
