<#
  Says the version of the engine a build of this tree is: what engine.txt holds, a mod's descriptor names and the play
  checksum hashes (docs/launcher.md, section 2). One program plays the faithful game and the enhanced one, so the
  version says nothing of the mode.

  - On a release - the tree clean and its commit carrying a tag v<x> - it is  openhoi3-<x> : tag v0.1.0 gives
    openhoi3-0.1.0. That is the version a mod is made for and names.
  - Anywhere else it is  openhoi3-<yyyy.MM.dd>-<commit> , with a + where the tree had changes not committed: a build
    between two releases, for its builder.

  Usage: tools/engine_version.ps1 [-RequireRelease]
    -RequireRelease  fails where the tree is not a release, saying why.
  Writes the version to the pipeline.
#>
param(
    [switch] $RequireRelease
)
$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)

$commit = (& git -C $root rev-parse --short HEAD 2>$null)
if (-not $commit) { $commit = 'unknown' }
$dirty = [bool] (& git -C $root status --porcelain 2>$null)
# The tags on this very commit; where several, the highest by its numbers.
$tags = @(& git -C $root tag --points-at HEAD --list 'v*' --sort=-v:refname 2>$null)

if ($tags.Count -gt 0 -and -not $dirty) {
    return 'openhoi3-' + $tags[0].Substring(1)
}
if ($RequireRelease) {
    if ($dirty) {
        throw 'This tree has changes not committed, so it is no release: commit or put them away first.'
    }
    throw "Commit $commit carries no tag v<x>, so it is no release: tag it first (git tag v0.1.0)."
}
$plus = if ($dirty) { '+' } else { '' }
return "openhoi3-$(Get-Date -Format 'yyyy.MM.dd')-$commit$plus"
