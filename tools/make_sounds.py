#!/usr/bin/env python3
"""Synthesises AVAQuantum's sound effects and title tune into ../src/AVAQuantum/Sounds as 16-bit mono WAV.

Nothing is sampled from any game: every effect is built here from square, saw, triangle, sine and noise voices, the
way the sound chips of the early eighties made them. The title tune is a new composition. Run it from anywhere:
    python3 tools/make_sounds.py
"""
import math
import os
import random
import struct
import wave


RATE = 22050
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "src", "AVAQuantum", "Sounds")
RNG = random.Random(1982)


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
    if kind == "sine":
        return math.sin(2 * math.pi * p)
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


def gain(part, g):
    return [v * g for v in part]


def lowpass(samples, cutoff):
    a = 1 - math.exp(-2 * math.pi * cutoff / RATE)
    out, y = [], 0.0
    for v in samples:
        y += a * (v - y)
        out.append(y)
    return out


def echoed(dry, taps=((0.09, 0.3), (0.19, 0.15))):
    """The ring of a big arcade cabinet's loudspeaker."""
    out = dry + [0.0] * int(RATE * 0.4)
    for delay, g in taps:
        k = int(RATE * delay)
        for i, v in enumerate(dry):
            out[i + k] += v * g
    return out


NOTES = {"C": 0, "D": 2, "E": 4, "F": 5, "G": 7, "A": 9, "B": 11}


def hz(note):
    n = NOTES[note[0]]
    rest = note[1:]
    while rest and rest[0] in "#b":
        n += 1 if rest[0] == "#" else -1
        rest = rest[1:]
    return 440.0 * 2 ** ((12 * (int(rest) + 1) + n - 69) / 12)


def seq(bpm, notes, kind="pulse", g=0.55, release=0.6, vibrato=0.0, legato=1.0):
    """A line of (note, beats); '-' is a rest. Notes can be cut short (legato below 1) for a detached feel."""
    beat = 60.0 / bpm
    out = []
    for note, beats in notes:
        d = beats * beat
        if note == "-":
            out.extend(silence(d))
            continue
        sounding = tone(d * legato, hz(note), kind=kind, release=release, gain=g, vibrato=vibrato, vib_rate=6)
        out.extend(sounding + silence(d - len(sounding) / RATE))
    return out


def fit(part, seconds):
    n = int(RATE * seconds)
    return (part + [0.0] * n)[:n]


def drums(bpm, beats, pattern):
    """A drum track: pattern is a string per beat - k kick, s snare, h hat, '.' nothing."""
    beat = 60.0 / bpm
    out = []
    for i in range(beats):
        c = pattern[i % len(pattern)]
        if c == "k":
            hit = tone(beat, 130, 45, "sine", attack=0.002, release=2.2, gain=0.9)
        elif c == "s":
            hit = mix(tone(beat, 2200, 900, "noise", attack=0.001, release=3.0, gain=0.45), tone(beat * 0.5, 220, 160, "tri", release=2, gain=0.3))
        elif c == "h":
            hit = tone(beat * 0.5, 7000, 6500, "noise", attack=0.001, release=4, gain=0.16)
        else:
            hit = []
        out.extend(fit(hit, beat))
    return out


# ───────────────────────────── the voice ─────────────────────────────

# ───────────────────────────── the title tune ─────────────────────────────

def title_tune():
    """A slow, glittering arpeggio over a drone in A minor: the atom humming to itself (a new composition)."""
    bpm = 132
    chords = [("A3", ["A4", "C5", "E5", "A5"]), ("F3", ["F4", "A4", "C5", "F5"]), ("D3", ["D4", "F4", "A4", "D5"]), ("E3", ["E4", "G#4", "B4", "E5"])]
    arps, bass = [], []
    for root, notes in chords * 2:
        line = []
        for rep in range(2):
            order = notes + notes[::-1][1:3] if rep == 0 else notes[::-1] + notes[1:3]
            line += [(n, 0.5) for n in order]
        arps.append(seq(bpm, line, kind="tri", g=0.42, release=1.4))
        bass.append(seq(bpm, [(root, 4), (root, 2), (root, 2)], kind="saw", g=0.22, release=0.4, vibrato=0.004))
    melody = seq(bpm, [("E5", 3), ("D5", 1), ("C5", 2), ("B4", 2), ("A4", 4), ("C5", 4), ("D5", 3), ("F5", 1), ("E5", 2), ("D5", 2), ("B4", 4), ("G#4", 4),
                       ("A5", 3), ("G5", 1), ("F5", 2), ("E5", 2), ("C5", 4), ("A4", 4), ("D5", 3), ("C5", 1), ("B4", 2), ("C5", 2), ("B4", 4), ("E5", 4)],
                 kind="pulse", g=0.3, release=0.5, vibrato=0.008, legato=0.9)
    return echoed(mix(cat(*arps), cat(*bass), melody), taps=((0.18, 0.3), (0.36, 0.15)))


