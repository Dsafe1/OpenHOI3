# Settings: the player's `settings.txt`

The player's settings are the original's own: `settings.txt` in `Documents\Paradox Interactive\Hearts of Iron III`, the
file the original writes as it is played - **not** the one shipped beside its executable. Read once as a session starts
(`Content/Settings`, `CUserSettingsReader`), and never written: nothing here changes a setting yet. By the maintainer's
word on 2026-10-15 (`docs/compatibility.md`, entry 13). With no file, the original's defaults stand. The same folder
holds the player's message settings, `messagetypes_custom.txt`, which the in-game menu does write, as the original's
does (`ingamemenu.md`, section 5).

The file is Clausewitz script. What is read of it:

| key | default | what it does here |
| --- | --- | --- |
| `gui = { language }` | `l_english` | read; nothing uses it yet |
| `mapRenderingOptions = { alwaysCounters }` | yes | **only `yes` is supported** (`docs/compatibility.md`, entry 12): the map always shows counters and the weather map mode its `_simple` icons (`weather.md`, section 6); a `no` is reported and read as `yes` |
| `mapRenderingOptions = { counter_scale }` | 1 | read; **what it does is not read** - the original's settings keep it beside a figure of the counters' sizes (`reversing/findings/FINDINGS-counters.md`, section 3) |
| `mapRenderingOptions = { counter_distance }` | 100 | read; the original draws its units' figures under it (`reversing/findings/FINDINGS-camera.md`), which `alwaysCounters` makes nothing here |

The defaults are the original's settings' constructor's, where they were read (`counter_scale`, `counter_distance`).

## Not established

- **What `counter_scale` does.** The maintainer plays at 2, and the counters here were laid out from his screenshot,
  so whether they already carry it is not known.
