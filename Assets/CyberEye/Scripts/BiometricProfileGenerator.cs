// M6: deterministic FICTIONAL dossier per track ID (seeded, so a locked target keeps the same identity).
// All data is invented for the cyberpunk-fiction experience — NOT real people. See privacy note (M9).
public static class BiometricProfileGenerator
{
    public struct Profile { public string title, name, line1, line2, stat, fact; }

    static readonly string[] First = { "KODA","RIVEN","NYX","JAX","MIRA","ZEV","LENA","ORION","VESPER","CASS","ECHO","DRAX","SIL","NOVA","RANE","IKO","VALA","TORU","SASHA","WREN" };
    static readonly string[] Last  = { "VOSS","KANE","MERIDIAN","OKONKWO","SALT","VANTA","REYES","HOLLOW","KUROSAWA","DELACROIX","GRIMM","ASH","NAKAMURA","STORM","BJORK","VEX","QUILL","MARROW","STRAND","OZ" };
    static readonly string[] Sectors = { "NEON HEIGHTS","SECTOR 7","THE SPRAWL","CHROME ROW","LOWTOWN","GRID 12","ASHFALL","THE STACKS","VOLTA DISTRICT","RUST QUARTER" };
    static readonly string[] Streets = { "Kessler Arcology","Trans-Am Overpass","Datcha Blocks","Neon Alley","Kowloon Spire","Faraday Lofts","Meridian Undercroft","Panopticon Plaza" };
    static readonly string[] Threat  = { "LOW","MODERATE","ELEVATED","SEVERE","BLACKLISTED" };
    static readonly string[] Facts = {
        "Owes 14,203cr to Zaibatsu MedCorp. Organ lien active.",
        "Flagged: 3 unlicensed dreams last cycle.",
        "Subdermal comms implant — model banned in 4 arcologies.",
        "Social feed sentiment: 12% (below compliance floor).",
        "Last seen purchasing analog coffee. Non-conformist.",
        "Employment: gig-runner, tier-C. Sleep debt: 61 hrs.",
        "Loyalty score decaying. Re-education eligible.",
        "Registered 2 synthetic pets. 1 undeclared.",
        "Bio-signature spoofed once. Warranty voided.",
        "Consumes 40% more bandwidth than demographic median.",
        "Nostalgia index critical. Prescribed mandatory ads.",
        "Wanted for questioning re: the Blackout of '77."
    };
    static readonly string[] FaunaTitle = { "UNREGISTERED FAUNA","LIVESTOCK UNIT","BIO-ASSET" };
    static readonly string[] FaunaNames = { "K-9 UNIT","STRAY-CLASS","COMPANION DRONE?","ORGANIC, UNTAGGED","GENE-PATENT PENDING" };
    static readonly string[] FaunaFacts = {
        "No ownership chip detected. Impound eligible.",
        "Emotional-support permit EXPIRED.",
        "Possible uplift candidate. Flag to BioReg.",
        "Consuming public oxygen without subscription.",
        "Cuteness rating exceeds regulated threshold."
    };

    public static Profile ForTrack(int id, int classId)
    {
        var r = new System.Random(id * 92821 + classId * 6971 + 17);
        if (classId == 0) // person
        {
            int y = 1985 + r.Next(0, 30), m = r.Next(1, 13), d = r.Next(1, 28);
            return new Profile
            {
                title = "CITIZEN DOSSIER",
                name  = First[r.Next(First.Length)] + " " + Last[r.Next(Last.Length)].Trim(),
                line1 = "ID#" + r.Next(100000, 999999) + "  DOB " + $"{y:0000}.{m:00}.{d:00}",
                line2 = r.Next(100, 9999) + " " + Streets[r.Next(Streets.Length)] + ", " + Sectors[r.Next(Sectors.Length)],
                stat  = "CREDIT " + r.Next(180, 860) + "  |  THREAT " + Threat[r.Next(Threat.Length)],
                fact  = Facts[r.Next(Facts.Length)]
            };
        }
        // dog / cat / bird(duck)
        return new Profile
        {
            title = FaunaTitle[r.Next(FaunaTitle.Length)],
            name  = FaunaNames[r.Next(FaunaNames.Length)],
            line1 = "TAG#" + r.Next(1000, 9999) + "  SPECIES CLASS " + classId,
            line2 = "LAST SEEN: " + Sectors[r.Next(Sectors.Length)],
            stat  = "COMPLIANCE " + r.Next(0, 60) + "%  |  STATUS " + Threat[r.Next(3)],
            fact  = FaunaFacts[r.Next(FaunaFacts.Length)]
        };
    }
}
