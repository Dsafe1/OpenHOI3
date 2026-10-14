using OpenHOI3.Lua.Native;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Modifiers;
using OpenHOI3.Sim.Politics;

namespace OpenHOI3.Lua.Api;

/// <summary>
/// <c>CMinister</c>, <c>CMinisterType</c>, <c>CGovernmentPosition</c> and <c>CGovernmentPositionDataBase</c>: a minister,
/// equal to another by his number, his ideology, the posts he may take and his personality in each; the posts numbered
/// from 1 in their file's order, 0 the null post. An empty post answers the null minister, never nil. See
/// <c>docs/mechanics/politics.md</c>, section 12.
/// </summary>
public static class CMinisterClass
{
	private const string MinisterName = "CMinister";
	private const string TypeName = "CMinisterType";
	private const string PositionName = "CGovernmentPosition";

	/// <summary>The null minister, number 0, whom an empty post holds.</summary>
	public static readonly CMinister NullMinister = new(0, "");

	/// <summary>The null minister type, a minister's personality in a post he has no posting for.</summary>
	public static readonly CMinisterType NullType = new(new CModifier(""));

	/// <summary>The null post, number 0.</summary>
	public static readonly CGovernmentPosition NullPosition = new("", 0, false);

	public static CLuaClass DefineMinister(CGameApi api)
	{
		return new CLuaClass(MinisterName)
			.Operator("__eq", CLuaEquality.By<CMinister>(MinisterName, (a, b) => a.Id == b.Id))
			.Method("GetKey", call => call.Return(api.String(Minister(call).Name)))
			.Method("GetIdeology", call => call.Return(api.IdeologyNamed(Minister(call).Ideology)))
			.Method("CanTakePosition", call => call.Return(Minister(call).MayTake(Position(call, 2).Name)))
			.Method("GetPersonality", call =>
				call.Return(new CLuaObject(api.MinisterType, Minister(call).TypeIn(Position(call, 2).Name) ?? NullType)));
	}

	public static CLuaClass DefineType(CGameApi api)
	{
		return new CLuaClass(TypeName)
			.Method("GetKey", call => call.Return(api.String(call.Object<CMinisterType>(1, TypeName).Name)));
	}

	public static CLuaClass DefinePosition(CGameApi api)
	{
		return new CLuaClass(PositionName)
			.Operator("__eq", CLuaEquality.Same<CGovernmentPosition>(PositionName))
			.Method("GetIndex", call => call.Return(Position(call, 1).Index))
			.Method("GetKey", call => call.Return(api.String(Position(call, 1).Name)));
	}

	public static CLuaClass DefinePositionDataBase(CGameApi api)
	{
		// Unchecked in the original; past the end, nil here.
		return new CLuaClass("CGovernmentPositionDataBase")
			.Static("GetGovernmentPositionByIndex", call =>
			{
				int index = call.Integer(1);
				if (index == 0)
				{
					return call.Return(new CLuaObject(api.GovernmentPosition, NullPosition));
				}
				var positions = api.State.GovernmentPositions;
				return call.Return(index > 0 && index <= positions.Count ? new CLuaObject(api.GovernmentPosition, positions[index - 1]) : null);
			});
	}

	/// <summary>Who holds a post: the null minister where nobody does.</summary>
	public static CMinister HolderOf(CCountry country, CGovernmentPosition position)
	{
		return country.Government.TryGetValue(position.Name, out CMinister? minister) ? minister : NullMinister;
	}

	private static CMinister Minister(CLuaCall call) => call.Object<CMinister>(1, MinisterName);

	public static CGovernmentPosition Position(CLuaCall call, int argument) => call.Object<CGovernmentPosition>(argument, PositionName);
}
