namespace OpenHOI3.Content.Script;

/// <summary>One word of a script file and the line it is on.</summary>
public readonly record struct CScriptToken(EScriptToken Kind, string Text, int Line);
