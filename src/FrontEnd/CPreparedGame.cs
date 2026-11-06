using OpenHOI3.Content;
using OpenHOI3.Content.Map;
using OpenHOI3.Gui;
using OpenHOI3.Lua;
using OpenHOI3.Sim;

namespace OpenHOI3.FrontEnd;

/// <summary>
/// A game the lobby made ready - a new game set up, or a save loaded - with what was read to make it, handed to the session
/// that plays it: the state, whose played countries are who plays, the map, the localisation, the interface and the
/// install's Lua, which the session then owns. See <c>docs/mechanics/frontend.md</c>, section 3.
/// </summary>
public sealed record CPreparedGame(CGameState State, CMap Map, CLocalisation Localisation, CGuiContext Gui, CAiScripts AiScripts);
