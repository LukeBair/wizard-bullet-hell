# Sound effects research

Practical notes for a solo dev shipping a mobile bullet-hell survivor. Items marked *(sourced)* came from
web searches this session; the rest is established game-audio practice from memory, so treat it as
guidance to verify by ear or on device.

## 1. How sound effects get made

| Approach | What it is | Pros | Cons |
|---|---|---|---|
| Synthesis (sfxr / jsfxr / bfxr / ChipTone style) | One or two oscillators (square, saw, triangle, noise) with a pitch sweep, an amplitude envelope, a filter, and some vibrato or arpeggio | Free, tiny, instant, perfectly consistent style, matches the pixel look | Sounds "retro"; hard to get weighty or organic impacts |
| Layered synthesis | Stack a transient click, a pitched body, a noise tail and a sub thump, each with its own envelope and filter | Punch and weight; easy to make variations by nudging parameters | Needs a little DSP code; tuning by ear |
| Recorded or foley | Record real objects (paper, water, cloth, vegetables, zippers) and process them (pitch, reverb, EQ) | Organic, rich | Needs a mic, a quiet room and time |
| Libraries | Download or buy packs | Fast, professional | Style mismatch; licence bookkeeping |
| AI audio generators | Text-to-SFX services | Fast ideas, one-off big moments (boss roar) | Licence and terms vary by service and tier; consistency and loopability vary; clean tails often need editing |

**Licences (App Store safe):**
- **CC0 / public domain**: no credit required, commercial use fine. Kenney's audio packs are CC0 *(sourced)*. On Freesound, licences are per sound (CC0, CC-BY, CC-BY-NC), so filter for CC0 and skip NC *(sourced)*. On OpenGameArt, check the bundled licence text too, because some entries add restrictions CC0 doesn't allow *(sourced)*.
- **CC-BY**: fine commercially but needs attribution. The game already has a Credits screen (`Music.Credits`), so this is manageable.
- **Purchasable packs** (itch.io, Humble bundles, Unity Asset Store): read the EULA. Normally allowed in a shipped game, not redistributable as raw files.
- **AI generators**: check commercial-use terms of the exact plan used, and keep screenshots of them.
- Apple does not vet audio licences itself, but you are responsible. Keep a source/licence spreadsheet row for every imported file (URL, author, licence, date).

**Recommended path for this game:** keep the synthesised sounds as the default and the fallback, make
the 15-20 sounds that matter most (spells, hurt, level-up, boss) better by layering, and later swap
chosen ones for CC0 or purchased files dropped into `Resources/Sfx/` with no code changes. Audition
AI-generated sounds only for rare, big moments.

## 2. Keeping high-frequency sounds from becoming noise

