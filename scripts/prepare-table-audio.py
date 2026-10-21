"""Derive quiet, dry tile contacts from the licensed physical recording.

Source: Macif, Domino pieces 2, Freesound 329100, CC0 1.0.
The input is the public high-quality preview, decoded to PCM with Blender aud.
No generated sine tones or pitch changes are mixed into the recording.
"""
import argparse
import json
import wave
from pathlib import Path
import numpy as np

ROOT = Path(__file__).resolve().parent.parent
parser = argparse.ArgumentParser()
parser.add_argument('--output', type=Path, default=ROOT/'Assets/Okey101/Art/Audio')
args = parser.parse_args()
args.output.mkdir(parents=True, exist_ok=True)
with wave.open(str(ROOT/'source-assets/audio/macif-329100-preview-decoded.wav'), 'rb') as source:
    rate = source.getframerate()
    assert source.getsampwidth() == 2 and rate == 44100
    signal = np.frombuffer(source.readframes(source.getnframes()), dtype='<i2').astype(np.float64)
    signal = signal.reshape(-1, source.getnchannels()).mean(axis=1)/32768

def write(path, data):
    assert np.isfinite(data).all() and np.max(np.abs(data)) < 1
    with wave.open(str(path), 'wb') as output:
        output.setparams((1, 2, rate, 0, 'NONE', 'not compressed'))
        output.writeframes(np.rint(data*32767).astype('<i2').tobytes())

clips = []
evidence = []
for name, contact, peak in [('draw', 1.50, .23), ('place', 2.69, .26), ('discard', 3.40, .30)]:
    clip = signal[int((contact-.035)*rate):int((contact+.245)*rate)].copy()
    # Remove room rumble and soften high-frequency impact; retain recorded body.
    frequencies = np.fft.rfftfreq(len(clip), 1/rate)
    response = (1-np.exp(-(frequencies/80)**2))/(1+(frequencies/6200)**4)
    clip = np.fft.irfft(np.fft.rfft(clip)*response, n=len(clip))
    attack, release = int(rate*.002), int(rate*.05)
    clip[:attack] *= np.linspace(0, 1, attack)
    clip[-release:] *= np.linspace(1, 0, release)
    clip *= peak/max(np.max(np.abs(clip)), 1e-9)
    write(args.output/(name+'.wav'), clip)
    clips.append(clip)
    evidence.append(dict(name=name, sourceContactSeconds=contact, durationSeconds=len(clip)/rate,
                         peak=float(np.max(np.abs(clip))), rms=float(np.sqrt(np.mean(clip**2)))))
preview = np.concatenate([np.concatenate([clip*.32, np.zeros(int(rate*.8))]) for clip in clips]*2)
write(args.output/'listening-preview.wav', preview)
(args.output/'audio-evidence.json').write_text(json.dumps(evidence, indent=2), encoding='utf-8')
print(json.dumps(evidence, indent=2))
