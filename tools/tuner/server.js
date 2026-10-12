// Wizard Bullet Hell tuner: serves the editor page and reads/writes Assets/Resources/tuning.json.
// Run:  node tools/tuner/server.js   then open http://localhost:8787
// The game (editor or phone on the same Wi-Fi) fetches /tuning.json from here at the start of
// each run, so saved changes apply without a rebuild. No dependencies.
// /save.json mirrors the phone's progress (gold, Grimoire unlocks, feats): the phone POSTs its save
// whenever it fetches tuning, the tuner page PUTs edits with pending: true, and the phone applies a
// pending edit the next time it checks in (title screen or after a run).
// /telemetry stores gameplay logs: the phone POSTs JSON Lines for a run to /telemetry/<run> during and
// after it, and they're appended to telemetry/<run>.jsonl. telemetry/runs.json is an index with one
// summary per run (newest first). The format is in telemetry/README.md.
const http = require("http");
const fs = require("fs");
const path = require("path");
const os = require("os");

const PORT = +process.env.PORT || 8787;
// TUNER_ROOT runs this server against another checkout's files (e.g. a worktree's newer server
// serving the main checkout's tuning, save and logs).
const ROOT = path.resolve(process.env.TUNER_ROOT || path.join(__dirname, "..", ".."));
const DIR = path.join(ROOT, "tools", "tuner");
const TUNING = path.join(ROOT, "Assets", "Resources", "tuning.json");
const DEFAULTS = path.join(DIR, "tuning.defaults.json");
const PAGE = path.join(__dirname, "index.html");
const SAVE = path.join(DIR, "save.json");   // not in git: it's one phone's progress
const TELEMETRY = path.join(DIR, "telemetry");   // run logs, not in git
const RUN_ID = /^[A-Za-z0-9_-]{1,64}$/;

function readSave() {
  try { return JSON.parse(fs.readFileSync(SAVE, "utf8")); } catch { return {}; }
}

function readBody(req, done) {
  let body = "";
  req.on("data", (c) => (body += c));
  req.on("end", () => done(body));
}

// ---------------------------------------------------------------- telemetry

function readRun(id) {
  const lines = [];
  const text = fs.readFileSync(path.join(TELEMETRY, id + ".jsonl"), "utf8");
  for (const l of text.split("\n")) {
    if (!l.trim()) continue;
    try { lines.push(JSON.parse(l)); } catch { /* a half-written line */ }
  }
  return lines;
}

// One row for the index: the run_start and summary lines, or a best guess for a run that never finished
// (the app was closed, or it's still going).
const summaryCache = new Map();   // id -> { mtime, row }
function summarize(id) {
  const file = path.join(TELEMETRY, id + ".jsonl");
  const st = fs.statSync(file);
  const hit = summaryCache.get(id);
  if (hit && hit.mtime === st.mtimeMs) return hit.row;
  const lines = readRun(id);
  const start = lines.find((l) => l.ev === "run_start") || {};
  const sum = lines.find((l) => l.ev === "summary");
  const last = lines[lines.length - 1] || {};
  const zones = lines.filter((l) => l.ev === "zone");
  const row = {
    run: id,
    date: start.date || new Date(st.mtimeMs).toISOString(),
    device: start.device, version: start.version, style: start.style,
    outcome: sum ? sum.outcome : "unfinished",
    duration: sum ? sum.duration : last.t || 0,
    level: sum ? sum.level : Math.max(1, ...lines.filter((l) => l.ev === "levelup").map((l) => l.level)),
    bossesDefeated: sum ? sum.bossesDefeated : lines.filter((l) => l.ev === "boss_kill").length,
    zone: zones.length ? zones[zones.length - 1].zone : 1,
    kills: sum ? sum.kills : lines.filter((l) => l.ev === "kill" || l.ev === "boss_kill").length,
    damageTaken: sum ? sum.damageTaken.total : lines.filter((l) => l.ev === "hurt").reduce((a, l) => a + l.amount, 0),
    killer: sum ? sum.killer : null,
    spells: sum ? sum.spells.map((s) => s.name) : lines.filter((l) => l.ev === "pick" && (l.took === "tome" || l.took === "start_tome")).map((l) => l.spell),
    lines: lines.length,
    bytes: st.size,
    file: "telemetry/" + id + ".jsonl",
  };
  summaryCache.set(id, { mtime: st.mtimeMs, row });
  return row;
}

function runIndex() {
  if (!fs.existsSync(TELEMETRY)) return [];
  return fs.readdirSync(TELEMETRY).filter((f) => f.endsWith(".jsonl")).map((f) => {
    try { return summarize(f.slice(0, -6)); } catch { return null; }
  }).filter(Boolean).sort((a, b) => (a.date < b.date ? 1 : -1));
}

function writeIndex() {
  fs.writeFileSync(path.join(TELEMETRY, "runs.json"), JSON.stringify(runIndex(), null, 1) + "\n");
}

function send(res, code, type, body) {
  res.writeHead(code, { "Content-Type": type, "Access-Control-Allow-Origin": "*", "Cache-Control": "no-store" });
  res.end(body);
}