No survivor game documents its own rules publicly *(search found no source for Vampire Survivors' internals)*,
but the techniques are standard (Wwise and FMOD both document concurrency/"playback limiting"; Unreal has
Sound Concurrency groups *(sourced)*; hobby survivor devs report using a capped shared pool per effect *(sourced)*):

1. **Voice cap per sound**: at most N copies of a sound at once (hits 3, gems 2, deaths 4, casts 2-3).
2. **Global voice cap and stealing**: a fixed pool; when full, steal the quietest/oldest, lowest-priority voice. Never let a low-priority sound steal a high-priority one.
3. **Per-sound cooldown (rate limiting)**: 30-100 ms for frequent sounds. Better than dropping: remember the dropped count and make the next sound a little louder, so a mass hit still *feels* bigger.
4. **Pitch and volume randomisation**: roughly ±5-10% pitch and ±10% volume. Stops the machine-gun effect.
5. **Round robin**: 3-5 variants, never the same one twice in a row. Variants differ in tone, not just pitch.
6. **Priority tiers**: player hurt, level-up, boss and UI beat everything else; ambient hits are lowest.
7. **Ducking**: briefly lower low-priority sounds (and music) when a high-priority sound plays (boss roar, level-up, death) so it reads clearly.
8. **Grouping**: when 20 enemies die in a second, play a few deaths plus one deeper "burst" thump instead of 20 pops.
9. **Spatial attenuation**: quieter and panned by on-screen position. A kill at the screen edge matters less than one next to the wizard.
10. **Frequency budgeting**: high-rate sounds should be short, quiet and in a narrow band (gems high and tiny, hits mid and dry). Keep the low end (below ~150 Hz) for rare big events so it does not mud up.
11. **Loudness budgeting**: give each category a loudness slot, e.g. player feedback > spell casts > enemy deaths > hits > gems. Scale volume down as the number of active voices grows, so 20 overlapping sounds sum to about the same level as 6.

## 3. Mobile and Unity-on-iOS specifics

- **Load type** *(sourced, Unity manual)*: *Decompress On Load* suits small compressed effects (no runtime decode cost, more RAM); *Compressed In Memory* decodes at play time and suits long music; *ADPCM* is about 3.5x smaller than PCM with little CPU, and Unity recommends it for noisy, frequently played sounds (impacts, weapons). On mobile, compressed audio is MP3/AAC with hardware decode. Plan for imported effects: short SFX = ADPCM (or Vorbis with Decompress On Load), Preload Audio Data on, music = Compressed In Memory or Streaming.
- **Latency** *(sourced)*: clips still loading defer play requests until loaded; preload or call `LoadAudioData`. For iOS, set Project Settings > Audio > DSP Buffer Size to *Best latency* if taps feel slow (default "Good" is fine for most).
- **Runtime-synthesised clips**: PCM float in RAM = 4 bytes x 44100 per second (~176 KB/s of sound). 35 sounds averaging 0.25 s with 3 variants on the busy ones comes to about 3-5 MB. Fine, but 22.05 kHz would halve it for noise/low sounds. CPU cost is paid once at creation (about 1-4 ms per sound), so spread it over frames instead of building in one frame.
- **Per-frame allocation**: avoid `PlayOneShot` plus string concatenation per play; pre-build keys, cache tuned numbers, reuse one pooled `AudioSource` per voice.
- **Mute switch / session** *(not sourced; verify on device)*: Unity iOS uses an ambient-style session by default, so the ringer switch silences the game and other apps' audio can keep playing. Keep that default for a casual game and expose only in-game volume. If *Mute Other Audio Sources* is enabled in iOS player settings, the game takes over the session.
- **Interruptions and backgrounding**: Unity pauses audio on app pause; calls/Siri can leave the output unstarted on some versions. Hook `OnApplicationPause`/`OnApplicationFocus` to stop voices and set `AudioListener.pause`, and subscribe to `AudioSettings.OnAudioConfigurationChanged` (output device change, e.g. headphones unplugged) to restart cleanly.
- **Budget**: Unity's default real-voice limit is 32 (`AudioSettings`), with virtual voices beyond that. A pool of 20-24 own voices stays well inside it, leaving room for music (2 sources).

## 4. Mixing

- **AudioMixer groups** (Master > SFX, UI, Music), with exposed volume parameters set in dB (`20*log10(linear)`), are the standard. Add a low-pass snapshot for pause menus and a ducking send if needed. Mixer assets are created in the editor, though, and this project builds everything from code, so the first pass uses **software buses** (a per-bus gain applied when a voice starts) that behave the same way. A real `AudioMixer` can replace them later without touching call sites.
- **Loudness targets**: aim for a consistent *peak* around -3 to -6 dBFS for one-shots, music about 6 dB under SFX in busy play, mobile speakers rolling off below ~200 Hz (don't put important content there). Commercial mobile games target roughly -16 LUFS integrated; per-sound LUFS matching is unrealistic here, so trim by ear on the phone speaker AND headphones.
- **Avoid clipping when many overlap**: headroom (master 0.8), the voice-count gain curve above, and short envelopes. A hard-clip is the failure mode of 20 stacked full-scale sounds; Unity does not limit at the listener by default (optionally add an `AudioMixer` Limiter/Compressor later).
- **Test on the phone**: the Mac speakers lie. Luke should play a 10-minute late-game run with headphones and the speaker.

## Sources
- [Unity manual: Audio Clip import settings](https://docs.unity.com/en-us/engine/6000.0/manual/audio/reference/class-audio-clip.md)
- [Unreal: Sound Concurrency reference](https://dev.epicgames.com/documentation/unreal-engine/sound-concurrency-reference-guide)
- [Cozy Space Survivors devlog: sound regulation](https://simonschreibt.itch.io/cozy-space-survivors/devlog/634575/update-61-sound-regulation)
- [Free sound effects and music for games (licence overview)](https://app.cinevva.com/guides/free-sound-effects-music)
- [Freesound forum: CC0 and credit](https://freesound.org/forum/legal-help-and-attribution-questions/35069)
