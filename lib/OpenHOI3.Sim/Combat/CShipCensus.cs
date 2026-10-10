using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Combat;

/// <summary>
/// How many ships of each sort a fleet has. A ship is the first of these it answers
/// to, asked in this order: a capital ship, a transport, a submarine, a carrier - and a
/// screen where it is none of them. So a carrier that is also a capital ship is counted
/// with the capital ships. See <c>docs/mechanics/navalcombat.md</c>, section 1.
/// </summary>
public readonly record struct CShipCensus(int Capitals, int Transports, int Submarines, int Carriers, int Screens)
{
	public const string CapitalFlag = "capital";
	public const string TransportFlag = "transport";
	public const string SubmarineFlag = "is_sub";
	public const string CarrierSize = "carrier_size";

	public static bool IsCapital(CSubUnit ship) => ship.Type.Flag(CapitalFlag);

	public static bool IsTransport(CSubUnit ship) => ship.Type.Flag(TransportFlag);

	public static bool IsSubmarine(CSubUnit ship) => ship.Type.Flag(SubmarineFlag);

	public static bool IsCarrier(CSubUnit ship) => ship.Stats[CarrierSize] > CFixedPoint.Zero;

	public static CShipCensus Of(CUnit fleet)
	{
		int capitals = 0, transports = 0, submarines = 0, carriers = 0, screens = 0;
		foreach (CSubUnit ship in fleet.SubUnits)
		{
			if (IsCapital(ship))
			{
				capitals++;
			}
			else if (IsTransport(ship))
			{
				transports++;
			}
			else if (IsSubmarine(ship))
			{
				submarines++;
			}
			else if (IsCarrier(ship))
			{
				carriers++;
			}
			else
			{
				screens++;
			}
		}
		return new CShipCensus(capitals, transports, submarines, carriers, screens);
	}
}
