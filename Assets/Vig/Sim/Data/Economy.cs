// The night's economy as data: the quota curve, the bank floor, shortfall and overshoot. Every number
// names its DESIGN.md section (docs/DESIGN.md); the data test enforces it.
namespace Vig.Sim.Data
{
    /// <summary>A design number and where it comes from.</summary>
    public sealed class Num
    {
        public readonly double V;
        public readonly string Derivation;
        public Num(double v, string derivation) { V = v; Derivation = derivation; }
        public int I => (int)V;
    }

    public static class Economy
    {
        public static readonly Num NightS = new Num(360, "DESIGN.md §1: a night is 360 s");
        public static readonly Num Nights = new Num(3, "DESIGN.md §1: the slice is three nights on floor 1 (ROADMAP Milestone 1)");
        public static readonly Num Quota1 = new Num(3000, "DESIGN.md §2: night 1 quota $3,000");
        public static readonly Num QuotaGrowth = new Num(1.5, "DESIGN.md §2: the quota x1.5 each night");
        public static readonly Num StartBank = new Num(1500, "DESIGN.md §2: start bank $1,500 (DECISIONS #40: $1,000 went broke on 42% of mid rotations)");
        public static readonly Num BankFloor = new Num(1000, "DESIGN.md §2 (DECISIONS #10): the bank carries over with a $1,000 floor");
        public static readonly Num ShortfallPerLife = new Num(2000, "DESIGN.md §2: one life per $2,000 short, rounded up (DECISIONS #55)");
        public static readonly Num OvershootPerMarker = new Num(500, "DESIGN.md §2: 1 marker per $500 over quota");
        public static readonly Num StartLives = new Num(3, "DESIGN.md §1: 3 lives per player (PLAN.md currencies)");
        public static readonly Num StartMarkers = new Num(6, "DESIGN.md §5 (DECISIONS #85): a run starts with 6 markers: a charm or two at home, or saved");
        public static readonly Num MaxLives = new Num(5, "DESIGN.md §7: lives cap at 5 (Boon and Lucky Fuse can lift you past 3)");
        public static readonly Num CharmSlots = new Num(3, "DESIGN.md §5: 3 charm slots per player");
        public static readonly Num BuybackShare = new Num(0.5, "DESIGN.md §4: soul buyback costs 50% of the current quota");
        public static readonly Num MarkersRedline = new Num(1, "DESIGN.md §5: a Red Line win +1 marker");
        public static readonly Num MarkersDeadman = new Num(2, "DESIGN.md §5: a Dead Man's Hand win +2 markers");

        public static readonly Num[] All =
        {
            NightS, Nights, Quota1, QuotaGrowth, StartBank, BankFloor, ShortfallPerLife, OvershootPerMarker,
            StartLives, StartMarkers, MaxLives, CharmSlots, BuybackShare, MarkersRedline, MarkersDeadman,
        };
    }
}
