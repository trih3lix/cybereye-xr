# Synthesizes original cyberpunk SFX + ambiance (CC0 / our own) for CyberEye. numpy -> 16-bit mono WAV.
import numpy as np, wave, os
SR = 44100
REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))  # scripts/.. = repo root
OUT = os.path.join(REPO, "Assets", "CyberEye", "Audio")
os.makedirs(OUT, exist_ok=True)

def save(name, sig):
    sig = np.clip(sig, -1, 1)
    pcm = (sig * 32767).astype('<i2')
    with wave.open(os.path.join(OUT, name), 'w') as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(SR); w.writeframes(pcm.tobytes())

def t(d): return np.linspace(0, d, int(SR * d), endpoint=False)

def env(sig, a=0.01, r=0.1):
    n = len(sig); e = np.ones(n); ai = int(a * SR); ri = int(r * SR)
    if ai > 0: e[:ai] = np.linspace(0, 1, ai)
    if ri > 0: e[-ri:] = np.linspace(1, 0, ri)
    return sig * e

# --- ambiance: seamless 8s dark drone (LFO cycles a whole number of times over the loop) ---
d = 8.0; tt = t(d)
amb  = 0.25*np.sin(2*np.pi*55*tt) + 0.18*np.sin(2*np.pi*82.5*tt) + 0.12*np.sin(2*np.pi*110.3*tt)
amb *= (0.6 + 0.4*np.sin(2*np.pi*(1/d)*tt))                 # 1 cycle -> seamless loop
amb += 0.05*np.sin(2*np.pi*440*tt)*(0.5+0.5*np.sin(2*np.pi*(2/d)*tt))
save("ambiance.wav", amb * 0.7)

# --- target lock: two-tone chirp ---
tt = t(0.16); save("lock.wav", env(0.5*np.sin(2*np.pi*880*tt) + 0.4*np.sin(2*np.pi*1320*(1+1.5*tt)*tt), 0.004, 0.08))
# --- scan sweep: rising ---
tt = t(0.4);  save("scan.wav",  env(0.4*np.sin(2*np.pi*(400 + 2000*(tt/0.4))*tt), 0.01, 0.15))
# --- glitch: gated noise ---
tt = t(0.2);  save("glitch.wav", env(0.35*np.random.uniform(-1,1,len(tt))*((np.sin(2*np.pi*45*tt)>0).astype(float)), 0.001, 0.05))
# --- alert: descending minor ---
tt = t(0.5); f = 660 - 330*(tt/0.5); save("alert.wav", env(0.4*np.sin(2*np.pi*f*tt) + 0.18*np.sin(2*np.pi*2*f*tt), 0.01, 0.22))
print("audio written to", OUT)
