namespace OpenHOI3.Sim.Economy;

/// <summary>The two things a country turns one good into another by each day (<see cref="CConversions"/>).</summary>
public enum EConversion
{
	/// <summary>Energy into crude oil.</summary>
	EnergyIntoCrudeOil,

	/// <summary>Crude oil into fuel.</summary>
	CrudeOilIntoFuel,
}
