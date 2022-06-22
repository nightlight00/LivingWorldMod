﻿using System;
using LivingWorldMod.Content.TileEntities.Interactables;
using LivingWorldMod.Content.UI.CommonElements;
using LivingWorldMod.Core.PacketHandlers;
using LivingWorldMod.Custom.Utilities;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent.UI.Elements;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;

namespace LivingWorldMod.Content.UI.VillageShrine {
    /// <summary>
    /// UIState that handles the UI for the village shrine for each type of village.
    /// </summary>
    public class VillageShrineUIState : UIState {
        public UIPanel backPanel;

        public UIPanel itemPanel;

        public UIBetterItemIcon respawnItemDisplay;

        public UIBetterText respawnItemCount;

        public UIPanelButton addRespawnButton;

        public UIPanelButton takeRespawnButton;

        public UIElement respawnTimerZone;

        public UIBetterText respawnTimerHeader;

        public UIBetterText respawnTimer;

        public VillageShrineEntity CurrentEntity {
            get;
            private set;
        }

        private TimeSpan _timeConverter;

        public override void OnInitialize() {
            Asset<Texture2D> vanillaPanelBackground = Main.Assets.Request<Texture2D>("Images/UI/PanelBackground");
            Asset<Texture2D> gradientPanelBorder = ModContent.Request<Texture2D>($"{LivingWorldMod.LWMSpritePath}UI/Elements/GradientPanelBorder");
            Asset<Texture2D> shadowedPanelBorder = ModContent.Request<Texture2D>($"{LivingWorldMod.LWMSpritePath}UI/Elements/ShadowedPanelBorder");

            backPanel = new UIPanel(vanillaPanelBackground, gradientPanelBorder) {
                BackgroundColor = new Color(59, 97, 203),
                BorderColor = Color.White
            };
            backPanel.Width = backPanel.Height = new StyleDimension(194f, 0f);
            Append(backPanel);

            itemPanel = new UIPanel(vanillaPanelBackground, shadowedPanelBorder) {
                BackgroundColor = new Color(46, 46, 159),
                BorderColor = new Color(22, 29, 107)
            };
            itemPanel.Width = itemPanel.Height = new StyleDimension(48f, 0f);
            itemPanel.SetPadding(0f);
            backPanel.Append(itemPanel);

            respawnItemDisplay = new UIBetterItemIcon(new Item(), 48f, true) {
                overrideDrawColor = Color.White * 0.45f
            };
            respawnItemDisplay.Width = respawnItemDisplay.Height = itemPanel.Width;
            itemPanel.Append(respawnItemDisplay);

            respawnItemCount = new UIBetterText("0") {
                horizontalTextConstraint = itemPanel.Width.Pixels,
                HAlign = 0.5f,
                VAlign = 0.85f
            };
            itemPanel.Append(respawnItemCount);

            addRespawnButton = new UIPanelButton(vanillaPanelBackground, gradientPanelBorder, text: "Add 1") {
                BackgroundColor = backPanel.BackgroundColor,
                BorderColor = Color.White,
                Width = itemPanel.Width,
                preventItemUsageWhileHovering = true
            };
            addRespawnButton.Height.Set(30f, 0f);
            addRespawnButton.Top.Set(itemPanel.Height.Pixels + 4f, 0f);
            addRespawnButton.ProperOnClick += AddRespawnItem;
            backPanel.Append(addRespawnButton);

            takeRespawnButton = new UIPanelButton(vanillaPanelBackground, gradientPanelBorder, text: "Take 1") {
                BackgroundColor = backPanel.BackgroundColor,
                BorderColor = Color.White,
                Width = addRespawnButton.Width,
                Height = addRespawnButton.Height,
                preventItemUsageWhileHovering = true
            };
            takeRespawnButton.Top.Set(addRespawnButton.Top.Pixels + addRespawnButton.Height.Pixels + 4f, 0f);
            takeRespawnButton.ProperOnClick += TakeRespawnItem;
            backPanel.Append(takeRespawnButton);

            respawnTimerZone = new UIElement();
            respawnTimerZone.Left.Set(itemPanel.Width.Pixels + 4f, 0f);
            respawnTimerZone.Width.Set(backPanel.Width.Pixels - backPanel.PaddingLeft - backPanel.PaddingRight - itemPanel.Width.Pixels - 4f, 0f);
            respawnTimerZone.Height.Set(itemPanel.Height.Pixels, 0f);
            backPanel.Append(respawnTimerZone);

            respawnTimerHeader = new UIBetterText("New Respawn Item in:") {
                HAlign = 0.5f,
                horizontalTextConstraint = respawnTimerZone.Width.Pixels
            };
            respawnTimerZone.Append(respawnTimerHeader);

            respawnTimer = new UIBetterText("00:00", 0.67f, true) {
                HAlign = 0.5f,
                horizontalTextConstraint = respawnTimerZone.Width.Pixels
            };
            respawnTimer.Top.Set(respawnTimerHeader.Height.Pixels + 12f, 0f);
            respawnTimerZone.Append(respawnTimer);

            _timeConverter = new TimeSpan();
        }

