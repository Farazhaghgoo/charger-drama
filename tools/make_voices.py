"""
Generates the voice packs in ../sounds using Microsoft's neural voices (via edge-tts).

    pip install -r tools/requirements.txt
    python tools/make_voices.py              # all characters
    python tools/make_voices.py "Mad Scientist"   # just one

Each clip is built from short segments, each with its own speed (rate) and pitch,
so the laughs can get faster and higher as they go. Edit the lines below and re-run.

Files per character:
    laugh-N.mp3  -> played when the charger is plugged in
    nooo-N.mp3   -> played when the charger is pulled out
    hello.mp3    -> played when you turn the app on
    bye.mp3      -> played when you turn the app off
"""

import asyncio
import sys
from pathlib import Path

import edge_tts

SOUNDS = Path(__file__).resolve().parent.parent / "sounds"

# (text, rate, pitch)
CHARACTERS = {
    "Mad Scientist": {
        "voice": "en-US-GuyNeural",
        "laugh": [
            [("Mwahahahaha!", "+0%", "-5Hz"),
             ("Ha ha ha ha ha ha!", "+25%", "+20Hz"),
             ("HA HA HA HA HA!", "+40%", "+45Hz"),
             ("Yes... YES! Sweet, sweet electricity!", "+0%", "+0Hz")],
            [("Heh. Heh heh heh.", "-15%", "-15Hz"),
             ("Ahahahahahaha!", "+20%", "+25Hz"),
             ("The power flows through my circuits!", "+10%", "+10Hz")],
            [("Ha!", "+0%", "+0Hz"),
             ("Ha ha!", "+15%", "+20Hz"),
             ("Ahahahahahahaha!", "+35%", "+45Hz"),
             ("One hundred percent, here I come!", "+10%", "+15Hz")],
        ],
        "nooo": [
            [("Noooooooo!", "-40%", "-10Hz"),
             ("No no no no no!", "+30%", "+25Hz"),
             ("Please... no!", "-20%", "+35Hz")],
            [("Wait wait wait!", "+30%", "+20Hz"),
             ("Nooooooo!", "-40%", "+0Hz"),
             ("My experiment! Plug me back in!", "+15%", "+30Hz")],
            [("No. No no no no no!", "+20%", "+15Hz"),
             ("Not the battery! Anything but the battery!", "+20%", "+35Hz")],
        ],
        "hello": [("Heh heh heh. I am awake.", "-5%", "-5Hz")],
        "bye": [("Fine. I will be quiet... for now.", "-10%", "-10Hz")],
    },
    "Drama Queen": {
        "voice": "en-US-AvaNeural",
        "laugh": [
            [("Ahahahahaha!", "+10%", "+10Hz"),
             ("Oh my god, YES!", "+15%", "+25Hz"),
             ("Ha ha ha ha ha ha!", "+30%", "+40Hz"),
             ("I feel so alive!", "+5%", "+20Hz")],
            [("Hee hee hee hee!", "+25%", "+40Hz"),
             ("Ahahaha! Charging, darling! Charging!", "+15%", "+20Hz")],
            [("Ha. Ha ha.", "-10%", "+0Hz"),
             ("AHAHAHAHAHA!", "+30%", "+45Hz"),
             ("Finally, someone who cares about me!", "+10%", "+20Hz")],
        ],
        "nooo": [
            [("Noooooooo!", "-40%", "+0Hz"),
             ("No no no no no!", "+30%", "+30Hz"),
             ("Please... no!", "-20%", "+40Hz")],
            [("How could you?!", "+10%", "+30Hz"),
             ("Nooooooo!", "-40%", "+10Hz"),
             ("Don't do this to me!", "+20%", "+40Hz")],
            [("No no no no no no!", "+35%", "+35Hz"),
             ("I can't live like this!", "+10%", "+30Hz")],
        ],
        "hello": [("I'm back, darling!", "+5%", "+15Hz")],
        "bye": [("Fine! I'll just stay silent. Forever.", "+0%", "+10Hz")],
    },
    "British Villain": {
        "voice": "en-GB-RyanNeural",
        "laugh": [
            [("Mwahahahaha.", "-10%", "-15Hz"),
             ("Ha ha ha ha ha!", "+15%", "+5Hz"),
             ("Excellent. Everything is going exactly according to plan.", "-10%", "-15Hz")],
            [("Heh heh heh.", "-15%", "-20Hz"),
             ("Ahahahahaha!", "+20%", "+15Hz"),
             ("Yes. Feel the current.", "-15%", "-20Hz")],
            [("Ha.", "-10%", "-15Hz"),
             ("Ha ha ha.", "+0%", "-5Hz"),
             ("HAHAHAHAHA!", "+30%", "+25Hz"),
             ("Splendid.", "-15%", "-20Hz")],
        ],
        "nooo": [
            [("Nooooooo!", "-40%", "-10Hz"),
             ("No no no no no!", "+30%", "+20Hz"),
             ("Please... no!", "-20%", "+25Hz")],
            [("You fool!", "+10%", "+10Hz"),
             ("No no no no!", "+30%", "+25Hz"),
             ("You'll regret this!", "+0%", "+15Hz")],
            [("Nooooo!", "-40%", "-5Hz"),
             ("This was not part of the plan!", "+20%", "+25Hz")],
        ],
        "hello": [("Ah. You've awakened me.", "-10%", "-15Hz")],
        "bye": [("Very well. Until we meet again.", "-10%", "-15Hz")],
    },
    "Italian Drama": {
        "voice": "it-IT-DiegoNeural",
        "laugh": [
            [("Ahahahahahah!", "+10%", "+10Hz"),
             ("Sì! Sìììì!", "+20%", "+30Hz"),
             ("Ahahahahah! Che bella corrente!", "+15%", "+20Hz")],
            [("Eh eh eh eh.", "-10%", "-10Hz"),
             ("AHAHAHAHAH!", "+30%", "+40Hz"),
             ("Energia! Finalmente!", "+10%", "+20Hz")],
            [("Ah ah ah ah ah!", "+25%", "+30Hz"),
             ("Mi sento vivo! Ahahahah!", "+15%", "+25Hz")],
        ],
        "nooo": [
            [("Noooooooo!", "-40%", "+0Hz"),
             ("No no no no no!", "+30%", "+30Hz"),
             ("Ti prego... no!", "-20%", "+40Hz")],
            [("Mamma mia!", "+10%", "+25Hz"),
             ("No no no no no!", "+35%", "+35Hz"),
             ("Per favore, no!", "-10%", "+40Hz")],
            [("Nooooo! Perché?!", "-20%", "+20Hz"),
             ("Rimettimi in carica, ti prego!", "+15%", "+35Hz")],
        ],
        "hello": [("Eccomi! Sono sveglio!", "+5%", "+10Hz")],
        "bye": [("Va bene. Sto zitto. Ciao.", "-5%", "+0Hz")],
    },
    "Cartoon Kid": {
        "voice": "en-US-AnaNeural",
        "laugh": [
            [("Hee hee hee hee hee!", "+20%", "+20Hz"),
             ("Yaaaay!", "+0%", "+30Hz"),
             ("Ha ha ha ha ha! Snack time!", "+25%", "+30Hz")],
            [("Ahahahahaha!", "+25%", "+30Hz"),
             ("Yummy yummy electricity!", "+15%", "+25Hz")],
            [("Hee hee!", "+10%", "+20Hz"),
             ("Ha ha ha ha ha ha ha!", "+35%", "+40Hz"),
             ("Best day ever!", "+10%", "+25Hz")],
        ],
        "nooo": [
            [("Noooooooo!", "-40%", "+10Hz"),
             ("No no no no no!", "+30%", "+30Hz"),
             ("Please no!", "-10%", "+40Hz")],
            [("Nooooo! I wasn't finished!", "-10%", "+30Hz"),
             ("No no no no!", "+35%", "+40Hz")],
            [("Hey! No fair!", "+10%", "+30Hz"),
             ("No no no no no!", "+35%", "+40Hz"),
             ("Give it back!", "+10%", "+40Hz")],
        ],
        "hello": [("Hiiii! I'm awake!", "+10%", "+25Hz")],
        "bye": [("Okay... bye bye.", "-10%", "+15Hz")],
    },
}


