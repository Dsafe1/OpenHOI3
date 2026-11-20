# Repository layout

Every folder and what it is for. A folder is a namespace, and a new type goes in the folder of its subject.

```
scenes/Main.tscn        main scene (run/main_scene)
fonts/                  the open typefaces all text is drawn in, with their licences
src/                    the Godot client
src/Map/                the map view, and the unit counters on it
src/Gfx/                decoding the original's textures and fonts
src/Debug/              ours: the debug menu of developer switches, on Shift and 4, and --click's player
src/Multiplayer/        this machine's part in a game between machines: a host's or a client's connections,
                        kept across the sessions of it
src/Loading/            the loading screen, and the steps of the front end's and a game's making it runs
src/Audio/              the music and the sound effects, one player of each for the whole run
src/FrontEnd/           the main menu and the lobby, with the world they offer, and the game Play
                        hands to a session
src/Gui/                the interface runtime: windows from the original's .gui files
src/Screens/            one class for each screen: the top bar, production, technology,
                        the builder windows, the deploy menu, the alerts
lib/OpenHOI3.Sim/       our own data model and the simulation: the game state, the tick,
                        the clock, the two kinds of number, the generator of chance and
                        the original runtime's sort are at its root
    Commands/           every change to the game state
    Provinces/          a province, its terrain, a building, and the day's repair of what
                        was bombed
    Countries/          a country, its tag, its laws, its ministers, its alliances and
                        guarantees
    Wars/               a war and its sides, casus belli types and those held, war goals,
                        a declaration and who joins it, the call to arms, the truce,
                        peace, the surrender and the capitulation, annexation, puppets
                        and the government in exile, factions - joining, inviting, leaving,
                        their rules - their progress and the victory conditions
    Diplomacy/          what two countries hold of each other: relation, threat and
                        neutrality, a new game's, the month's drift and each change
    Agreements/         an offer and the AI's answer to it, the pacts - NAP, embargo,
                        influence, alignment - what each does and how each ends
    Plans/              a battle plan as data, which nothing simulated reads
    StrategicWarfare/   the played country's records of each war, their rings and what writes them
    Politics/           the parties and their drift, the cabinet, elections, national unity
                        and strategic warfare, the pool of ministers and the filling of
                        posts, a law changed, and the AI's politics
    Espionage/          a country's spies in each country, their placing by priority, the
                        nine missions, catching spies, the coup and covert operations
    Occupation/         the occupation policies, the one each country holds towards each,
                        and what sets it
    Revolts/            suppression, revolt risk, the rebels and the undergrounds
    Intelligence/       what a country knows: of another country, by its spies; of every
                        province, made again daily, raised hourly, and what radar sees
    Aid/                debt and its repayment, shared technology, lend-lease - who may, its
                        convoys, its IC given and received - and a licence's terms
    Trade/              a trade route, its terms and price, its convoy, its day's exchange
                        and how it ends, the AI's trading each hour, and the player's
                        automated trades
    Ai/                 what the simulation asks of the install's Lua, allied objectives,
                        the foreign minister's decisions and prepared wars, and the AI's
                        strategy - its personality and weighted lists, and readiness for war
    Theatres/           the AI's fronts and theatres - an area's fronts and a country's, the
                        front-worthy enemies, making, merging, refreshing and looking after
                        theatres, and their headquarters
    Agents/             the AI's unit agents - the sort into theatres, the ladder of commands,
                        fleets and air groups regrouped, headquarters placed, a theatre's
                        plan, the movers between theatres - and moving and attacking: the
                        garrisons, the line and the reserve, the attack and its odds, the
                        sea targets and the fleets' hour, the sealift and invasions, and the
                        air - air bases chosen and missions given
    Replay/             ours: the AI's steps recorded and played back in their place, the
                        commands' written form and the state's hash (docs/replay.md)
    Multiplayer/        ours: a host's and a client's side of a game between several machines,
                        and the lines of text they send each other (docs/multiplayer.md)
    Modifiers/          what a modifier is, and the rebuilding of a province's and a country's
    Economy/            goods, resources, IC, the stockpile, the six shares, manpower, leadership
    Research/           technologies, their categories, the queue and the decay
    Units/              units and brigades, their types, what a day does to one, and its
                        organisation hour by hour
    Leaders/            leaders and their traits, who is in service, which unit each
                        may command, and the choice of one for a unit
    Movement/           where a unit may go, the way there, its speed and its hourly march
    Combat/             a battle and its two sides, the front, the round, the damage,
                        falling back, the attack delay, and a province changing hands;
                        and the battle at sea, in the classes named CNaval - the
                        sighting, each ship's distance and target, its shots, sinking,
                        breaking away and falling back - with the raid on a convoy;
                        the battle in the air, in the classes named CAir; what a
                        country knows of a province; and the bombing - who is in one,
                        the bombers' fire, what the bombs do to the province, the fire
                        that comes back, and what radar gives a side
    Missions/           a fleet's missions: reserve, patrol, intercept, sortie, the raid
                        on convoys and their escort, the frame they share, the look for
                        an enemy fleet, and how good a country is at each; and an air
                        unit's, in the classes named CAir: air superiority, the
                        intercept, their frame, their targets and a mission's area;
                        and the eight bombing missions, in the classes named CBombing:
                        what tells them apart, their hour, and which province they fly at;
                        and the transport and the invasion
    Transport/          land units carried by a fleet - its room, boarding, getting off,
                        too much aboard and a transport's damage - the landing from the
                        sea with its two scales, and the warning a landing raises
    Weather/            the weather over a province, what it does to a march and to a
                        battle, and whether it is night there
    Supply/             the supply network - areas, groups, depots and distances, and what
                        a province passes and takes - the day's pass along it, what a unit
                        draws and brings, where supplies are made, and a new game's filling
    Convoys/            a convoy, the way it sails and the heap that is found with, what it
                        wants and lands, the making of supply and resource convoys, and
                        the sharing out of transports and escorts
    Construction/       the build queue: a line, its brigades, the rules, and placing what
                        comes out
    Script/             events and decisions as read: an event, an option, a decision, a
                        mean time to happen, a trigger and an effect, and a value; and
                        what a condition answers - the scope, its forms, the sets, the
                        leaves, and what is reported unbuilt; and what an effect does -
                        the blocks, the draws, flags, variables, the modifiers an event
                        gives, and the effects on countries and provinces
    Events/             when events are asked and fire: each country's candidates, the
                        day's roll, the chance of a day, a firing, the AI's option, an open
                        event; and which decisions are offered
    Settings/           the game's difficulty and mode and what it does, and what the player
                        hands to the AI - the record, who does a country's work, its ministers
lib/OpenHOI3.Content/   reading an install: finding and layering it, localisation, the loader
    BattlePlans/        a battle plan's file both ways, bp_settings.txt and the arrow and icon types
    Checksum/           the original's file checksum over the install, which the version label's letters come from
    Clausewitz/         the script format
    Map/                the province bitmap, shapes, the edges between provinces with their
                        kinds and distances, terrain, a province's points, and the
                        declarations of the unit counters, their pointer and the arrows
      Cache/            the map cache made from the map files, in the original's formats
    Definitions/        what common/, technologies/ and units/ declare
    History/            what history/ gives on the start date, orders of battle among it
    Gui/                the interface files' declarations, and font descriptors
    Script/             events and decisions: their words, the keyword tables, the names
                        a key may be, the readers of triggers, effects, events and
                        decisions and the on_actions, and the problems found
    Launch/             the launcher's side of an install: its mods listed and ticked, the engine a choice is
                        played on, engine.txt, the command line to an engine, where an install is looked for
                        and what the launcher remembers (docs/launcher.md)
    Logs/               where the game's own log goes, in the player's folder's logs, and the runs kept
    Messages/           the message types of interface/messagetypes.txt
    Mods/               a .mod file, the mods a command line selects and their order, the layering's rules
    Saves/              the original's .hoi3 saves: the loader, a save's values, its ids, units, orders, leaders and lines, wars, pacts and convoys, battles, what a load left unread, the writer and its layout
    Settings/           the player's settings.txt, from the original's folder under Documents
    Frontend/           the bookmarks of common/bookmarks.txt, the lobby's Historical Start
    Music/              the songs of music/*.txt
    Sound/              the sound effects of interface/sound.sfx
    Shaders/            the original's effect files: the preprocessor, the parser, the syntax tree and the writer
                        that translates each pass of a technique into a Godot shader
    Textures/           a picture's file told by its content; DDS and TGA headers, and the decoders for what
                        Godot's do not read - 8- and 16-bit and palette DDS, odd-sized DXT, colour-mapped TGA
lib/OpenHOI3.Interface/ what the screens show, with no engine: sliders and placement at its root
    BattlePlans/        the plans kept, the tools and the mouse, an arrow's mesh, what the window says
    Text/               colour escapes, number formats, templates, breaking and placing lines
    Screens/            the text of each screen's figures
    Alerts/             the sixteen alerts: which are up and what each says
    Chat/               the chat in play: its channels, lines, unread marks and what a typed line comes to
    Ledger/             the Statistics screen's pages, tables and pies
    Tooltips/           a tooltip, what the box shows of it, where it goes and when its
                        second half comes; the map's, the top bar's, the production and
                        technology screens', the sliders', the builder windows', the
                        unit panel's, a leader's, the deploy menu's, the orders
                        windows' and a battle's
    Modifiers/          how each kind of modifier is written, and a modifier's sources
                        written out
    Map/                the unit counters: which units have one, how they stack, what one
                        says, where its parts go, the painting of one, and the shape of
                        the arrow along a selected unit's way
    Events/             an event's and a decision's text with its $VARIABLES$, and which
                        window an event is shown in and where its options stand
    Script/             what a block of conditions and a block of effects say of
                        themselves: each leaf's line, the blocks, the asterisks
    Messages/           a message, its words, and the log it is kept in
    Music/              which song plays next, and how loud
    Sound/              how loud a sound effect plays, and which a button, a unit and a battle play
    Loading/            the loading screen's pictures and where its parts stand
lib/OpenHOI3.Lua/       the install's Lua, run on the simulation: the state the original
                        makes, its searcher and autoexec, and what the game asks it
    Native/             the interpreter's C API, the trampoline, a state, a call, a class
    Api/                the classes the original hands its scripts, on the simulation
native/luajit/          LuaJIT's source, its licence and the build of openhoi3_lua.dll, the interpreter
                        the install's Lua runs on, which the Lua project runs
native/lua/             Lua 5.1.4's source, its licence, our trampoline and the build of
                        openhoi3_lua514.dll, the original's interpreter, behind a switch
tools/OpenHOI3.SaveCheck/   checks the libraries against a savegame the original wrote
tools/build_*.ps1       the builds: build_engine the standalone engine, build_launcher the launcher, build_both
                        the launcher with the engine in its engine folder (docs/launcher.md, section 7)
launcher/OpenHOI3.Launcher/ the launcher, in Avalonia: the window that lists the mods and starts an engine
docs/mechanics/         one spec per system: what the original computes, and how sure that is
docs/compatibility.md   where a valid file of the original's is treated differently on purpose
docs/launcher.md        the launcher: choosing the mods and the engine each one runs on
tests/OpenHOI3.Tests/   Sim/, Content/, Interface/ and Lua/: the tests of each library
```

Nothing under `lib/` references Godot, and `OpenHOI3.Sim` references nothing at all. The
tests run against those four as a plain .NET project, with no engine behind them - the Lua
project's native interpreter copied beside them.

A folder is a namespace: `lib/OpenHOI3.Sim/Units/CUnit.cs` is `OpenHOI3.Sim.Units.CUnit`.
