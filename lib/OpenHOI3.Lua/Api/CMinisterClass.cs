using System;
using System.Collections.Generic;
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

	/// <summary>The null minister type's name.</summary>
	public const string NullTypeName = "noMinisterType";

	/// <summary>
	/// The null minister type, <c>noMinisterType</c>, which is empty: a minister's personality in a post he has no posting
	/// for, and the first of the minister type database (reversing/findings/FINDINGS-countrymodifier.md).
	/// </summary>
	public static readonly CMinisterType NullType = new(new CModifier(NullTypeName));

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

	/// <summary>
	/// <c>CMinisterTypeDataBase</c>: the types in their database's order - the null type, then
	/// <c>common/minister_types.txt</c>'s - their number, and a type by its exact name, case and all, nil for none.
	/// </summary>
	public static CLuaClass DefineTypeDataBase(CGameApi api)
	{
		return new CLuaClass("CMinisterTypeDataBase")
			.Static("GetMinisterTypeList", call =>
			{
				var types = new List<object?> { new CLuaObject(api.MinisterType, NullType) };
				foreach (CMinisterType type in api.State.MinisterTypes)
				{
					types.Add(new CLuaObject(api.MinisterType, type));
				}
				return call.ReturnIterator(types);
			})
			.Static("GetMinisterType", call =>
			{
				string name = call.String(1);
				if (name == NullTypeName)
				{
					return call.Return(new CLuaObject(api.MinisterType, NullType));
				}
				foreach (CMinisterType type in api.State.MinisterTypes)
				{
					if (string.Equals(type.Name, name, StringComparison.Ordinal))
					{
						return call.Return(new CLuaObject(api.MinisterType, type));
					}
				}
				return call.Return(null);
			})
			.Static("GetNumberOfMinisterTypes", call => call.Return(api.State.MinisterTypes.Count + 1));
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
