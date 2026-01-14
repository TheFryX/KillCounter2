using System.Drawing;
using ExileCore2.Shared.Attributes;
using ExileCore2.Shared.Interfaces;
using ExileCore2.Shared.Nodes;

namespace KillCounter2;

public class KillCounterSettings : ISettings
{
    [Menu("Enable", "Toggle the KillCounter overlay.")]
    public ToggleNode Enable { get; set; } = new(true);

    [Menu("Show in Town / Hideout", "If disabled, the counter is hidden in towns and hideouts.")]
    public ToggleNode ShowInTown { get; set; } = new(false);

    [Menu("Show when zero kills", "If disabled, the overlay is hidden until you kill at least one monster in the area.")]
    public ToggleNode ShowWhenZero { get; set; } = new(true);

    [Menu("Show rarity breakdown", "Show separate counts for White / Magic / Rare / Unique.")]
    public ToggleNode ShowDetail { get; set; } = new(true);

    [Menu("Horizontal layout", "If enabled, show counts horizontally: Kills / White / Magic / Rare / Unique.")]
    public ToggleNode HorizontalLayout { get; set; } = new(false);

    [Menu("Show White", "Show White rarity kills in the overlay.")]
    public ToggleNode ShowWhite { get; set; } = new(true);

    [Menu("Show Magic", "Show Magic rarity kills in the overlay.")]
    public ToggleNode ShowMagic { get; set; } = new(true);

    [Menu("Show Rare", "Show Rare rarity kills in the overlay.")]
    public ToggleNode ShowRare { get; set; } = new(true);

    [Menu("Show Unique", "Show Unique rarity kills in the overlay.")]
    public ToggleNode ShowUnique { get; set; } = new(true);

    [Menu("Text color", "Color used for labels and the header.")]
    public ColorNode TextColor { get; set; } = new(Color.White);

    [Menu("White rarity color")]
    public ColorNode WhiteColor { get; set; } = new(Color.White);

    [Menu("Magic rarity color")]
    public ColorNode MagicColor { get; set; } = new(Color.DeepSkyBlue);

    [Menu("Rare rarity color")]
    public ColorNode RareColor { get; set; } = new(Color.Yellow);

    [Menu("Unique rarity color")]
    public ColorNode UniqueColor { get; set; } = new(Color.Orange);

    [Menu("Position X", "Horizontal screen position of the overlay (in pixels).")]
    public RangeNode<int> PositionX { get; set; } = new(50, 0, 4000);

    [Menu("Position Y", "Vertical screen position of the overlay (in pixels).")]
    public RangeNode<int> PositionY { get; set; } = new(200, 0, 4000);
}
