using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.Network;
using predo.sdvmods.EditStableOwnership.Framework;
using predo.sdvmods.EditStableOwnership.Compatibility;
using EditStableOwnership;
using Netcode;
using StardewValley.Characters;
using Microsoft.Xna.Framework;

namespace predo.sdvmods.EditStableOwnership
{
    /// <summary>The mod entry point.</summary>
    internal sealed class ModEntry : Mod
    {
        private static ModConfig _modConfig = new ModConfig();

        /*********
        ** Public methods
        *********/
        /// <summary>The mod entry point, called after the mod is first loaded.</summary>
        /// <param name="helper">Provides simplified APIs for writing mods.</param>
        public override void Entry(IModHelper helper)
        {
            I18n.Init(helper.Translation);

            helper.Events.Input.ButtonPressed += OnButtonPressed;
            helper.Events.GameLoop.GameLaunched += OnGameLaunched;
        }

        /*********
        ** Private methods
        *********/
        /// <summary>On game launcher event.</summary>
        private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
        {
            // Trying to registry the GenericModConfigMenu API
            var configMenu = Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");

            // If the mod is not installed, return
            if (configMenu == null)
                return;
            
            // Configuring the options for the GenericModConfigMenu
            configMenu.Register(
                mod: this.ModManifest,
                reset: () => _modConfig = new ModConfig(),
                save: () => this.Helper.WriteConfig(_modConfig)
            );
            configMenu.AddSectionTitle(
                mod: this.ModManifest,
                text: () => I18n.ConfigMenu_Title_Keys(),
                tooltip: () => I18n.ConfigMenu_TitleTooltip_Keys()
            );
            configMenu.AddKeybindList(
                mod: this.ModManifest,
                getValue: () => _modConfig.ChangeStableOwnerKeybing,
                setValue: value => _modConfig.ChangeStableOwnerKeybing = value,
                name: () => I18n.ConfigMenu_Key_ChangeOwner(),
                tooltip: () => I18n.ConfigMenu_KeyTooltip_ChangeOwner()
            );
        }

        /// <summary>Raised after the player presses a button on the keyboard, controller, or mouse.</summary>
        private void OnButtonPressed(object? sender, ButtonPressedEventArgs e)
        {
            // ignore if player hasn't loaded a save yet
            if (!Context.IsWorldReady)
                return;
            
            // Checking if the player is playing on multiplayer and it is the "master player"
            if (Game1.server != null && Game1.player.userID == Game1.MasterPlayer.userID && Game1.player.currentLocation is Farm)
            {
                if (_modConfig.ChangeStableOwnerKeybing.JustPressed())
                    ChangeStableOwnership(e.Cursor, Game1.getOnlineFarmers());
            }
                
        }

        /// <summary>Set a new owner for a given stable.</summary>
        private void SetNewStableOwner(Stable stable, string farmer_umi, FarmerCollection farmers)
        {
            foreach(var farmer in farmers)
            {
                if (farmer.UniqueMultiplayerID.ToString() == farmer_umi)
                {
                    this.Monitor.Log($"Previous owner: {stable.owner.Value} - New owner: {farmer.UniqueMultiplayerID}", LogLevel.Info);
                    stable.owner.Value = farmer.UniqueMultiplayerID;
                    Horse horse = stable.getStableHorse();
                    horse.ownerId.Value = farmer.UniqueMultiplayerID;
                    horse.Name = "";
                    Game1.addHUDMessage(new HUDMessage(I18n.Alert_StableNewOwner()+farmer.Name, HUDMessage.newQuest_type) { timeLeft = 2000 });
                    break;
                }
            }
        }

        /// <summary>Run the ownership dialog recursively in order to show only 3 players per dialog.</summary>
        private void RecursiveOwnerDialog(Response[] responses, int skip, Stable stable, FarmerCollection farmers)
        {
            Game1.currentLocation.createQuestionDialogue(I18n.Dialog_ChooseNewOwner(), 
                responses.Skip(skip).Take(4).ToArray(), 
                new GameLocation.afterQuestionBehavior(
                    (Farmer who, string dialogue_id) => {
                        if (dialogue_id != "0"){
                            SetNewStableOwner(stable, dialogue_id, farmers);
                        }
                        else
                            RecursiveOwnerDialog(responses, skip+4, stable, farmers);
                    }
                )
            );
        }

        /// <summary>Start the process to change the stable ownership.</summary>
        private void ChangeStableOwnership(ICursorPosition cursor, FarmerCollection farmers)
        {
            // Getting the builing by mouse tile
            Building building = Game1.currentLocation.getBuildingAt(cursor.Tile);

            // Checking if it is actually a stable
            if (building is Stable)
            {
                Stable stable = (Stable)building;

                List<Response> responses = new List<Response>();

                // Creating the responses for the dialog
                int i = 0;
                foreach(var farmer in farmers)
                {
                    responses.Add(new Response($"{farmer.UniqueMultiplayerID}", $"{farmer.Name}"));

                    // For every 3 player, put a response to show more players
                    if (i != 0 && i % 2 == 0)
                        responses.Add(new Response($"0", $"Show more ->"));

                    i++;
                }

                Response[] responses_array = responses.ToArray<Response>();

                // Calling the recursive dialog
                RecursiveOwnerDialog(responses_array, 0, stable, farmers);
            }
        }
    }
}