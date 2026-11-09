"""Batch-generate images on grok.com/imagine by driving the website (no API key).
Setup once:   python3 -m venv .venv && .venv/bin/pip install playwright && .venv/bin/playwright install chromium
Run:          .venv/bin/python grok_batch.py [name ...] [--out DIR] [--prompts FILE] [--no-style]
First run opens a browser with its own saved profile (~/.cache/grok-batch-profile): sign in to Grok there once,
then press Enter in the terminal. Later runs reuse the login. For each prompt it opens /imagine, picks Image,
types "<style> <prompt>", submits, waits for the new result images, and saves them as OUT/<name>_<k>.png.
Names already saved are skipped (delete the files to redo). Props prompts: grok_prompts.json ("style" is
prepended to each; --no-style for ground/texture prompts). Selectors are at the top; if Grok's page changes,
that is the only place to fix. The result-image detection is heuristic: it takes new <img> elements at
least MIN_PX wide that appear after submitting and stop changing for SETTLE seconds."""
import argparse, base64, json, os, sys, time
from playwright.sync_api import sync_playwright

PROMPT_BOX = "div.ProseMirror[contenteditable=true]"
IMAGE_TAB = "button:has-text('Image')"
SUBMIT = "button[aria-label='Submit']"
MIN_PX, SETTLE, TIMEOUT = 400, 6, 240
PROFILE = os.path.expanduser("~/.cache/grok-batch-profile")

JS_SIZES = "() => [...document.images].map(i => [i.currentSrc || i.src, i.naturalWidth, i.naturalHeight])"
JS_FETCH = """async (src) => { const r = await fetch(src, {credentials: 'include'}); const b = new Uint8Array(await r.arrayBuffer());
  let s = ''; for (let i = 0; i < b.length; i += 32768) s += String.fromCharCode(...b.subarray(i, i + 32768)); return btoa(s); }"""


def wait_results(page, before):
    start, last_change, seen = time.time(), time.time(), None
    while time.time() - start < TIMEOUT:
        cur = sorted(s for s, w, h in page.evaluate(JS_SIZES) if s not in before and w >= MIN_PX)
        if cur != seen: seen, last_change = cur, time.time()
        if seen and time.time() - last_change >= SETTLE: return seen
        time.sleep(1)
    return seen or []


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("names", nargs="*"); ap.add_argument("--out", default=os.path.join(os.path.dirname(__file__), "grok_out"))
    ap.add_argument("--prompts", default=os.path.join(os.path.dirname(__file__), "grok_prompts.json")); ap.add_argument("--no-style", action="store_true")
    a = ap.parse_args()
    data = json.load(open(a.prompts)); style = "" if a.no_style else data.get("style", "")
    names = a.names or list(data["prompts"]); os.makedirs(a.out, exist_ok=True)
    with sync_playwright() as p:
        ctx = p.chromium.launch_persistent_context(PROFILE, headless=False, viewport={"width": 1280, "height": 900})
        page = ctx.pages[0] if ctx.pages else ctx.new_page()
        page.goto("https://grok.com/imagine")
        input("Sign in to Grok in the opened window if needed, then press Enter here... ")
        for name in names:
            if any(f.startswith(name + "_") for f in os.listdir(a.out)): print("skip", name); continue
            print("generating", name, flush=True)
            page.goto("https://grok.com/imagine"); page.wait_for_selector(PROMPT_BOX, timeout=30000)
            try: page.locator(IMAGE_TAB).first.click(timeout=5000)
            except Exception: print("  (could not click Image tab; check the mode by hand)")
            before = {s for s, w, h in page.evaluate(JS_SIZES)}
            page.click(PROMPT_BOX); page.keyboard.type((style + " " + data["prompts"][name]).strip(), delay=2)
            page.click(SUBMIT)
            urls = wait_results(page, before)
            if not urls: print("  no images found (timeout or selectors changed)"); continue
            for k, u in enumerate(urls):
                raw = base64.b64decode(page.evaluate(JS_FETCH, u))
                open(os.path.join(a.out, f"{name}_{k}.png"), "wb").write(raw)   # may be jpg bytes; Pillow/Blender read either
            print(f"  saved {len(urls)} to {a.out}")
        ctx.close()


if __name__ == "__main__":
    main()
