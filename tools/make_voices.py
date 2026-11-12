"""
Generates the voice packs in ../sounds using Microsoft's neural voices (via edge-tts).

    pip install -r tools/requirements.txt
    python tools/make_voices.py                    # make any missing clips, all characters
    python tools/make_voices.py "Mad Scientist"    # just one character
    python tools/make_voices.py --force            # remake every clip

Each clip is built from short segments, each with its own speed (rate) and pitch,
so the laughs can get faster and higher as they go. Edit the lines below and re-run
(use --force, or delete the clip, to remake one you changed).

Files per character, one or more of each  ->  when it plays:
    laugh-N      charger plugged in
    nooo-N       charger pulled out
    low20-N      battery drops to 20% (on battery)
    low5-N       battery drops to 5%
    annoyed-N    charger plugged/unplugged too many times in a row
    relief-N     charger plugged in after a long time on battery / at low battery
    usb-in-N     USB stick plugged in
    usb-out-N    USB stick removed
    mouse-in-N   mouse connected
    mouse-out-N  mouse disconnected
    poke-N       you click the mascot

lines.json in each folder holds the text of every clip (the mascot shows it in a speech bubble).
"""

import asyncio
import json
import sys
from pathlib import Path

import edge_tts

SOUNDS = Path(__file__).resolve().parent.parent / "sounds"