        public override void Update(GameTime gameTime) {
            base.Update(gameTime);

            if (CurrentEntity.remainingRespawnItems < CurrentEntity.CurrentValidHouses) {
                _timeConverter = new TimeSpan((long)(TimeSpan.TicksPerSecond / (double)60 * CurrentEntity.remainingRespawnTime));

                respawnTimer.SetText(_timeConverter.ToString(@"mm\:ss"));
            }
            else {
                respawnTimer.SetText("\u221E");
            }
        }

        protected override void DrawSelf(SpriteBatch spriteBatch) {
            CalculatedStyle panelDimensions = backPanel.GetDimensions();
            Vector2 centerOfEntity = CurrentEntity.Position.ToWorldCoordinates(32f, 0f);

            backPanel.Left.Set(centerOfEntity.X - panelDimensions.Width / 2f - Main.screenPosition.X, 0f);
            backPanel.Top.Set(centerOfEntity.Y - panelDimensions.Height - Main.screenPosition.Y, 0f);
            respawnItemCount.SetText(CurrentEntity.remainingRespawnItems.ToString());
            addRespawnButton.isVisible = CurrentEntity.remainingRespawnItems < CurrentEntity.CurrentValidHouses;
            takeRespawnButton.isVisible = CurrentEntity.remainingRespawnItems > 0;
        }

        /// <summary>
        /// Regenerates this UI state with the new passed in shrine entity.
        /// </summary>
        /// <param name="entity"> The new entity that this state will bind to. </param>
        public void RegenState(VillageShrineEntity entity) {
            CurrentEntity = entity;

            respawnItemDisplay.SetItem(NPCUtils.VillagerTypeToRespawnItemType(entity.shrineType));
            addRespawnButton.SetText(LocalizationUtils.GetLWMTextValue("UI.Shrine.AddText"));
            takeRespawnButton.SetText(LocalizationUtils.GetLWMTextValue("UI.Shrine.TakeText"));
            respawnTimerHeader.SetText(LocalizationUtils.GetLWMTextValue($"UI.Shrine.{entity.shrineType}Countdown"));
        }

        private void AddRespawnItem(UIMouseEvent evt, UIElement listeningElement) {
            int respawnItemType = NPCUtils.VillagerTypeToRespawnItemType(CurrentEntity.shrineType);
            Player player = Main.LocalPlayer;

            if (Main.netMode == NetmodeID.MultiplayerClient) {
                if (player.HasItem(respawnItemType)) {
                    player.inventory[player.FindItem(respawnItemType)].stack--;

                    ModPacket packet = ModContent.GetInstance<ShrinePacketHandler>().GetPacket(ShrinePacketHandler.AddRespawnItem);
                    packet.WriteVector2(CurrentEntity.Position.ToVector2());
                    packet.Send();
                }
            }
            else {
                if (player.HasItem(respawnItemType)) {
                    player.inventory[player.FindItem(respawnItemType)].stack--;

                    CurrentEntity.remainingRespawnItems++;
                }
            }
        }

        private void TakeRespawnItem(UIMouseEvent evt, UIElement listeningElement) {
            Player player = Main.LocalPlayer;
            Item respawnItem = new Item(NPCUtils.VillagerTypeToRespawnItemType(CurrentEntity.shrineType));

            if (Main.netMode == NetmodeID.MultiplayerClient) {
                if (player.CanAcceptItemIntoInventory(respawnItem)) {
                    ModPacket packet = ModContent.GetInstance<ShrinePacketHandler>().GetPacket(ShrinePacketHandler.TakeRespawnItem);
                    packet.WriteVector2(CurrentEntity.Position.ToVector2());
                    packet.Send();
                }
            }
            else {
                if (player.CanAcceptItemIntoInventory(respawnItem) && CurrentEntity.remainingRespawnItems < CurrentEntity.CurrentValidHouses) {
                    player.QuickSpawnItem(new EntitySource_TileEntity(CurrentEntity), respawnItem);

                    CurrentEntity.remainingRespawnItems--;
                }
            }
        }
    }
}