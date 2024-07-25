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

namespace predo.sdvmods.EditStableOwnership
{  
    /// <summary>The mod entry point.</summary>
    internal sealed class ModEntry : Mod
    {
        private static ModConfig _modConfig = new ModConfig();
        private static bool _enableMod = true;
        private static bool _isDialogActive = false;
        private static int _dialogSkip = 0;
        private static List<Response> _responses = new();
        private static Stable? _stable; 
        private static FarmerCollection? _farmers;
        private static string? _farmerUMI;
        private static bool _showDialogAgain = true; 

        /*********
        ** Public methods
        *********/
        /// <summary>The mod entry point, called after the mod is first loaded.</summary>
        /// <param name="helper">Provides simplified APIs for writing mods.</param>
        public override void Entry(IModHelper helper)
        {
            I18n.Init(helper.Translation);

            helper.Events.GameLoop.GameLaunched += OnGameLaunched;
            helper.Events.Input.ButtonPressed += OnButtonPressed;
            helper.Events.GameLoop.SaveLoaded += OnSaveLoaded;
            helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
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

        private void OnSaveLoaded(object? sender, SaveLoadedEventArgs e)
        {
            if (Game1.server == null || !Context.IsMainPlayer)
            {
                this.Monitor.Log("Mod Disabled. Only works for the main player in multiplayer.", LogLevel.Warn);
                _enableMod = false;
            }
            else
                _enableMod = true;
        }

        /// <summary>Raised after the player presses a button on the keyboard, controller, or mouse.</summary>
        private void OnButtonPressed(object? sender, ButtonPressedEventArgs e)
        {
            // Ignore if player hasn't loaded a save yet or the game is singleplayer
            if (!Context.IsWorldReady || !_enableMod)
                return;
            
            // Checking if the player is playing on multiplayer and it is the "master player"
            if (Game1.player.currentLocation is Farm)
            {
                if (_modConfig.ChangeStableOwnerKeybing.JustPressed())
                    ChangeStableOwnership(e.Cursor, Game1.getOnlineFarmers());
            }
                
        }

        private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
        {
            if (!_isDialogActive)
                return;

            if (_showDialogAgain)
            {
                Game1.currentLocation.createQuestionDialogue(I18n.Dialog_ChooseNewOwner(), 
                    _responses.ToArray().Skip(_dialogSkip).Take(4).ToArray(), 
                    new GameLocation.afterQuestionBehavior(
                        (Farmer who, string dialogue_id) => {
                            if (dialogue_id == "0")
                            {
                                _dialogSkip += 4;
                                _showDialogAgain = true;
                            }
                            else
                            {
                                _farmerUMI = dialogue_id;
                                _isDialogActive = false;
                                SetNewStableOwner();
                            }
                                
                        }
                    )
                );

                _showDialogAgain = false;
            }
        }

        /// <summary>Set a new owner for a given stable.</summary>
        private void SetNewStableOwner()
        {
            foreach(var farmer in _farmers)
            {
                if (farmer.UniqueMultiplayerID.ToString() == _farmerUMI)
                {
                    Monitor.Log($"Previous owner: {_stable.owner.Value} - New owner: {farmer.UniqueMultiplayerID}", LogLevel.Info);
                    _stable.owner.Value = farmer.UniqueMultiplayerID;
                    Horse horse = _stable.getStableHorse();
                    horse.ownerId.Value = farmer.UniqueMultiplayerID;
                    horse.Name = "";
                    Game1.addHUDMessage(new HUDMessage(I18n.Alert_StableNewOwner()+farmer.Name, HUDMessage.newQuest_type) { timeLeft = 2000 });
                    break;
                }
            }
        }

        /// <summary>Start the process to change the stable ownership.</summary>
        private void ChangeStableOwnership(ICursorPosition cursor, FarmerCollection farmers)
        {
            // Getting the builing by mouse tile
            Building building = Game1.currentLocation.getBuildingAt(cursor.Tile);

            // Checking if it is actually a stable
            if (building is Stable)
            {
                _stable = (Stable)building;
                _farmers = farmers;

                // Creating the responses for the dialog
                int i = 0;
                foreach(var farmer in farmers)
                {
                    _responses.Add(new Response($"{farmer.UniqueMultiplayerID}", $"{farmer.Name}"));

                    // For every 3 player, put a response to show more players
                    if (i != 0 && i % 2 == 0)
                        _responses.Add(new Response($"0", $"Show more ->"));

                    i++;
                }

                // Start dialog
                _isDialogActive = true;
            }
        }
    }
}