const server = http.createServer((req, res) => {
  const url = req.url.split("?")[0];
  try {
    if (req.method === "GET" && (url === "/" || url === "/index.html"))
      return send(res, 200, "text/html; charset=utf-8", fs.readFileSync(PAGE));
    if (req.method === "GET" && url === "/tuning.json")
      return send(res, 200, "application/json", fs.readFileSync(TUNING));
    if (req.method === "GET" && url === "/defaults.json")
      return send(res, 200, "application/json", fs.readFileSync(DEFAULTS));
    if (req.method === "PUT" && url === "/tuning.json") {
      let body = "";
      req.on("data", (c) => (body += c));
      req.on("end", () => {
        try {
          const parsed = JSON.parse(body);   // refuse anything that isn't valid JSON
          fs.writeFileSync(TUNING, JSON.stringify(parsed, null, 2) + "\n");
          console.log(new Date().toLocaleTimeString(), "saved tuning.json");
          send(res, 200, "application/json", '{"ok":true}');
        } catch (e) {
          send(res, 400, "application/json", JSON.stringify({ ok: false, error: e.message }));
        }
      });
      return;
    }
    if (req.method === "GET" && url === "/save.json")
      return send(res, 200, "application/json", JSON.stringify(readSave()));
    // From the tuner page: an edit for the phone to pick up.
    if (req.method === "PUT" && url === "/save.json") {
      return readBody(req, (body) => {
        try {
          const parsed = JSON.parse(body);
          parsed.pending = true;
          fs.writeFileSync(SAVE, JSON.stringify(parsed, null, 2) + "\n");
          console.log(new Date().toLocaleTimeString(), "save edit waiting for the phone");
          send(res, 200, "application/json", '{"ok":true}');
        } catch (e) {
          send(res, 400, "application/json", JSON.stringify({ ok: false, error: e.message }));
        }
      });
    }
    // From the phone: its current save. Ignored while an edit the phone hasn't applied yet is waiting.
    if (req.method === "POST" && url === "/save.json") {
      return readBody(req, (body) => {
        try {
          const parsed = JSON.parse(body);
          const current = readSave();
          if (current.pending && (parsed.revision || 0) < (current.revision || 0))
            return send(res, 200, "application/json", '{"ok":true,"kept":"pending edit"}');
          parsed.pending = false;
          parsed.receivedAt = new Date().toISOString();
          fs.writeFileSync(SAVE, JSON.stringify(parsed, null, 2) + "\n");
          send(res, 200, "application/json", '{"ok":true}');
        } catch (e) {
          send(res, 400, "application/json", JSON.stringify({ ok: false, error: e.message }));
        }
      });
    }
    if (req.method === "GET" && url === "/telemetry")
      return send(res, 200, "application/json", JSON.stringify(runIndex()));
    const tm = url.match(/^\/telemetry\/([^/]+)$/);
    if (tm && !RUN_ID.test(tm[1])) return send(res, 400, "text/plain", "bad run id");
    if (tm && req.method === "GET") {
      const file = path.join(TELEMETRY, tm[1] + ".jsonl");
      if (!fs.existsSync(file)) return send(res, 404, "text/plain", "no such run");
      return send(res, 200, "text/plain; charset=utf-8", fs.readFileSync(file));
    }
    // From the phone: more lines for a run. Only complete JSON lines are kept.
    if (tm && req.method === "POST") {
      return readBody(req, (body) => {
        const good = [];
        let bad = 0;
        for (const l of body.split("\n")) {
          if (!l.trim()) continue;
          try { JSON.parse(l); good.push(l.trim()); } catch { bad++; }
        }
        fs.mkdirSync(TELEMETRY, { recursive: true });
        if (good.length) fs.appendFileSync(path.join(TELEMETRY, tm[1] + ".jsonl"), good.join("\n") + "\n");
        if (body.includes('"ev":"summary"')) console.log(new Date().toLocaleTimeString(), "run logged:", tm[1]);
        try { writeIndex(); } catch (e) { console.log("telemetry index:", e.message); }
        send(res, 200, "application/json", JSON.stringify({ ok: true, lines: good.length, bad }));
      });
    }
    if (tm && req.method === "DELETE") {
      const file = path.join(TELEMETRY, tm[1] + ".jsonl");
      if (fs.existsSync(file)) fs.unlinkSync(file);
      summaryCache.delete(tm[1]);
      writeIndex();
      return send(res, 200, "application/json", '{"ok":true}');
    }
    if (req.method === "GET" && url === "/info") {
      const ips = Object.values(os.networkInterfaces()).flat().filter((a) => a && a.family === "IPv4" && !a.internal && !a.address.startsWith("169.254.")).map((a) => a.address);
      return send(res, 200, "application/json", JSON.stringify({ ips, port: PORT, file: TUNING }));
    }
    send(res, 404, "text/plain", "not found");
  } catch (e) {
    send(res, 500, "text/plain", e.message);
  }
});

server.listen(PORT, "0.0.0.0", () => {
  console.log(`Tuner running: http://localhost:${PORT}`);
  console.log(`Editing ${TUNING}`);
});
