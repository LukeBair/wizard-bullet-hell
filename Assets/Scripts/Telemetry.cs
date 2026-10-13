using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

// Gameplay telemetry: every run is logged as JSON Lines (one event object per line) and sent to the
// tuner server on the Mac (tools/tuner), which stores it as tools/tuner/telemetry/<run>.jsonl and
// shows it on the Runs page. The format is described in tools/tuner/telemetry/README.md.
//
// Every line has "t" (seconds of play time, pauses excluded) and "ev" (the event type). The first line
// is run_start, the last is summary. Lines are buffered and posted every telemetry.flushEvery seconds
// and when the run ends; anything that can't be sent is kept on the device and retried later.
// Switches live under telemetry.* in the tuner.
public static class Telemetry
{
    static bool Enabled => Tune.B("telemetry.enabled", true);
    static bool EnemyEvents => Tune.B("telemetry.enemyEvents", true);
    static float SnapshotEvery => Tune.F("telemetry.snapshotEvery", 10f);
    static float FlushEvery => Tune.F("telemetry.flushEvery", 30f);

    // The enemy whose Update is running, so bullets and hazards it creates know who fired them.
    public static string Actor;

    // What is hurting enemies right now (a spell id, or Burn, Steam, Shatter...). Set it around a
    // TakeDamage call with `using (Telemetry.By("Steam")) ...`; Enemy.TakeDamage credits it.
    public static string DamageBy;
    public static Scope By(string source) => new Scope(source);

    public readonly struct Scope : IDisposable
    {
        readonly string previous;
        public Scope(string source) { previous = DamageBy; DamageBy = source; }
        public void Dispose() => DamageBy = previous;
    }

    public static string RunId { get; private set; }
    static bool active;
    static readonly StringBuilder pending = new StringBuilder();
    static bool sending, again;
    static float nextFlush, nextSnapshot;
    static int nextEnemyId;
    static int runSerial;   // enemies left over from an earlier run don't count toward this one

    class Tally { public string tier; public int spawned, killed, despawned, hits; public float damage, lifeSum; }
    static readonly Dictionary<string, Tally> enemies = new Dictionary<string, Tally>();
    static readonly Dictionary<string, float> damageBySource = new Dictionary<string, float>();
    static readonly Dictionary<string, float> damageByKind = new Dictionary<string, float>();
    static readonly Dictionary<string, int> runePicks = new Dictionary<string, int>();
    static readonly List<string> tomePicks = new List<string>();
    static readonly List<string> evolvePicks = new List<string>();
    static int rests, hurtCount, blocked;
    static float damageTotal;
    static string lastHitBy;
    static string offer;           // what the open level-up screen offered, as JSON
    static int offerLevel, claimedLevel;
    static Enemy boss;
    static float bossStart, bossDamage;
    static int bossHits;
    static readonly Dictionary<string, float> dealtBySource = new Dictionary<string, float>();
    static readonly Dictionary<string, int> killsBySource = new Dictionary<string, int>();
    static readonly Dictionary<string, float> healBySource = new Dictionary<string, float>();
    static readonly Dictionary<string, int> effects = new Dictionary<string, int>();
    static readonly List<string> levelTimes = new List<string>();
    static int xpDropped, xpCollected;
    static float dealtTotal, healTotal, tickDealt, tickHealed;
    // Frame rate: frames and real time since the last tick, the slowest frame, and per-tick averages.
    static int frames;
    static float frameTime, worstFrame, fpsLow = float.MaxValue, runFrameTime;
    static int runFrames;

    static float T
    {
        get
        {
            var gm = GameManager.Instance;
            return gm != null && gm.Director != null ? gm.Director.Elapsed : 0f;
        }
    }

    // ---------------------------------------------------------------- run lifecycle

