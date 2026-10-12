# Homepage

A static, single-file homepage for the game: `index.html` plus the Dusk sprites in `assets/` (copied from `Assets/Resources/Art/Dusk`). No build step.

- **Preview locally:** `python3 -m http.server -d site 8000`, then open http://localhost:8000.
- **Store links:** fill in `STORE_LINKS` near the bottom of `index.html`. The buttons switch from "Coming soon" to "Download" by themselves.
- **Hosting:** any static host works (GitHub Pages, Netlify, Cloudflare Pages). Point it at the `site/` folder.
