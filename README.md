# ⚡ Charger Drama

A tiny Windows app that gives your laptop feelings about its charger.

- **Plug the charger in:** it laughs like a maniac.
- **Pull the charger out:** *"Nooooo! No no no no! Please... no!"*

It sits in the system tray as a little yellow lightning bolt and uses almost no resources. It checks the charger a few times a second.

## Install

1. Download or clone this repo.
2. Double-click **`Install.cmd`**.

That's it. No admin rights or extra software needed. It uses the PowerShell that comes with Windows 10/11.

The installer:
- copies the app to `%LOCALAPPDATA%\ChargerDrama`
- puts a **Charger Drama** shortcut on your Desktop that turns it **on/off**
- assigns the hotkey **Ctrl + Alt + D** to that shortcut
- starts it

To remove everything, double-click **`Uninstall.cmd`**.

## Using it

| What | How |
|---|---|
| Turn on / off | Desktop shortcut **Charger Drama**, or **Ctrl + Alt + D** (it says hello / goodbye) |
| Change the voice | Right-click the ⚡ in the tray → **Voice** |
| Hear it without touching the cable | Right-click ⚡ → **Test: laugh** / **Test: nooo** |
| Run it every time Windows starts | Right-click ⚡ → **Start with Windows** |

## Voices

| Voice | Style |
|---|---|
| Mad Scientist | escalating evil laugh, begs for his experiment |
| Drama Queen | over-the-top theatrical |
| British Villain | calm, menacing, "you'll regret this" |
| Italian Drama | *"Mamma mia! No no no no! Ti prego, no!"* |
| Cartoon Kid | giggly, "no fair!" |
| Classic Robot | the built-in Windows voice (works even if the sound files are missing) |

Each character has 3 laughs and 3 "nooo"s, picked at random.

### Add your own voice (e.g. record yourself)

1. Right-click ⚡ → **Voice** → **Open sounds folder**.
2. Make a new folder, e.g. `My Voice`.
3. Put audio files in it named `laugh…` and `nooo…` (e.g. `laugh-1.m4a`, `nooo-1.m4a`). You can also add `hello…` and `bye…`.
   `.mp3`, `.wav`, `.m4a` and `.wma` all work, so recordings from the Windows **Sound Recorder** app work as they are.
4. Right-click ⚡ → **Voice** → **Reload voices**, then pick it.

### Regenerate or edit the AI voices

The voice packs in `sounds/` were made with Microsoft's neural voices via [edge-tts](https://github.com/rany2/edge-tts).
To change the lines, edit `tools/make_voices.py` and run:

```bash
pip install -r tools/requirements.txt
python tools/make_voices.py
```

Then run `Install.cmd` again to update your installed copy.

## Files

| File | What it does |
|---|---|
| `charger-drama.ps1` | the app (tray icon, charger watcher, voices) |
| `toggle.ps1` | turns the app on if it's off, off if it's on |
| `install.ps1` / `Install.cmd` / `Uninstall.cmd` | install / remove |
| `sounds/` | voice packs, one folder per character |
| `tools/make_voices.py` | generates the voice packs |