    public static void BeginRun(GameManager gm, List<Biome> zones)
    {
        active = Enabled;
        if (!active) return;
        RunId = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + "-" + UnityEngine.Random.Range(0x1000, 0xffff).ToString("x4");
        pending.Clear();
        enemies.Clear();
        damageBySource.Clear();
        damageByKind.Clear();
        runePicks.Clear();
        tomePicks.Clear();
        evolvePicks.Clear();
        rests = hurtCount = blocked = 0;
        dealtBySource.Clear();
        killsBySource.Clear();
        healBySource.Clear();
        effects.Clear();
        levelTimes.Clear();
        xpDropped = xpCollected = 0;
        dealtTotal = healTotal = tickDealt = tickHealed = 0f;
        frames = runFrames = 0;
        frameTime = worstFrame = runFrameTime = 0f;
        fpsLow = float.MaxValue;
        damageTotal = 0f;
        lastHitBy = null;
        offer = null;
        claimedLevel = 1;
        boss = null;
        nextEnemyId = 0;
        runSerial++;
        nextSnapshot = SnapshotEvery;
        nextFlush = Time.unscaledTime + FlushEvery;

        var tomes = new List<string>();
        var evolutions = new List<string>();
        foreach (var id in Spell.AllIds)
        {
            if (Grimoire.HasTome(id)) tomes.Add(id.ToString());
            for (int tier = 2; tier <= Spell.MaxTier; tier++)
                if (Grimoire.HasEvolution(id, tier)) evolutions.Add($"{id} {tier}");
        }
        var names = new List<string>();
        foreach (var z in zones) names.Add(z.Name);

        Line("run_start")
            .S("run", RunId)
            .S("date", DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture))
            .S("device", SystemInfo.deviceModel)
            .S("platform", Application.platform.ToString())
            .S("version", Application.version)
            .S("tuning", Tune.Source)
            .S("style", ArtStyle.Current.ToString())
            .I("bosses", Director.RunBosses)
            .Strs("zones", names)
            .Strs("tomesOwned", tomes)
            .Strs("evolutionsOwned", evolutions)
            .I("gold", Grimoire.Gold)
            .N("maxHealth", gm.Player.MaxHealth)
            .Write();
        Zone(1, zones[0].Name);
    }

    // outcome: death, win or quit.
    public static void EndRun(GameManager gm, string outcome, int gold)
    {
        if (!active) return;
        var p = gm.Player;
        var d = gm.Director;
        if (outcome == "death")
            Line("death").S("killer", lastHitBy).I("level", p.Level).S("boss", boss != null ? boss.Def.Name : null).I("alive", Enemy.All.Count).Write();
        else if (outcome == "win")
            Line("win").I("level", p.Level).Write();
        else
            Line("quit").I("level", p.Level).N("hp", p.Health).Write();

        var spells = new StringBuilder("[");
        for (int i = 0; i < p.Spells.Count; i++)
        {
            var s = p.Spells[i];
            var runes = new StringBuilder("{");
            foreach (Rune r in Enum.GetValues(typeof(Rune)))
            {
                float n = s.RuneCount(r);
                if (n <= 0.001f) continue;
                if (runes.Length > 1) runes.Append(',');
                runes.Append(Str(r.ToString())).Append(':').Append(Num(n));
            }
            runes.Append('}');
            if (i > 0) spells.Append(',');
            spells.Append($"{{\"id\":{Str(s.Id.ToString())},\"name\":{Str(s.Title)},\"tier\":{s.Tier},\"level\":{s.Level},\"runes\":{runes}}}");
        }
        spells.Append(']');

        var foes = new StringBuilder("{");
        foreach (var kv in enemies)
        {
            var t = kv.Value;
            if (foes.Length > 1) foes.Append(',');
            foes.Append(Str(kv.Key)).Append($":{{\"tier\":{Str(t.tier)},\"spawned\":{t.spawned},\"killed\":{t.killed},\"despawned\":{t.despawned},");
            foes.Append($"\"avgLife\":{Num(t.killed > 0 ? t.lifeSum / t.killed : 0f)},\"hitsOnPlayer\":{t.hits},\"damageToPlayer\":{Num(t.damage)}}}");
        }
        foes.Append('}');

        var aliveByType = new Dictionary<string, float>();
        foreach (var e in Enemy.All) aliveByType[e.Def.Name] = (aliveByType.TryGetValue(e.Def.Name, out var n) ? n : 0f) + 1f;

        int kills = 0;
        foreach (var t in enemies.Values) kills += t.killed;

        Line("summary")
            .S("run", RunId)
            .S("outcome", outcome)
            .N("duration", d.Elapsed)
            .I("level", p.Level)
            .I("bossesDefeated", d.BossesDefeated)
            .I("threat", d.Threat + 1)
            .N("healthScale", d.HealthScale)
            .I("kills", kills)
            .I("score", gm.Score)
            .I("gold", gold)
            .S("killer", outcome == "death" ? lastHitBy : null)
            .Raw("spells", spells.ToString())
            .Raw("enemies", foes.ToString())
            .Raw("damageTaken", $"{{\"total\":{Num(damageTotal)},\"hits\":{hurtCount},\"blocked\":{blocked},\"bySource\":{Map(damageBySource)},\"byKind\":{Map(damageByKind)}}}")
            .Raw("picks", $"{{\"runes\":{IntMap(runePicks)},\"tomes\":{StrList(tomePicks)},\"evolves\":{StrList(evolvePicks)},\"rests\":{rests}}}")
            .Raw("aliveAtEnd", Map(aliveByType))
            .Raw("damageDealt", $"{{\"total\":{Num(dealtTotal)},\"bySource\":{Map(dealtBySource)},\"killsBySource\":{IntMap(killsBySource)}}}")
            .Raw("healing", $"{{\"total\":{Num(healTotal)},\"bySource\":{Map(healBySource)}}}")
            .Raw("xp", $"{{\"dropped\":{xpDropped},\"collected\":{xpCollected},\"levelTimes\":[{string.Join(",", levelTimes)}]}}")
            .Raw("combos", IntMap(effects))
            .Raw("fps", $"{{\"avg\":{Num(runFrameTime > 0f ? runFrames / runFrameTime : 0f)},\"worstTick\":{Num(fpsLow == float.MaxValue ? 0f : fpsLow)}}}")
            .Write();
        active = false;
        gm.StartCoroutine(Flush());
    }

    // Called every frame by the GameManager while a run is going.
    public static void Update(GameManager gm)
    {
        if (!active) return;
        if (gm.State == GameState.Playing)
        {
            float dt = Time.unscaledDeltaTime;
            frames++;
            frameTime += dt;
            runFrames++;
            runFrameTime += dt;
            worstFrame = Mathf.Max(worstFrame, dt);
        }
        if (gm.State == GameState.Playing && T >= nextSnapshot)
        {
            nextSnapshot = T + Mathf.Max(1f, SnapshotEvery);
            float fps = frameTime > 0f ? frames / frameTime : 0f;
            if (frameTime > 1f) fpsLow = Mathf.Min(fpsLow, fps);
            int chaff = 0, medium = 0;
            foreach (var e in Enemy.All)
                if (e.Def.Tier == EnemyTier.Chaff) chaff++;
                else if (e.Def.Tier == EnemyTier.Medium) medium++;
            var p = gm.Player;
            Line("tick").N("hp", p.Health).N("maxHp", p.MaxHealth).I("level", p.Level).I("xp", p.Xp).I("xpToNext", p.XpToNext)
                .I("chaff", chaff).I("medium", medium).I("bullets", EnemyBullets())
                .I("gems", XpGem.All.Count).I("threat", gm.Director.Threat + 1).N("healthScale", gm.Director.HealthScale)
                .N("dealt", tickDealt).N("healed", tickHealed)
                .N("fps", fps).N("fpsMin", worstFrame > 0f ? 1f / worstFrame : 0f).Write();
            tickDealt = tickHealed = 0f;
            frames = 0;
            frameTime = worstFrame = 0f;
        }
        if (Time.unscaledTime >= nextFlush)
        {
            nextFlush = Time.unscaledTime + Mathf.Max(5f, FlushEvery);
            gm.StartCoroutine(Flush());
        }
    }

    static int EnemyBullets()
    {
        int n = 0;
        foreach (var p in Projectile.All) if (p.Team == Team.Enemy) n++;
        return n;
    }

    // ---------------------------------------------------------------- events

    public static void Zone(int number, string name)
    {
        if (active) Line("zone").I("zone", number).S("name", name).Write();
    }

    public static void Swarm(int threat, int count)
    {
        if (active) Line("swarm").I("threat", threat).I("count", count).Write();
    }

    public static void Spawned(Enemy e, string cause)
    {
        if (!active) return;
        e.TelemetryId = ++nextEnemyId;
        e.TelemetryRun = runSerial;
        e.BornAt = T;
        Get(e).spawned++;
        if (e.IsBoss)
        {
            boss = e;
            bossStart = T;
            bossDamage = 0f;
            bossHits = 0;
            Line("boss_spawn").I("id", e.TelemetryId).S("enemy", e.Def.Name).N("hp", e.MaxHealth).Write();
        }
        else if (EnemyEvents)
            Line("spawn").I("id", e.TelemetryId).S("enemy", e.Def.Name).S("tier", e.Def.Tier.ToString()).S("cause", cause).N("hp", e.MaxHealth).Write();
    }

    public static void Killed(Enemy e)
    {
        if (!Ours(e)) return;
        float life = T - e.BornAt;
        var t = Get(e);
        t.killed++;
        string by = e.LastHitBy ?? "Other";
        killsBySource[by] = (killsBySource.TryGetValue(by, out var k) ? k : 0) + 1;
        t.lifeSum += life;
        if (e == boss)
        {
            Line("boss_kill").I("id", e.TelemetryId).S("enemy", e.Def.Name).N("fight", life).N("damageTaken", bossDamage).I("hitsTaken", bossHits)
                .N("hp", GameManager.Instance.Player.Health).Write();
            boss = null;
        }
        else if (EnemyEvents)
            Line("kill").I("id", e.TelemetryId).S("enemy", e.Def.Name).N("life", life).Write();
    }

    // Cleared without being killed (zone change).
    public static void Despawned(Enemy e)
    {
        if (Ours(e)) Get(e).despawned++;
    }

    // kind: contact, bullet or hazard.
    public static void Hurt(float amount, string source, string kind, float hpAfter)
    {
        if (!active) return;
        source = source ?? "unknown";
        lastHitBy = source;
        hurtCount++;
        damageTotal += amount;
        damageBySource[source] = (damageBySource.TryGetValue(source, out var a) ? a : 0f) + amount;
        damageByKind[kind] = (damageByKind.TryGetValue(kind, out var b) ? b : 0f) + amount;
        if (enemies.TryGetValue(source, out var t)) { t.hits++; t.damage += amount; }
        if (boss != null) { bossDamage += amount; bossHits++; }
        Line("hurt").N("amount", amount).S("src", source).S("kind", kind).N("hp", hpAfter).Write();
    }

    // by: veil or ward.
    public static void Blocked(string by, string source)
    {
        if (!active) return;
        blocked++;
        Line("blocked").S("by", by).S("src", source ?? "unknown").Write();
    }

    public static void LevelUp(int level)
    {
        if (!active) return;
        levelTimes.Add(Num(T));
        Line("levelup").I("level", level).Write();
    }

    // Damage an enemy actually lost (overkill not counted), credited to whatever dealt it.
    public static void Dealt(float amount, string by)
    {
        if (!active || amount <= 0f) return;
        dealtTotal += amount;
        tickDealt += amount;
        dealtBySource[by] = (dealtBySource.TryGetValue(by, out var a) ? a : 0f) + amount;
    }

    // Health the wizard actually got back (overheal not counted).
    public static void Healed(float amount, string source)
    {
        if (!active || amount <= 0f) return;
        healTotal += amount;
        tickHealed += amount;
        healBySource[source] = (healBySource.TryGetValue(source, out var a) ? a : 0f) + amount;
    }

    // A combo or named effect went off (Steam, Conductive, Bug Burst...).
    public static void Effect(string name)
    {
        if (active) effects[name] = (effects.TryGetValue(name, out var n) ? n : 0) + 1;
    }

    public static void XpDropped(int amount) { if (active) xpDropped += amount; }
    public static void XpCollected(int amount) { if (active) xpCollected += amount; }

    // The level-up screen opened with these choices.
    public static void Offer(List<Rune> runes, SpellId? tome, List<Spell> evolves)
    {
        if (!active) return;
        offerLevel = ++claimedLevel;
        var r = new List<string>();
        foreach (var x in runes) r.Add(x.ToString());
        var ev = new List<string>();
        foreach (var s in evolves) ev.Add($"{s.Id} {s.Tier + 1}");
        offer = $"{{\"runes\":{StrList(r)},\"tome\":{(tome.HasValue ? Str(tome.Value.ToString()) : "null")},\"evolve\":{StrList(ev)}}}";
    }

    public static void StartTome(List<SpellId> offered, SpellId took)
    {
        if (!active) return;
        var o = new List<string>();
        foreach (var x in offered) o.Add(x.ToString());
        tomePicks.Add(took.ToString());
        Line("pick").I("level", 1).S("took", "start_tome").Raw("offered", $"{{\"tomes\":{StrList(o)}}}").S("spell", took.ToString()).Write();
    }

    public static void PickRune(Rune rune, Spell spell, bool resonance)
    {
        if (!active) return;
        runePicks[rune.ToString()] = (runePicks.TryGetValue(rune.ToString(), out var n) ? n : 0) + 1;
        Line("pick").I("level", offerLevel).S("took", "rune").Raw("offered", offer ?? "null").S("rune", rune.ToString()).S("spell", spell.Id.ToString())
            .I("sockets", spell.TotalRunes).B("resonance", resonance).Write();
    }

    public static void PickTome(SpellId id)
    {
        if (!active) return;
        tomePicks.Add(id.ToString());
        Line("pick").I("level", offerLevel).S("took", "tome").Raw("offered", offer ?? "null").S("spell", id.ToString()).Write();
    }

    public static void PickEvolve(Spell spell)
    {
        if (!active) return;
        evolvePicks.Add($"{spell.Id} {spell.Tier}");
        Line("pick").I("level", offerLevel).S("took", "evolve").Raw("offered", offer ?? "null").S("spell", spell.Id.ToString()).I("tier", spell.Tier).Write();
        Line("evolve").S("spell", spell.Id.ToString()).I("tier", spell.Tier).S("name", spell.Title).Write();
    }

    public static void PickRest()
    {
        if (!active) return;
        rests++;
        Line("pick").I("level", offerLevel).S("took", "rest").Raw("offered", offer ?? "null").Write();
    }

    static bool Ours(Enemy e) => active && e.TelemetryId != 0 && e.TelemetryRun == runSerial;

    static Tally Get(Enemy e)
    {
        if (!enemies.TryGetValue(e.Def.Name, out var t)) enemies[e.Def.Name] = t = new Tally { tier = e.Def.Tier.ToString() };
        return t;
    }

    // ---------------------------------------------------------------- sending

    static string BaseUrl;
    static string PendingDir => Path.Combine(Application.persistentDataPath, "telemetry");

    public static void SetServer(string baseUrl) => BaseUrl = baseUrl;

    // The app is going to the background: keep what we have on the device in case it never comes back.
    public static void SaveToDevice()
    {
        if (pending.Length == 0 || RunId == null) return;
        try
        {
            Directory.CreateDirectory(PendingDir);
            File.AppendAllText(Path.Combine(PendingDir, RunId + ".jsonl"), pending.ToString());
            pending.Clear();
        }
        catch (Exception e) { Debug.LogWarning($"telemetry: could not save to device: {e.Message}"); }
    }

    // Moves the buffer to the device, then posts every saved file (this run's and any older ones the
    // server never got). A file is only deleted once the server has it, so failures retry next flush.
    public static IEnumerator Flush()
    {
        SaveToDevice();   // one queue: device files, oldest first
        if (BaseUrl == null) yield break;
        if (sending) { again = true; yield break; }
        sending = true;
        do
        {
            again = false;
            SaveToDevice();
            string[] files;
            try { files = Directory.Exists(PendingDir) ? Directory.GetFiles(PendingDir, "*.jsonl") : new string[0]; }
            catch (Exception) { files = new string[0]; }
            Array.Sort(files, StringComparer.Ordinal);
            foreach (var file in files)
            {
                string body;
                try { body = File.ReadAllText(file); }
                catch (Exception) { continue; }
                string run = Path.GetFileNameWithoutExtension(file);
                bool ok = false;
                yield return Post(run, body, r => ok = r);
                if (!ok) { again = false; break; }
                // Lines saved while the request was out were appended to the same file; keep those.
                try
                {
                    string now = File.ReadAllText(file);
                    if (now.Length > body.Length) File.WriteAllText(file, now.Substring(body.Length));
                    else File.Delete(file);
                }
                catch (Exception) { }
            }
        } while (again);
        sending = false;
    }

    static IEnumerator Post(string run, string body, Action<bool> done)
    {
        using (var req = new UnityWebRequest(BaseUrl + "/telemetry/" + run, "POST"))
        {
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/x-ndjson");
            req.SetRequestHeader("ngrok-skip-browser-warning", "1");
            req.timeout = 10;
            yield return req.SendWebRequest();
            done(req.result == UnityWebRequest.Result.Success);
        }
    }

    // ---------------------------------------------------------------- JSON lines

    static LineBuilder Line(string ev) => new LineBuilder(ev, T);

    sealed class LineBuilder
    {
        readonly StringBuilder sb = new StringBuilder(128);

        public LineBuilder(string ev, float t)
        {
            sb.Append("{\"t\":").Append(Num(t)).Append(",\"ev\":").Append(Str(ev));
        }

        LineBuilder Key(string k) { sb.Append(',').Append(Str(k)).Append(':'); return this; }
        public LineBuilder S(string k, string v) { Key(k).sb.Append(v == null ? "null" : Str(v)); return this; }
        public LineBuilder N(string k, float v) { Key(k).sb.Append(Num(v)); return this; }
        public LineBuilder I(string k, int v) { Key(k).sb.Append(v.ToString(CultureInfo.InvariantCulture)); return this; }
        public LineBuilder B(string k, bool v) { Key(k).sb.Append(v ? "true" : "false"); return this; }
        public LineBuilder Raw(string k, string json) { Key(k).sb.Append(json); return this; }
        public LineBuilder Strs(string k, List<string> v) => Raw(k, StrList(v));

        public void Write() => pending.Append(sb).Append("}\n");
    }

    static string Num(float v) => float.IsNaN(v) || float.IsInfinity(v) ? "0" : Math.Round(v, 2).ToString(CultureInfo.InvariantCulture);

    static string Str(string s)
    {
        var sb = new StringBuilder(s.Length + 2).Append('"');
        foreach (char c in s)
        {
            if (c == '"' || c == '\\') sb.Append('\\').Append(c);
            else if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4"));
            else sb.Append(c);
        }
        return sb.Append('"').ToString();
    }

    static string StrList(List<string> v)
    {
        var sb = new StringBuilder("[");
        for (int i = 0; i < v.Count; i++) { if (i > 0) sb.Append(','); sb.Append(Str(v[i])); }
        return sb.Append(']').ToString();
    }

    static string Map(Dictionary<string, float> m)
    {
        var sb = new StringBuilder("{");
        foreach (var kv in m) { if (sb.Length > 1) sb.Append(','); sb.Append(Str(kv.Key)).Append(':').Append(Num(kv.Value)); }
        return sb.Append('}').ToString();
    }

    static string IntMap(Dictionary<string, int> m)
    {
        var sb = new StringBuilder("{");
        foreach (var kv in m) { if (sb.Length > 1) sb.Append(','); sb.Append(Str(kv.Key)).Append(':').Append(kv.Value); }
        return sb.Append('}').ToString();
    }
}
