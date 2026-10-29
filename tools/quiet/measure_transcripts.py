#!/usr/bin/env python3
"""Measure which tool calls fill Claude Code's context, from local session transcripts.

    python3 tools/quiet/measure_transcripts.py [--days 7] [--top 20] [--out report.txt]

Reads ~/.claude/projects/*/*.jsonl (sessions run on this Mac, Remote Control threads included)
and prints sizes only, never contents: totals per session, per tool/command category, and the
largest single results. Tokens are estimated as chars / 4; images as ~1,600 tokens each.
"""
import argparse, glob, json, os, re, sys, time
from collections import defaultdict

IMAGE_TOKENS = 1600


def category(name, inp):
    if name != "Bash":
        if name == "Read":
            p = str(inp.get("file_path", ""))
            ext = os.path.splitext(p)[1].lower()
            return "Read image" if ext in (".png", ".jpg", ".jpeg", ".gif", ".webp") else f"Read {ext or 'file'}"
        if name.startswith("mcp__"):
            return "MCP " + name.split("__")[1]
        return name
    cmd = str(inp.get("command", ""))
    rules = [
        (r"Unity(\.app)?|-batchmode|unity-cli|com\.unity|executeMethod", "Unity"),
        (r"xcodebuild|xcrun|devicectl|ios-deploy", "Xcode/device"),
        (r"[Bb]lender", "Blender"),
        (r"trellis|TRELLIS", "TRELLIS"),
        (r"git (diff|show)", "git diff/show"),
        (r"git log", "git log"),
        (r"\bgit\b", "git other"),
        (r"pip |npm |brew ", "package install"),
        (r"\b(find|ls|tree|du)\b", "listing"),
        (r"\b(cat|head|tail|sed -n|less)\b", "print file"),
        (r"\b(grep|rg)\b", "search"),
        (r"curl|wget", "curl"),
        (r"python|node", "script"),
    ]
    for pat, cat in rules:
        if re.search(pat, cmd):
            return "Bash: " + cat
    return "Bash: other"


def result_size(block):
    c = block.get("content")
    chars, images = 0, 0
    if isinstance(c, str):
        chars = len(c)
    elif isinstance(c, list):
        for part in c:
            if part.get("type") == "image":
                images += 1
            else:
                chars += len(part.get("text", "") or "")
    return chars, images


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--days", type=float, default=7)
    ap.add_argument("--top", type=int, default=20)
    ap.add_argument("--root", default=os.path.expanduser("~/.claude/projects"))
    ap.add_argument("--out")
    a = ap.parse_args()
    out = open(a.out, "w") if a.out else sys.stdout

    cutoff = time.time() - a.days * 86400
    files = [f for f in glob.glob(os.path.join(a.root, "*", "*.jsonl")) if os.path.getmtime(f) >= cutoff]
    cats = defaultdict(lambda: [0, 0, 0])  # calls, tokens, images
    sessions, biggest = [], []
    for f in files:
        uses, total, compactions, first_user = {}, 0, 0, ""
        for line in open(f, errors="replace"):
            try:
                e = json.loads(line)
            except ValueError:
                continue
            if e.get("type") == "system" and "compact" in str(e.get("subtype", "")):
                compactions += 1
            msg = e.get("message") or {}
            content = msg.get("content")
            if not isinstance(content, list):
                if e.get("type") == "user" and isinstance(content, str) and not first_user:
                    first_user = content[:60].replace("\n", " ")
                continue
            for b in content:
                t = b.get("type")
                if t == "tool_use":
                    uses[b.get("id")] = (b.get("name", "?"), b.get("input") or {})
                elif t == "tool_result":
                    name, inp = uses.get(b.get("tool_use_id"), ("?", {}))
                    chars, imgs = result_size(b)
                    tok = chars // 4 + imgs * IMAGE_TOKENS
                    cat = category(name, inp)
                    cats[cat][0] += 1; cats[cat][1] += tok; cats[cat][2] += imgs
                    total += tok
                    summary = str(inp.get("command") or inp.get("file_path") or inp.get("pattern") or "")[:90].replace("\n", " ")
                    biggest.append((tok, cat, summary, os.path.basename(f)[:8]))
        sessions.append((total, compactions, os.path.basename(f)[:8], first_user))

    p = lambda *x: print(*x, file=out)
    grand = sum(s[0] for s in sessions) or 1
    p(f"{len(files)} sessions in the last {a.days:g} days; tool results ~{grand:,} tokens in total\n")
    p("By category (calls, ~tokens, share):")
    for cat, (n, tok, imgs) in sorted(cats.items(), key=lambda kv: -kv[1][1]):
        extra = f", {imgs} images" if imgs else ""
        p(f"  {cat:24} {n:5} calls  ~{tok:>9,} tok  {100*tok/grand:4.1f}%{extra}")
    p(f"\nLargest {a.top} single results:")
    for tok, cat, summary, sid in sorted(biggest, reverse=True)[: a.top]:
        p(f"  ~{tok:>7,} tok  {cat:20} [{sid}] {summary}")
    p("\nBiggest sessions (tool-result tokens, compactions):")
    for total, comp, sid, first in sorted(sessions, reverse=True)[:10]:
        p(f"  ~{total:>9,} tok  {comp} compactions  [{sid}] {first}")


if __name__ == "__main__":
    main()