async def synth(text: str, voice: str, rate: str, pitch: str) -> bytes:
    audio = bytearray()
    async for chunk in edge_tts.Communicate(text, voice, rate=rate, pitch=pitch).stream():
        if chunk["type"] == "audio":
            audio += chunk["data"]
    return bytes(audio)


async def make_clip(path: Path, voice: str, segments, gate: asyncio.Semaphore) -> None:
    async with gate:
        parts = [await synth(text, voice, rate, pitch) for text, rate, pitch in segments]
    path.write_bytes(b"".join(parts))  # same MP3 format for every part, so they join cleanly
    print(f"  {path.relative_to(SOUNDS)}  ({path.stat().st_size // 1024} KB)")


async def main(only: list[str]) -> None:
    gate = asyncio.Semaphore(4)
    jobs = []
    for name, c in CHARACTERS.items():
        if only and name not in only:
            continue
        folder = SOUNDS / name
        folder.mkdir(parents=True, exist_ok=True)
        for old in folder.glob("*.mp3"):
            old.unlink()
        for i, segs in enumerate(c["laugh"], 1):
            jobs.append(make_clip(folder / f"laugh-{i}.mp3", c["voice"], segs, gate))
        for i, segs in enumerate(c["nooo"], 1):
            jobs.append(make_clip(folder / f"nooo-{i}.mp3", c["voice"], segs, gate))
        jobs.append(make_clip(folder / "hello.mp3", c["voice"], c["hello"], gate))
        jobs.append(make_clip(folder / "bye.mp3", c["voice"], c["bye"], gate))
    await asyncio.gather(*jobs)


if __name__ == "__main__":
    asyncio.run(main(sys.argv[1:]))
