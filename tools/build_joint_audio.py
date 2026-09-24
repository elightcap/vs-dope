"""Original synthesized inhalation foley: filtered breath noise plus a quiet ember crackle.
Requires numpy, scipy, ffmpeg; no external recordings or samples.
"""
from pathlib import Path
import subprocess, tempfile, wave
import numpy as np
from scipy.signal import butter, sosfilt
rate=22050; duration=3.7
rng=np.random.default_rng(20260924);t=np.arange(int(rate*duration))/rate
noise=sosfilt(butter(3,[480,4300],btype='bandpass',fs=rate,output='sos'),rng.normal(0,1,t.size))
env=np.sin(np.pi*np.minimum(t/duration,1))**1.2
breath=noise*env*(.19+.035*np.sin(2*np.pi*5*t))
for at in [.18,.47,.93,1.28,2.15,2.9]:
 n=int(.014*rate);i=int(at*rate)
 breath[i:i+n]+=rng.normal(0,.065,n)*np.exp(-np.arange(n)/(rate*.0025))
waveform=np.int16(np.clip(breath,-.8,.8)*32767)
out=Path(__file__).resolve().parents[1]/'assets/vs-dope/sounds/player/joint-drag.ogg';out.parent.mkdir(parents=True,exist_ok=True)
with tempfile.TemporaryDirectory() as folder:
 wav=Path(folder)/'drag.wav'
 with wave.open(str(wav),'wb') as f:f.setnchannels(1);f.setsampwidth(2);f.setframerate(rate);f.writeframes(waveform.tobytes())
 subprocess.run(['ffmpeg','-v','error','-y','-i',str(wav),'-c:a','libvorbis','-q:a','5',str(out)],check=True)
print(out)