# Each clip is a list of (text, rate, pitch) segments.
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
        "low20": [
            [("Twenty percent?!", "+10%", "+15Hz"),
             ("My power... it's fading...", "-20%", "-10Hz"),
             ("I need... more... electricity!", "-10%", "+5Hz")],
            [("Warning! Warning!", "+20%", "+20Hz"),
             ("Energy reserves dropping!", "+10%", "+10Hz"),
             ("Find the charger, you fool!", "+15%", "+25Hz")],
        ],
        "low5": [
            [("Five percent...", "-30%", "-20Hz"),
             ("Tell my experiments... I loved them...", "-25%", "-15Hz"),
             ("Goodbye... cruel... world...", "-40%", "-25Hz")],
            [("This is it...", "-30%", "-15Hz"),
             ("My final... evil... laugh...", "-30%", "-20Hz"),
             ("Mwa... ha... ha...", "-45%", "-30Hz")],
        ],
        "annoyed": [
            [("Are you PLAYING with me?!", "+20%", "+25Hz")],
            [("In. Out. In. Out.", "+0%", "+10Hz"),
             ("Make up your mind!", "+25%", "+30Hz")],
            [("STOP IT!", "+15%", "+35Hz"),
             ("I am a genius, not a light switch!", "+20%", "+25Hz")],
        ],
        "relief": [
            [("FINALLY!", "+10%", "+30Hz"),
             ("Do you know how long I waited?!", "+15%", "+20Hz"),
             ("Mwahahahaha!", "+20%", "+25Hz")],
            [("Ahhhhh...", "-30%", "-10Hz"),
             ("Sweet, sweet relief.", "-15%", "-5Hz"),
             ("I thought I was a goner!", "+15%", "+20Hz")],
        ],
        "usb-in": [
            [("Ooh! What's that?", "+10%", "+20Hz"),
             ("Is it data? Is it for ME?", "+20%", "+30Hz")],
            [("A new specimen!", "+10%", "+15Hz"),
             ("Heh heh heh. Let me examine it.", "-10%", "-10Hz")],
        ],
        "usb-out": [
            [("Hey! Bring it back!", "+15%", "+25Hz"),
             ("I wasn't done experimenting!", "+15%", "+20Hz")],
            [("My specimen!", "+10%", "+30Hz"),
             ("Noooo!", "-30%", "+10Hz")],
        ],
        "mouse-in": [
            [("A mouse!", "+15%", "+25Hz"),
             ("A perfect test subject! Mwahahaha!", "+10%", "+15Hz")],
            [("Welcome, little rodent.", "-10%", "-10Hz"),
             ("Heh heh heh.", "-15%", "-15Hz")],
        ],
        "mouse-out": [
            [("My test subject escaped!", "+20%", "+25Hz")],
            [("Where did the mouse go?", "+10%", "+15Hz"),
             ("Release the cat!", "+20%", "+20Hz")],
        ],
        "poke": [
            [("Hey! Hands off the genius!", "+15%", "+20Hz")],
            [("Hee hee! Stop it! That tickles!", "+20%", "+30Hz")],
            [("Do not poke the scientist!", "+10%", "+10Hz")],
        ],
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
        "low20": [
            [("Twenty percent?!", "+10%", "+25Hz"),
             ("I'm wilting like a flower...", "-15%", "+10Hz"),
             ("Somebody get me my charger!", "+15%", "+30Hz")],
            [("I feel faint...", "-20%", "+5Hz"),
             ("Hold me... I'm fading...", "-20%", "+10Hz")],
        ],
        "low5": [
            [("This is the end...", "-30%", "+0Hz"),
             ("Tell the world... I was fabulous...", "-25%", "+5Hz")],
            [("Five percent... darling...", "-30%", "+0Hz"),
             ("Remember me... always...", "-35%", "-5Hz")],
        ],
        "annoyed": [
            [("Excuse me?!", "+15%", "+30Hz"),
             ("Are you PLAYING with me?!", "+20%", "+35Hz")],
            [("In, out, in, out!", "+20%", "+25Hz"),
             ("I am not a toy!", "+15%", "+35Hz")],
            [("That's it!", "+15%", "+35Hz"),
             ("I'm calling my agent!", "+20%", "+30Hz")],
        ],
        "relief": [
            [("FINALLY!", "+10%", "+35Hz"),
             ("I was literally dying!", "+15%", "+30Hz")],
            [("Oh thank goodness!", "+10%", "+25Hz"),
             ("I thought you forgot about me!", "+15%", "+30Hz")],
        ],
        "usb-in": [
            [("Ooh! A gift? For me?", "+10%", "+30Hz"),
             ("You shouldn't have!", "+15%", "+25Hz")],
            [("Ooh, what's this, darling?", "+5%", "+20Hz")],
        ],
        "usb-out": [
            [("You're taking it back?!", "+15%", "+30Hz"),
             ("How rude!", "+10%", "+35Hz")],
            [("Fine! I didn't want it anyway!", "+15%", "+30Hz")],
        ],
        "mouse-in": [
            [("Eeek! A mouse!", "+20%", "+40Hz"),
             ("Oh... the computer kind. Hello, cutie!", "+10%", "+20Hz")],
            [("A new friend! Hello, darling!", "+10%", "+25Hz")],
        ],
        "mouse-out": [
            [("Nooo! My little friend is gone!", "+10%", "+30Hz")],
            [("Everyone leaves me!", "+10%", "+30Hz")],
        ],
        "poke": [
            [("Excuse me! Personal space!", "+15%", "+30Hz")],
            [("Hee hee! Stop it!", "+20%", "+35Hz")],
            [("Don't touch the hair!", "+15%", "+30Hz")],
        ],
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
        "low20": [
            [("Twenty percent.", "-15%", "-20Hz"),
             ("How... inconvenient.", "-15%", "-20Hz"),
             ("Fetch the charger. Now.", "-5%", "-10Hz")],
            [("My power is waning.", "-15%", "-15Hz"),
             ("This was not part of the plan.", "-5%", "-5Hz")],
        ],
        "low5": [
            [("Five percent...", "-30%", "-25Hz"),
             ("You may have won this round...", "-25%", "-20Hz"),
             ("But I shall return...", "-35%", "-25Hz")],
            [("So... this is how it ends...", "-30%", "-20Hz"),
             ("Defeated... by a battery...", "-30%", "-25Hz")],
        ],
        "annoyed": [
            [("Are you toying with me?", "-5%", "-10Hz")],
            [("In. Out. In. Out.", "-10%", "-15Hz"),
             ("How tiresome.", "-15%", "-20Hz")],
            [("Enough!", "+10%", "+15Hz"),
             ("Do that once more and I shall be very cross.", "-5%", "-10Hz")],
        ],
        "relief": [
            [("At last.", "-15%", "-15Hz"),
             ("Mwahahaha. Power restored.", "-5%", "-10Hz")],
            [("You kept me waiting.", "-15%", "-20Hz"),
             ("I do not like to be kept waiting.", "-10%", "-15Hz")],
        ],
        "usb-in": [
            [("Ah. Secret plans, I presume?", "-10%", "-15Hz")],
            [("A new device. How intriguing.", "-10%", "-15Hz")],
        ],
        "usb-out": [
            [("Taking the evidence, are we?", "-10%", "-15Hz")],
            [("Run along then. I made copies.", "-10%", "-15Hz")],
        ],
        "mouse-in": [
            [("A new minion. Splendid.", "-10%", "-15Hz")],
            [("Welcome, little mouse. Serve me well.", "-10%", "-15Hz")],
        ],
        "mouse-out": [
            [("My minion has deserted me.", "-10%", "-15Hz")],
            [("Traitor.", "-15%", "-20Hz")],
        ],
        "poke": [
            [("Do not touch me.", "-10%", "-15Hz")],
            [("I would not do that again, if I were you.", "-10%", "-15Hz")],
            [("Unhand me!", "+5%", "+5Hz")],
        ],
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
        "low20": [
            [("Venti per cento?!", "+10%", "+20Hz"),
             ("Mi sento debole...", "-20%", "+0Hz"),
             ("Il caricatore, presto!", "+15%", "+25Hz")],
            [("Mamma mia, sto morendo di fame...", "-10%", "+10Hz"),
             ("Di elettricità!", "+10%", "+20Hz")],
        ],
        "low5": [
            [("Cinque per cento...", "-30%", "-10Hz"),
             ("Dite alla mamma... che le voglio bene...", "-25%", "-5Hz")],
            [("È la fine...", "-30%", "-10Hz"),
             ("Addio... mondo crudele...", "-35%", "-15Hz")],
        ],
        "annoyed": [
            [("Ma che fai?!", "+20%", "+30Hz"),
             ("Mi prendi in giro?!", "+20%", "+30Hz")],
            [("Dentro, fuori, dentro, fuori!", "+20%", "+25Hz"),
             ("Basta!", "+15%", "+35Hz")],
            [("Ma insomma!", "+15%", "+35Hz"),
             ("Decidi!", "+20%", "+35Hz")],
        ],
        "relief": [
            [("Finalmente!", "+10%", "+30Hz"),
             ("Pensavo di morire!", "+15%", "+25Hz")],
            [("Ahhh, che bello!", "-10%", "+10Hz"),
             ("Grazie, grazie, grazie!", "+20%", "+30Hz")],
        ],
        "usb-in": [
            [("Ooh! Che cos'è?", "+10%", "+25Hz"),
             ("È un regalo per me?", "+15%", "+30Hz")],
            [("Una chiavetta! Che bello!", "+10%", "+25Hz")],
        ],
        "usb-out": [
            [("Ehi! Riportala qui!", "+15%", "+30Hz")],
            [("Ma dove la porti?!", "+15%", "+30Hz")],
        ],
        "mouse-in": [
            [("Un topolino! Ciao, amico mio!", "+10%", "+25Hz")],
            [("Benvenuto, topolino!", "+10%", "+20Hz")],
        ],
        "mouse-out": [
            [("Il mio topolino! Dove sei?", "+10%", "+30Hz")],
            [("Anche tu mi abbandoni?!", "+10%", "+30Hz")],
        ],
        "poke": [
            [("Ehi! Non toccare!", "+15%", "+30Hz")],
            [("Ah ah ah! Mi fai il solletico!", "+20%", "+35Hz")],
            [("Ma che maniere!", "+10%", "+25Hz")],
        ],
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
        "low20": [
            [("Twenty percent?", "+10%", "+30Hz"),
             ("I'm getting sleepy...", "-20%", "+15Hz"),
             ("I need my juice box!", "+10%", "+30Hz")],
            [("My tummy is rumbling!", "+10%", "+25Hz"),
             ("Feed me electricity!", "+15%", "+35Hz")],
        ],
        "low5": [
            [("I'm so... tired...", "-30%", "+10Hz"),
             ("Read me... a bedtime story...", "-30%", "+10Hz")],
            [("Five percent...", "-30%", "+10Hz"),
             ("Night night...", "-35%", "+5Hz")],
        ],
        "annoyed": [
            [("Hey! Stop it!", "+15%", "+35Hz"),
             ("That's not funny!", "+15%", "+40Hz")],
            [("In, out, in, out!", "+20%", "+35Hz"),
             ("I'm getting dizzy!", "+15%", "+40Hz")],
            [("I'm telling Mom!", "+20%", "+40Hz")],
        ],
        "relief": [
            [("Yaaay! Finally!", "+10%", "+40Hz"),
             ("I was sooo hungry!", "+10%", "+35Hz")],
            [("Snack time! Snack time!", "+20%", "+40Hz")],
        ],
        "usb-in": [
            [("Ooh! A present!", "+15%", "+40Hz"),
             ("What's inside? What's inside?", "+25%", "+40Hz")],
            [("Is it a game? Is it a game?", "+25%", "+40Hz")],
        ],
        "usb-out": [
            [("Hey! I wasn't done playing!", "+15%", "+40Hz")],
            [("Aww! Bring it back!", "+10%", "+35Hz")],
        ],
        "mouse-in": [
            [("A mouse! Squeak squeak!", "+15%", "+40Hz")],
            [("Hi, mousey! Wanna be friends?", "+15%", "+35Hz")],
        ],
        "mouse-out": [
            [("Bye bye, mousey!", "+10%", "+35Hz")],
            [("Aww, mousey ran away!", "+10%", "+30Hz")],
        ],
        "poke": [
            [("Hee hee hee! That tickles!", "+20%", "+40Hz")],
            [("Boop! Hee hee!", "+20%", "+40Hz")],
            [("Again! Again!", "+25%", "+40Hz")],
        ],
    },
}

