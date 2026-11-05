#!/usr/bin/env python3
"""apply.py [--revert] - route every default `new Random()` of the product through SweepHook (scratch, never committed).

Q1023 (docs/TOOLING.md, "A test's flake found by setting its generators from outside"). Run from the repo root.
Copies SweepHook.cs into source/OpenTPW/ and ZSweepScratch.cs into source/OpenTPW.Tests/, and edits the 17 default
generators the park's audio, weather, sea, staff and peeps make (a line that has moved stops the script with an
AssertionError, so an edit never half applies). `--revert` restores the edited files with git and deletes the two copies.
Build, then run.sh. Commit nothing while it is applied: the edits are the sweep's, not the product's.
"""
import os, shutil, subprocess, sys

HERE = os.path.dirname(os.path.abspath(__file__))
P = "source/OpenTPW/"
EDITS = [
	("World/Park/ParkAudio.cs", [(f"Random {n} = new();", f"Random {n} = SweepHook.Make( \"{n}\" );") for n in
		("_musicRandom", "_crowdRandom", "_rainRandom", "_staffRandom", "_screamRandom", "_followingRandom")]),
	("World/Park/ParkAudioScripts.cs", [("private Random _scriptRandom = new();", "private Random _scriptRandom = SweepHook.Make( \"script\" );")]),
	("World/Park/ParkCarSounds.cs", [("Random _random = new();", "Random _random = SweepHook.Make( \"CarSounds\" );")]),
	("World/Park/ParkScreams.cs", [("_random = random ?? new Random();", "_random = random ?? SweepHook.Make( \"Screams\" );")]),
	("Audio/SoundCategory.cs", [("= static () => new Random();", "= static () => SweepHook.Make( \"SoundCategory\" );")]),
	("World/Lobby/LobbyAudio.cs", [("Random _random = new();", "Random _random = SweepHook.Make( \"LobbyAudio\" );")]),
	("World/Advisor/Advisor.cs", [("Random _random = new();", "Random _random = SweepHook.Make( \"Advisor\" );")]),
	("World/Park/ParkWeather.cs", [("Random _rng = new();", "Random _rng = SweepHook.Make( \"ParkWeather\" );")]),
	("World/Park/ParkSea.cs", [("SmallTextures( textureQuality ), new Random() )", "SmallTextures( textureQuality ), SweepHook.Make( \"ParkSea\" ) )")]),
	("World/Park/StaffBehaviour.cs", [("_random = random ?? new Random();", "_random = random ?? SweepHook.Make( \"StaffBehaviour\" );")]),
	("World/Park/ParkPeople.cs", [("_arrivalRandom = random ?? new Random();", "_arrivalRandom = random ?? SweepHook.Make( \"ArrivalRandom\" );"),
		("_rideRandom = rideRandom ?? new Random();", "_rideRandom = rideRandom ?? SweepHook.Make( \"RideRandom\" );")]),
	("World/Park/PeepBehaviour.cs", [("_random = random ?? new Random();", "_random = random ?? SweepHook.Make( \"PeepBehaviour\" );")]),
]
COPIES = [("SweepHook.cs", P + "SweepHook.cs"), ("ZSweepScratch.cs", "source/OpenTPW.Tests/ZSweepScratch.cs")]

if "--revert" in sys.argv:
	subprocess.check_call(["git", "checkout", "--"] + [P + f for f, _ in EDITS])
	for _, dest in COPIES:
		if os.path.exists(dest):
			os.remove(dest)
	sys.exit(0)

texts = {}
for f, subs in EDITS:
	text = open(P + f).read()
	for old, new in subs:
		assert old in text, f"{f}: {old!r} not found"
		text = text.replace(old, new)
	texts[f] = text
for f, text in texts.items():
	open(P + f, "w").write(text)
for src, dest in COPIES:
	shutil.copy(os.path.join(HERE, src), dest)
print( "applied: build, then run.sh" )
