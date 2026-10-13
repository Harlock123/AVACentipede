#!/usr/bin/env python3
"""Synthesises AVACentipede's sound effects and tunes into ../src/AVACentipede/Sounds as 16-bit mono WAV.

Nothing is sampled from any arcade machine: every effect is built here from square, triangle and
noise voices, and the tunes are new compositions. Run it from anywhere:
    python3 tools/make_sounds.py
"""
import math
import os
import random
import struct
import wave

RATE = 22050
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "src", "AVACentipede", "Sounds")
RNG = random.Random(1980)


def write(name, samples, volume=0.7):
    peak = max(1e-9, max(abs(s) for s in samples))
    with wave.open(os.path.join(OUT, name), "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(RATE)
        w.writeframes(b"".join(struct.pack("<h", int(s / peak * volume * 32767)) for s in samples))


def voice(kind, phase):
    p = phase % 1.0
    if kind == "square":
        return 1.0 if p < 0.5 else -1.0
    if kind == "pulse":
        return 1.0 if p < 0.25 else -1.0
    if kind == "tri":
        return 4 * p - 1 if p < 0.5 else 3 - 4 * p
    if kind == "saw":
        return 2 * p - 1
    return RNG.uniform(-1, 1)


def tone(seconds, f0, f1=None, kind="square", attack=0.004, release=1.0, vibrato=0.0, vib_rate=8.0, gain=1.0):
    """A note gliding from f0 to f1 (exponentially), with a short attack and a shaped decay."""
    n = int(RATE * seconds)
    f1 = f1 or f0
    out, phase, held = [], 0.0, 0.0
    for i in range(n):
        t = i / RATE
        f = f0 * (f1 / f0) ** (i / max(1, n - 1))
        if vibrato:
            f *= 1 + vibrato * math.sin(2 * math.pi * vib_rate * t)
        phase += f / RATE
        if kind == "noise":
            # Noise re-sampled at the voice's rate: a lower 'pitch' sounds rougher.
            if int(phase) != int(phase - f / RATE):
                held = RNG.uniform(-1, 1)
            s = held
        else:
            s = voice(kind, phase)
        env = min(1.0, t / attack) * (1 - i / n) ** release
        out.append(s * env * gain)
    return out


def silence(seconds):
    return [0.0] * int(RATE * seconds)


def cat(*parts):
    out = []
    for p in parts:
        out.extend(p)
    return out


def mix(*parts):
    n = max(len(p) for p in parts)
    out = [0.0] * n
    for p in parts:
        for i, v in enumerate(p):
            out[i] += v
    return out


NOTES = {"C": 0, "D": 2, "E": 4, "F": 5, "G": 7, "A": 9, "B": 11}


def hz(note):
    n = NOTES[note[0]]
    rest = note[1:]
    while rest and rest[0] in "#b":
        n += 1 if rest[0] == "#" else -1
        rest = rest[1:]
    return 440.0 * 2 ** ((12 * (int(rest) + 1) + n - 69) / 12)


def tune(bpm, lead, bass, kind="pulse"):
    """Two voices: a lead and a bass line, as (note, beats) lists; '-' is a rest."""
    beat = 60.0 / bpm

    def line(notes, k, gain, release):
        out = []
        for note, beats in notes:
            d = beats * beat
            out.extend(silence(d) if note == "-" else tone(d, hz(note), kind=k, release=release, gain=gain))
        return out

    return mix(line(lead, kind, 0.55, 0.6), line(bass, "tri", 0.6, 0.3))


def main():
    os.makedirs(OUT, exist_ok=True)
    # The shot: a short, bright zip (it fires a lot, so it's quiet and quick).
    write("Fire.wav", tone(0.06, 2400, 900, "square", release=1.8), 0.3)
    write("MushroomHit.wav", tone(0.04, 700, 500, "pulse", release=1.5), 0.3)
    write("MushroomGone.wav", cat(tone(0.04, 900, 600, "pulse"), tone(0.08, 500, 200, "noise", release=1.2)), 0.45)
    write("SegmentHit.wav", cat(tone(0.05, 1300, 700, "square"), tone(0.12, 600, 120, "noise", release=1.3)), 0.5)
    write("HeadHit.wav", cat(tone(0.05, 1800, 1100, "square"), tone(0.06, 1100, 1600, "square"), tone(0.14, 700, 120, "noise", release=1.2)), 0.55)
    # The flea's falling whistle.
    write("Flea.wav", tone(0.9, 2600, 300, "tri", attack=0.02, release=0.5, vibrato=0.02, vib_rate=14), 0.4)
    write("FleaHit.wav", cat(tone(0.06, 1600, 2200, "pulse"), tone(0.2, 900, 150, "noise", release=1.2)), 0.5)
    # The spider's skittering approach.
    write("Spider.wav", cat(*[tone(0.05, 300 + 120 * (i % 3), 200 + 120 * (i % 3), "square", release=0.6, gain=0.7) for i in range(10)]), 0.35)
    write("SpiderDie.wav", cat(tone(0.08, 1400, 2000, "square"), tone(0.35, 1000, 80, "noise", release=0.9)), 0.6)
    write("Scorpion.wav", cat(*[tone(0.12, 180 if i % 2 == 0 else 240, kind="saw", release=0.5, gain=0.6) for i in range(6)]), 0.35)
    write("ScorpionDie.wav", cat(tone(0.08, 300, 900, "saw"), tone(0.4, 900, 60, "noise", release=0.9)), 0.6)
    write("PlayerDie.wav", cat(*[tone(0.08, 1200 - i * 90, 1100 - i * 90, "square", release=0.5) for i in range(10)], tone(0.6, 400, 40, "noise", release=0.8)), 0.65)
    write("Restore.wav", tone(0.05, 1500, 1900, "pulse", release=0.8), 0.3)
    write("ExtraLife.wav", cat(*[tone(0.06, hz(n), kind="square", release=0.4) for n in ["E6", "G6", "B6", "E7"] * 3]), 0.45)

    # The centipede's march: a dark, steady bass beat that loops while it crawls.
    beat = []
    for k, n in enumerate(["E2", "E2", "G2", "E2", "D2", "D2", "F#2", "D2"]):
        beat.extend(tone(0.18, hz(n), kind="tri", attack=0.003, release=1.4, gain=0.9))
        beat.extend(silence(0.02))
    write("Beat.wav", beat, 0.5)

    # The title tune: a busy, scurrying theme in E minor (new), 12 bars, loops.
    run = [("E5", 0.5), ("G5", 0.5), ("B5", 0.5), ("G5", 0.5), ("E5", 0.5), ("F#5", 0.5), ("G5", 1)]
    write("TitleTune.wav", tune(144,
        run + [("A5", 0.5), ("G5", 0.5), ("F#5", 0.5), ("E5", 0.5), ("D5", 1), ("B4", 1)]
        + run + [("A5", 0.5), ("B5", 0.5), ("C6", 0.5), ("B5", 0.5), ("A5", 1), ("G5", 1)]
        + [("C6", 1), ("B5", 0.5), ("A5", 0.5), ("G5", 1), ("E5", 1), ("A5", 1), ("G5", 0.5), ("F#5", 0.5), ("E5", 2),
           ("D5", 0.5), ("E5", 0.5), ("F#5", 0.5), ("G5", 0.5), ("A5", 1), ("F#5", 1), ("G5", 1), ("F#5", 0.5), ("D#5", 0.5), ("E5", 2)]
        + run + [("A5", 0.5), ("G5", 0.5), ("F#5", 0.5), ("E5", 0.5), ("D5", 1), ("B4", 1)]
        + [("E5", 0.5), ("D#5", 0.5), ("E5", 0.5), ("F#5", 0.5), ("G5", 1), ("B5", 1), ("A5", 1), ("F#5", 0.5), ("D#5", 0.5), ("E5", 2)],
        [(n, 0.5) for bar in ["E3", "D3", "E3", "A2", "C3", "B2", "D3", "B2", "E3", "D3", "B2", "E3"]
         for n in [bar, bar.replace("3", "4") if "3" in bar else bar.replace("2", "3")] * 4]), 0.6)
    # A new wave: a quick rising call.
    write("WaveStart.wav", tune(176,
        [("B4", 0.5), ("E5", 0.5), ("G5", 0.5), ("B5", 0.5), ("E6", 1.5), ("-", 0.5)],
        [("E3", 1), ("B2", 1), ("E3", 1.5), ("-", 0.5)]), 0.6)
    write("GameOver.wav", tune(92,
        [("B4", 1), ("A4", 1), ("G4", 1), ("F#4", 1), ("E4", 2), ("B3", 2)],
        [("E3", 2), ("C3", 2), ("B2", 2), ("E2", 2)], kind="tri"), 0.65)
    print("sounds written to", os.path.normpath(OUT))


if __name__ == "__main__":
    main()
