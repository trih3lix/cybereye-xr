# Generates the CyberEye cyberpunk app icon (neon camera-aperture eye + targeting HUD).
# Renders at supersample then downsamples with LANCZOS for clean edges. Outputs:
#   full (opaque, legacy icon), fg (transparent eye, adaptive foreground), bg (adaptive background).
import math, os
from PIL import Image, ImageDraw, ImageFilter, ImageChops

SS = 3
S = 1024 * SS
CX = CY = S // 2
OUT = r"C:\Users\jslade\CyberEyeXR\Assets\CyberEye\Icon"
os.makedirs(OUT, exist_ok=True)

CYAN = (0, 240, 255)
MAG  = (255, 45, 214)
WHITE= (225, 255, 255)
BG0  = (6, 8, 16)     # center
BG1  = (18, 12, 34)   # edge (violet-black)

def blank(): return Image.new("RGBA", (S, S), (0, 0, 0, 0))

def make_bg():
    img = Image.new("RGBA", (S, S), BG1 + (255,))
    d = ImageDraw.Draw(img)
    steps = 220
    for i in range(steps, 0, -1):
        r = int(S/2 * i/steps)
        t = i/steps
        c = tuple(int(BG1[k]*t + BG0[k]*(1-t)) for k in range(3))
        d.ellipse([CX-r, CY-r, CX+r, CY+r], fill=c + (255,))
    # scanlines + faint grid
    ov = blank(); do = ImageDraw.Draw(ov)
    for y in range(0, S, 12*SS):
        do.line([(0, y), (S, y)], fill=(0, 240, 255, 12), width=SS)
    for x in range(0, S, 96*SS):
        do.line([(x, 0), (x, S)], fill=(0, 240, 255, 8), width=SS)
    for y in range(0, S, 96*SS):
        do.line([(0, y), (S, y)], fill=(0, 240, 255, 8), width=SS)
    return Image.alpha_composite(img, ov)

def draw_eye(scale=1.0, with_brackets=True):
    """Draw the neon eye onto a transparent layer. scale shrinks toward center (adaptive safe zone)."""
    layer = blank(); d = ImageDraw.Draw(layer)
    lw = max(SS, int(S * 0.011 * scale))
    def E(cx, cy, rx, ry=None, **kw):
        if ry is None: ry = rx
        d.ellipse([cx-rx, cy-ry, cx+rx, cy+ry], **kw)

    tr  = int(S*0.44*scale)   # outer tech ring
    eyeW= int(S*0.40*scale); eyeH = int(S*0.235*scale)
    ir  = int(S*0.165*scale)  # iris outer (magenta)
    ir2 = int(S*0.122*scale)  # iris inner (cyan)
    pr  = int(S*0.052*scale)  # pupil

    # outer segmented tech ring
    segs = 24
    for i in range(segs):
        a0 = 360*i/segs; a1 = a0 + (360/segs)*0.62
        d.arc([CX-tr, CY-tr, CX+tr, CY+tr], a0, a1, fill=CYAN + (220,), width=int(lw*0.85))
    # eye lens outline (almond via wide ellipse)
    E(CX, CY, eyeW, eyeH, outline=CYAN + (255,), width=lw)
    E(CX, CY, int(eyeW*0.98), int(eyeH*0.96), outline=(0,240,255,90), width=max(SS,lw//2))
    # iris reticle ticks
    for i in range(48):
        a = 2*math.pi*i/48
        r1 = ir*1.03; r2 = ir*(1.16 if i % 4 == 0 else 1.08)
        d.line([(CX+r1*math.cos(a), CY+r1*math.sin(a)), (CX+r2*math.cos(a), CY+r2*math.sin(a))],
               fill=CYAN + (200,), width=SS*2)
    # iris rings
    E(CX, CY, ir,  outline=MAG + (255,),  width=lw)
    E(CX, CY, ir2, outline=CYAN + (255,), width=int(lw*0.75))
    # camera aperture blades
    blades = 9
    for i in range(blades):
        a = 2*math.pi*i/blades
        x1 = CX + ir2*0.42*math.cos(a);        y1 = CY + ir2*0.42*math.sin(a)
        x2 = CX + ir2*0.99*math.cos(a+0.62);   y2 = CY + ir2*0.99*math.sin(a+0.62)
        d.line([(x1, y1), (x2, y2)], fill=CYAN + (230,), width=int(lw*0.6))
    # pupil core + hot center
    E(CX, CY, pr, fill=CYAN + (255,))
    E(CX, CY, int(pr*0.5), fill=WHITE + (255,))
    # HUD corner brackets (skip for adaptive fg)
    if with_brackets:
        m = int(S*0.055); L = int(S*0.11); bw = int(S*0.011)
        for (ox, oy, dx, dy) in [(m,m,1,1),(S-m,m,-1,1),(m,S-m,1,-1),(S-m,S-m,-1,-1)]:
            d.line([(ox, oy), (ox+dx*L, oy)], fill=MAG + (255,), width=bw)
            d.line([(ox, oy), (ox, oy+dy*L)], fill=MAG + (255,), width=bw)
    return layer

def neon(layer):
    """Return layer with additive neon glow underneath."""
    out = blank()
    for rad, mul in [(S*0.020, 1), (S*0.008, 1)]:
        g = layer.copy().filter(ImageFilter.GaussianBlur(rad))
        out = ImageChops.add(out, g)
    return Image.alpha_composite(out, layer)

def save(img, name, size=1024):
    img.convert("RGBA").resize((size, size), Image.LANCZOS).save(os.path.join(OUT, name))

bg = make_bg()
eye_full = neon(draw_eye(1.0, with_brackets=True))
eye_fg   = neon(draw_eye(0.66, with_brackets=False))   # adaptive foreground, inside safe zone

full = Image.alpha_composite(bg, eye_full)
save(full,   "cybereye_icon.png")     # legacy / round source
save(eye_fg, "cybereye_fg.png")       # adaptive foreground (transparent)
save(bg,     "cybereye_bg.png")       # adaptive background
print("icon written to", OUT)
