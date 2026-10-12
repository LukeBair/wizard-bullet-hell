// Wizard Bullet Hell tuner: serves the editor page and reads/writes Assets/Resources/tuning.json.
// Run:  node tools/tuner/server.js   then open http://localhost:8787
// The game (editor or phone on the same Wi-Fi) fetches /tuning.json from here at the start of
// each run, so saved changes apply without a rebuild. No dependencies.
const http = require("http");
const fs = require("fs");
const path = require("path");
const os = require("os");

const PORT = 8787;
const ROOT = path.resolve(__dirname, "..", "..");
const TUNING = path.join(ROOT, "Assets", "Resources", "tuning.json");
const DEFAULTS = path.join(__dirname, "tuning.defaults.json");
const PAGE = path.join(__dirname, "index.html");

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