def main():
    os.makedirs(OUT, exist_ok=True)
    # A loop closing: a bright upward chirp and a ring.
    write("Catch.wav", mix(tone(0.18, 500, 2400, "square", attack=0.002, release=0.8, gain=0.5), tone(0.35, 2400, 2350, "sine", attack=0.01, release=1.6, gain=0.5)), 0.4)
    # Several at once: a quick rising arpeggio with a shimmer.
    write("MultiCatch.wav", cat(*[tone(0.05, hz(n), kind="square", release=0.4, gain=0.6) for n in ["C6", "E6", "G6", "C7", "E7", "G7", "C8"]],
                                tone(0.45, hz("C7"), kind="tri", vibrato=0.02, vib_rate=14, release=1.2)), 0.38)
    # A loop round nothing: a soft blip.
    write("Loop.wav", tone(0.1, 900, 1300, "tri", attack=0.002, release=1.2), 0.22)
    write("Split.wav", mix(tone(0.25, 1600, 300, "saw", attack=0.001, release=1.2, gain=0.6), tone(0.25, 1700, 320, "saw", attack=0.001, release=1.2, gain=0.6)), 0.32)
    # The atom breaking: a deep boom with a swirl of falling tones.
    write("AtomBreak.wav", mix(lowpass(tone(1.6, 2400, 80, "noise", attack=0.002, release=0.7), 1800), tone(1.4, 160, 30, "sine", attack=0.002, release=0.8, gain=0.9),
                               cat(*[tone(0.12, 2000 - i * 140, 1500 - i * 120, "square", release=1, gain=0.25) for i in range(10)])), 0.55)
    write("AtomForm.wav", cat(*[tone(0.06, 300 + i * 160, kind="tri", release=0.5, gain=0.5) for i in range(10)], tone(0.3, 1900, kind="sine", release=1.5, gain=0.4)), 0.3)
    # The probe hit: a crackling zap that sinks away.
    write("Zap.wav", cat(mix(tone(0.5, 3000, 60, "saw", attack=0.001, release=0.6, vibrato=0.3, vib_rate=40), lowpass(tone(0.9, 4000, 200, "noise", attack=0.001, release=0.9), 3000)),
                         tone(0.5, 120, 40, "sine", release=1.5, gain=0.5)), 0.55)
    # A kitten eats the trail: a little chip-voiced "mew", rising then falling.
    write("Mew.wav", cat(tone(0.09, 700, 1400, "pulse", attack=0.01, release=0.2, vibrato=0.02, vib_rate=20, gain=0.6),
                         tone(0.22, 1400, 600, "pulse", attack=0.002, release=1.0, vibrato=0.03, vib_rate=18, gain=0.6)), 0.3)
    write("Charge.wav", tone(0.35, 200, 1600, "square", attack=0.02, release=0.3, vibrato=0.05, vib_rate=25), 0.16)
    write("Emerge.wav", tone(0.16, 300, 900, "sine", attack=0.005, release=1.0), 0.22)
    write("ExtraLife.wav", cat(*[tone(0.07, hz(n), kind="pulse", release=0.3) for n in ["A5", "C#6", "E6", "A6", "C#7", "E7"]], tone(0.3, hz("A6"), kind="pulse", release=0.6)), 0.3)
    write("WaveStart.wav", cat(*[tone(0.09, hz(n), kind="tri", release=0.5, gain=0.7) for n in ["A4", "E5", "A5"]], tone(0.4, hz("E6"), kind="tri", vibrato=0.01, release=1.2)), 0.32)
    write("WaveClear.wav", mix(seq(150, [("A5", 0.5), ("C6", 0.5), ("E6", 0.5), ("A6", 0.5), ("-", 0.25), ("G#6", 0.25), ("A6", 2.5)], kind="pulse", g=0.5),
                               seq(150, [("A3", 2), ("E3", 1), ("A3", 2.5)], kind="tri", g=0.6)), 0.34)
    write("GameOver.wav", mix(seq(72, [("E5", 1), ("C5", 1), ("A4", 1), ("G#4", 1), ("A4", 4)], kind="tri", g=0.5),
                              seq(72, [("A2", 4), ("E2", 2), ("A2", 2)], kind="saw", g=0.25, release=0.3)), 0.34)
    write("TitleTune.wav", title_tune(), 0.32)
    print("wrote", len([f for f in os.listdir(OUT) if f.endswith(".wav")]), "sounds to", os.path.normpath(OUT))


if __name__ == "__main__":
    main()
