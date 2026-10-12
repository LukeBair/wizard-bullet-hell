#!/bin/bash
# Double-click to start the tuner and open it in your browser.
cd "$(dirname "$0")"
(sleep 1 && open http://localhost:8787) &
exec node server.js
