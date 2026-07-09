using UnityEngine;

// Single source of truth for the HUD's neon language. Additive optics: black is
// transparent, so legibility comes from bright strokes, never dark fills.
public static class CyberPalette
{
    public static readonly Color Cyan    = new Color(0.00f, 0.94f, 1.00f);   // #00F0FF scan / friendly
    public static readonly Color Yellow  = new Color(0.99f, 0.93f, 0.04f);   // #FCEE0A locked / accent
    public static readonly Color Magenta = new Color(1.00f, 0.18f, 0.59f);   // #FF2E97 organic / alert
    public static readonly Color Dim     = new Color(0.00f, 0.55f, 0.62f);   // muted cyan for chrome
    public static readonly Color PanelFill = new Color(0.00f, 0.25f, 0.30f, 0.10f); // barely-there grouping fill

    // Class → color. person=magenta (organic), cat/dog=magenta, else yellow-tinged tech.
    public static Color ForClass(int id)
    {
        if (id == 0 || id == 15 || id == 16) return Magenta;
        return Cyan;
    }

    public static Color Locked => Yellow;

    // Threat-chip vocabulary for the detector's wanted classes (person/bird/cat/dog).
    public static string ClassWord(int id) => id switch
    {
        0  => "ORGANIC",
        14 => "AVIAN",
        15 => "FELINE",
        16 => "CANINE",
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
