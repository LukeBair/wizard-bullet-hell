# Wizard Bullet Hell: path to the App Store

Target: v1.0 in about 3-5 weeks. v1.1 adds the new biome.

## 0. Unblock the repo
- [ ] Resolve merge conflicts in `ArtStyle.cs`, `GameManager.cs`, `Player.cs`, `Settings.cs`
- [ ] Pull origin/main (3 commits behind), commit untracked `Assets/AppIcon.meta`, `tools/icons/`
- [ ] Confirm the project compiles and a run plays start to finish

## 1. Playtest and stabilise (on a real device)
- [ ] Difficulty curve and run length feel right
- [ ] Dusk holds a steady framerate on an older phone
- [ ] No crashes or memory growth over several runs
- [ ] Pause works, app backgrounding/resume works
- [ ] Settings cover volume and haptics

## 2. Free tier + in-app purchase
- [ ] Pick the 5 free spells (should show off the rune system)
- [ ] Add Unity IAP (StoreKit), one non-consumable "Unlock Everything"
- [ ] Saved entitlement flag; gate spell tomes and runes in the level-up flow
- [ ] Never offer locked spells as cards; add a "Full version" screen on the title
- [ ] **Restore Purchases** button (Apple requires it)
- [ ] Create the product in App Store Connect, price it, submit with the first build
- [ ] Test with a sandbox account

## 3. Stage-specific enemies
- [ ] About 2 unique enemies per biome (Forest, Academy, Caves) added to each `EnemyRoster`
- [ ] Sprite sheets via the TRELLIS -> Blender pipeline
- [ ] Tune their stats in `tuning.json`

## 4. Story bits
- [ ] Short text cards: intro per biome, before each boss, on death, on win
- [ ] Small dialogue-card UI
- [ ] Write the one-line ending
- [ ] (Skip for v1) lore unlocks that need persistence

## 5. New spells and runes
- [ ] 2-3 new spells: behaviour, painted VFX, tuning entries, tier I-III evolution
- [ ] 1-2 new runes with an effect on every spell
- [ ] Combos and balance pass

## 6. Decide on art styles
- [ ] Ship Dusk only, or finish Classic / Vector / CRT too (fewer styles = less QA)

## 7. Strip dev-only features for release
- [ ] Telemetry and live tuning calls to the Mac server (no LAN requests in the shipped app)
- [ ] `L` instant-level-up key and `GameManager.Autoplay`
- [ ] Tuner dependence: bundled `tuning.json` is the source of truth

## 8. App Store requirements
- [ ] Apple Developer Program membership ($99/yr)
- [ ] App Store Connect listing: name, subtitle, description, keywords, category, age rating
- [ ] Screenshots: 6.9" and 6.5" iPhone (plus iPad if supported)
- [ ] Privacy policy URL (host on `site/`) and App Privacy label answers
- [ ] Support URL and contact
- [ ] Set `bundleVersion` and build number; check icon and launch screens
- [ ] Confirm no ads or other SDKs are in the build (the code search hit "ads" strings; verify)

## 9. Beta and submit
- [ ] Archive in Xcode, upload to TestFlight, test with friends
- [ ] Fix beta feedback
- [ ] Submit for review (expect 1-3 days, possibly a rejection round)

## v1.1 and later
- [ ] New biome: art, enemy roster, boss with unique patterns, music, balance
- [ ] Optionally sell it as part of the unlock or as an update
