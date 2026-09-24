"""Original bubbling/inhalation foley; deterministic synthesis, no sampled recordings.
Requires numpy, scipy and ffmpeg. Starts 0.9s into the five-second held use.
"""
from pathlib import Path
import subprocess
import tempfile
import wave
import numpy as np
from scipy.signal import butter, sosfilt

RATE = 22050
DURATION = 3.7


def main():
    rng = np.random.default_rng(2026092402)
    t = np.arange(int(RATE * DURATION)) / RATE
    envelope = np.sin(np.pi * t / DURATION) ** .8
    noise = sosfilt(butter(3, [380, 2600], btype='bandpass', fs=RATE, output='sos'), rng.normal(0, 1, t.size))
    signal = .055 * noise * envelope
    # Irregular, overlapping resonant bubbles rise in pitch as each bubble releases.
    at = .08
    while at < DURATION - .12:
        duration = rng.uniform(.065, .15)
        u = np.arange(int(RATE * duration)) / RATE
        frequency = rng.uniform(140, 340)
        phase = 2 * np.pi * (frequency * u + frequency * 2.8 * u * u)
        bubble = (np.sin(phase) + .24 * np.sin(2.1 * phase)) * np.exp(-u * 32)
        bubble *= (1 - np.exp(-u * 650)) * rng.uniform(.25, .5)
        index = int(at * RATE)
        count = min(len(bubble), len(signal) - index)
        signal[index:index + count] += bubble[:count] * envelope[index:index + count]
        at += rng.uniform(.042, .10)
    signal *= .76 / max(np.max(np.abs(signal)), .01)
    pcm = np.int16(np.clip(signal, -1, 1) * 32767)
    output = Path(__file__).resolve().parents[1] / 'assets/vs-dope/sounds/player/bong-bubbles.ogg'
    output.parent.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory() as folder:
        wav = Path(folder) / 'bubbles.wav'
        with wave.open(str(wav), 'wb') as file:
            file.setnchannels(1)
            file.setsampwidth(2)
            file.setframerate(RATE)
            file.writeframes(pcm.tobytes())
        subprocess.run(['ffmpeg', '-v', 'error', '-y', '-i', str(wav), '-c:a', 'libvorbis', '-q:a', '5', str(output)], check=True)
    print(output)


if __name__ == '__main__':
    main()
