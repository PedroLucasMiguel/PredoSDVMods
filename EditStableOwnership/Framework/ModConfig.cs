using StardewModdingAPI;
using StardewModdingAPI.Utilities;

namespace predo.sdvmods.EditStableOwnership.Framework
{
    /// <summary>A set of parsed key bindings.</summary>
    internal class ModConfig
    {
        /*********
        ** Accessors
        *********/
        /// <summary>The keys which tha player can change the stable ownership.</summary>
        public KeybindList ChangeStableOwnerKeybing { get; set; } = KeybindList.Parse($"{SButton.LeftShift}+{SButton.MouseRight}");
    }
}