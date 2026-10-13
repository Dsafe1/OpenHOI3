using OpenHOI3.Lua.Native;
using OpenHOI3.Sim;

namespace OpenHOI3.Lua.Api;

/// <summary>
/// <c>CFixedPoint</c>, a figure in thousandths: <c>Get()</c> a float, the arithmetic between two of
/// them and with a number - a number on the left answering a number, and <c>-</c> and <c>/</c> then
/// worked the other way round, as the original does. See <c>docs/mechanics/lua.md</c>, section 5.
/// </summary>
public static class CFixedPointClass
{
	private const string Name = "CFixedPoint";

	/// <summary>What a division by nought gives, in thousandths: the original's, with no error.</summary>
	private const int ByNought = -1;

	public static CLuaClass Define()
	{
		CLuaClass luaClass = new CLuaClass(Name)
			.Method("Get", call => call.Return(CLuaFloats.Thousandths(Raw(call, 1))))
			.Method("GetTruncated", call => call.Return(Raw(call, 1) / 1000));
		luaClass
			.Operator("__add", call => Arithmetic(call, luaClass, (a, b) => a + b, (fp, n) => fp + n, (n, fp) => n + fp))
			.Operator("__sub", call => Arithmetic(call, luaClass, (a, b) => a - b, (fp, n) => fp - n, (n, fp) => fp - n))
			.Operator("__mul", call => Arithmetic(call, luaClass, Multiply, Multiply, (n, fp) => Multiply(fp, n)))
			.Operator("__div", call => Arithmetic(call, luaClass, Divide, Divide, (n, fp) => Divide(fp, n)))
			.Operator("__eq", call => call.Return(Raw(call, 1) == Raw(call, 2)))
			.Operator("__lt", call => call.Return(Raw(call, 1) < Raw(call, 2)))
			.Operator("__le", call => call.Return(Raw(call, 1) <= Raw(call, 2)));
		// CFixedPoint(x): the number made a float, then to the nearest thousandth by single precision.
		return luaClass.Constructs(call =>
			call.Return(new CLuaObject(luaClass, new CFixedPoint(CLuaFloats.ToThousandths((float)call.Number(2))))));
	}

	private static int Multiply(int a, int b) => (int)((long)a * b / 1000);

	private static int Divide(int a, int b) => b == 0 ? ByNought : (int)((long)a * 1000 / b);

	/// <summary>
	/// One arithmetic operator: two objects, the object and a number, or a number and the object. The
	/// last answers a number, the result's thousandths made a float and divided by 1000.
	/// </summary>
	private static int Arithmetic(CLuaCall call, CLuaClass luaClass, System.Func<int, int, int> both,
		System.Func<int, int, int> objectFirst, System.Func<int, int, int> numberFirst)
	{
		bool leftIsObject = call.ObjectAt(1, out _) is CFixedPoint;
		bool rightIsObject = call.ObjectAt(2, out _) is CFixedPoint;
		if (leftIsObject && rightIsObject)
		{
			return call.Return(new CLuaObject(luaClass, new CFixedPoint(both(Raw(call, 1), Raw(call, 2)))));
		}
		if (leftIsObject)
		{
			int number = CLuaFloats.ToThousandths(call.Number(2));
			return call.Return(new CLuaObject(luaClass, new CFixedPoint(objectFirst(Raw(call, 1), number))));
		}
		int left = CLuaFloats.ToThousandths(call.Number(1));
		return call.Return(CLuaFloats.SingleThousandths(numberFirst(left, Raw(call, 2))));
	}

	/// <summary>The object's thousandths, as the original's 32-bit figure.</summary>
	private static int Raw(CLuaCall call, int argument) => (int)call.Object<CFixedPoint>(argument, Name).Raw;
}
