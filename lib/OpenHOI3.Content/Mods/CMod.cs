using System.Collections.Generic;

namespace OpenHOI3.Content.Mods;

/// <summary>
/// A mod as its <c>.mod</c> file describes it: the folder laid over the install, the player's folder of its own under the
/// Documents folder, the folders whose listings it replaces and the mods it depends on (<c>docs/mechanics/mods.md</c>,
/// section 2).
/// </summary>
/// <param name="Name">The <c>name</c> line; never empty.</param>
/// <param name="File">The <c>.mod</c> file, absolute.</param>
/// <param name="VirtualPath">The descriptor's path as the original's file system names it, <c>mod/&lt;file&gt;.mod</c>: what <c>-mod=</c> is matched against.</param>
/// <param name="Folder">The <c>path</c> line made absolute against the install, or null where the file gives none.</param>
/// <param name="PathAsWritten">The <c>path</c> line as written, or null.</param>
/// <param name="Archive">The <c>archive</c> line, or null: an archive is not read here.</param>
/// <param name="UserDir">The <c>user_dir</c> line as written, or null where the file gives none.</param>
/// <param name="ReplacePaths">The <c>replace_path</c> lines, '\' turned to '/' and nothing else changed.</param>
/// <param name="Dependencies">The names in its <c>dependencies</c> block.</param>
public sealed record CMod(string Name, string File, string VirtualPath, string? Folder, string? PathAsWritten, string? Archive,
	string? UserDir, IReadOnlyList<string> ReplacePaths, IReadOnlyList<string> Dependencies);
