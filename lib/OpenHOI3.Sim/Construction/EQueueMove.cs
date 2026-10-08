namespace OpenHOI3.Sim.Construction;

/// <summary>Where a line of the build queue is moved to. The first line is paid for first.</summary>
public enum EQueueMove
{
	/// <summary>To the front of the queue.</summary>
	First,

	/// <summary>One place towards the front.</summary>
	Earlier,

	/// <summary>One place towards the back.</summary>
	Later,

	/// <summary>To the back of the queue.</summary>
	Last,
}
