using System.Windows.Forms;
using ExileCore.Shared.Attributes;
using ExileCore.Shared.Interfaces;
using ExileCore.Shared.Nodes;
using SharpDX;

namespace HarvestCraftExtractor
{
    public class Settings : ISettings
    {
        public Settings()
        {
            ExtractHarvestsKey = new HotkeyNode(Keys.F6);
            MinCraftLevel = new RangeNode<int>(76, 1, 100);
        }

        [Menu("Read crafts from stash")] public HotkeyNode ExtractHarvestsKey { get; set; }
        [Menu("Ignore crafts below this item level")] public RangeNode<int> MinCraftLevel { get; set; }

        public ToggleNode Enable { get; set; } = new ToggleNode(true);
    }
}