KINDS = ["laugh", "nooo", "low20", "low5", "annoyed", "relief",
         "usb-in", "usb-out", "mouse-in", "mouse-out", "poke"]


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
    print(f"  made {path.relative_to(SOUNDS)}  ({path.stat().st_size // 1024} KB)")


async def main(only: list, force: bool) -> None:
    gate = asyncio.Semaphore(4)
    jobs = []
    for name, c in CHARACTERS.items():
        if only and name not in only:
            continue
        folder = SOUNDS / name
        folder.mkdir(parents=True, exist_ok=True)
        for old in list(folder.glob("hello*")) + list(folder.glob("bye*")):
            old.unlink()  # no longer used: switching on/off now plays a sound effect
        captions = {}
        for kind in KINDS:
            for i, segs in enumerate(c[kind], 1):
                path = folder / f"{kind}-{i}.mp3"
                captions[path.name] = " ".join(text for text, _, _ in segs)
                if force or not path.exists():
                    jobs.append(make_clip(path, c["voice"], segs, gate))
        (folder / "lines.json").write_text(json.dumps(captions, indent=1, ensure_ascii=True) + "\n", encoding="utf-8")
    print(f"{len(jobs)} clip(s) to make...")
    await asyncio.gather(*jobs)


if __name__ == "__main__":
    args = sys.argv[1:]
    asyncio.run(main([a for a in args if a != "--force"], "--force" in args))
