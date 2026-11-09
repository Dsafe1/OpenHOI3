namespace OpenHOI3.Interface.Console;

/// <summary>What a console line answers: its lines are white where it worked and red where it did not.</summary>
public readonly record struct CConsoleReply(bool Ok, string Text);
