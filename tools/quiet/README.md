# Quiet tools

Every character a tool prints lands in the AI's context and is re-read on every later message,
so one Unity or Xcode log can cost more than the rest of a thread. These tools keep the full
output on disk and show the AI only what it needs.

| Tool | Use it for |
|---|---|
| `tools/quiet/q -n NAME -- CMD...` | Unity CLI/batchmode, `xcodebuild`, `xcrun devicectl`, Blender, TRELLIS, `pip`/`npm`/`brew`. Prints OK/FAIL, time, error lines and (on failure) the last 15 lines. Full log in `Logs/quiet/` (gitignored); grep or `tail` it if you need more. `-g REGEX` adds lines to show, `-t N` changes the tail. |
| `tools/quiet/thumb [-s 512] IMG...` | Shrink screenshots, renders and sprite sheets before reading them. Prints the small copy's path. |
| `python3 tools/quiet/measure_transcripts.py` | Run on the Mac (in Terminal) to see which tool calls used the most context in recent sessions. Prints sizes only. |

`.claude/settings.json` also caps any single Bash result at 12,000 characters and MCP results
at 10,000 tokens, so a forgotten wrapper can't dump a whole build log.

Habits that matter as much as the tools:
- `git diff --stat` before `git diff`; `git log --oneline -n 10`, never a bare `git log`.
- Read big files with an offset/limit or grep them instead of reading them whole.
- `ls` a folder, not `find`/`ls -R` on `Assets/` or `Library/`.
- Look at one contact sheet instead of many separate frames.
