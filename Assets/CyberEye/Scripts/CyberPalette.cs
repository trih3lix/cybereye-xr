using UnityEngine;

// Single source of truth for the HUD's neon language. Additive optics: black is
// transparent, so legibility comes from bright strokes, never dark fills.
public static class CyberPalette
{
    public static readonly Color Cyan    = new Color(0.00f, 0.94f, 1.00f);   // #00F0FF scan / tech
    public static readonly Color Yellow  = new Color(0.99f, 0.93f, 0.04f);   // #FCEE0A locked / accent
    public static readonly Color Magenta = new Color(1.00f, 0.18f, 0.59f);   // #FF2E97 organic / alert
    public static readonly Color Green   = new Color(0.35f, 1.00f, 0.45f);   // #59FF73 household hardware
    public static readonly Color Dim     = new Color(0.00f, 0.55f, 0.62f);   // muted cyan for chrome
    public static readonly Color PanelFill = new Color(0.00f, 0.25f, 0.30f, 0.10f); // barely-there grouping fill

    // Class → color. Organics = magenta, electronics = cyan, all other household
    // hardware = green. Three hues keeps the additive HUD readable.
    public static Color ForClass(int id)
    {
        if (id == 0 || id == 14 || id == 15 || id == 16 || id == 77) return Magenta;   // organics (+teddy)
        if (id >= 62 && id <= 67) return Cyan;                                          // screens & devices
        return Green;
    }

    public static Color Locked => Yellow;

    // Threat-chip vocabulary — cyberpunk designations for every wanted class.
    public static string ClassWord(int id) => id switch
    {
        0  => "ORGANIC",
        14 => "AVIAN",
        15 => "FELINE",
        16 => "CANINE",
        39 => "FLASK",
        40 => "VESSEL",
        41 => "VESSEL",
        42 => "UTENSIL",
        43 => "EDGE TOOL",
        44 => "UTENSIL",
        45 => "VESSEL",
        46 => "SUSTENANCE",
        47 => "SUSTENANCE",
        48 => "SUSTENANCE",
        49 => "SUSTENANCE",
        56 => "SEATING",
        57 => "SEATING",
        58 => "FLORA",
        59 => "REST POD",
        60 => "SURFACE",
        62 => "DISPLAY",
        63 => "TERMINAL",
        64 => "INPUT DEV",
        65 => "CONTROLLER",
        66 => "INPUT DEV",
        67 => "COMMS UNIT",
        68 => "EMITTER",
        69 => "THERMAL UNIT",
        70 => "THERMAL UNIT",
        71 => "BASIN",
        72 => "CRYO UNIT",
        73 => "ARCHIVE",
        74 => "CHRONO",
        75 => "VESSEL",
        76 => "EDGE TOOL",
        77 => "SYNTH ORGANIC",
        78 => "TOOL",
        79 => "TOOL",
        _  => "OBJECT"
    };

    // Deterministic fake hex readout content, cheap to regenerate.
    public static string HexTicker(int seed, int groups = 3)
    {
        var sb = new System.Text.StringBuilder(groups * 5);
        uint h = (uint)(seed * 2654435761u + 0x9E3779B9u);
        for (int i = 0; i < groups; i++)
        {
            h ^= h << 13; h ^= h >> 17; h ^= h << 5;
            sb.Append((h & 0xFFFF).ToString("X4"));
            if (i < groups - 1) sb.Append(' ');
        }
        return sb.ToString();
    }
}
