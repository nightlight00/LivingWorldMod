﻿using LivingWorldMod.Custom.Enums;
using LivingWorldMod.Custom.Structs;
using System.IO;
using System.Linq;
using LivingWorldMod.Common.Systems.UI;
using LivingWorldMod.Content.Tiles.Interactables;
using LivingWorldMod.Content.UI.VillageShrine;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using LivingWorldMod.Custom.Utilities;

namespace LivingWorldMod.Content.TileEntities.Interactables {
    /// <summary>
    /// Tile Entity within each village shrine of each type, which mainly handles whether or not a
    /// specified player is close enough to the specified shrine to be considered "within the village."
    /// </summary>
    [LegacyName("HarpyShrineEntity")]
    public class VillageShrineEntity : BaseTileEntity {
        public VillagerType shrineType;

        public Circle villageZone;

        public int remainingRespawnItems;

        public int remainingRespawnTime;

        public int respawnTimeCap;

        public override int ValidTileID => ModContent.TileType<VillageShrineTile>();

        private int _syncTimer;

        private int _currentVillagerCount;

        private int _currentValidHouses;

        public const float DefaultVillageRadius = 1360f;

        public const int EmptyVillageRespawnTime = 60 * 60 * 15;

        public const int FullVillageRespawnTime = 60 * 60 * 3;

        public override bool? PreValidTile(int i, int j) {
            Tile tile = Framing.GetTileSafely(i, j);

            return tile.HasTile && tile.TileType == ValidTileID && tile.TileFrameX % 72 == 0 && tile.TileFrameY == 0;
        }

        public override void Update() {
            //This is only here for backwards compatibility, if someone is loading a world from where
            //the shrines were the older HarpyShrineEntity type, then their VillageZone values will
            //be default and thus need to be fixed.
            if (villageZone == default) {
                InstantiateVillageZone();

                SyncDataToClients();
            }

            if (--_syncTimer <= 0) {
                _syncTimer = 60 * 4;

                _currentVillagerCount = NPCUtils.GetVillagerCountInZone(villageZone);
                _currentValidHouses = NPCUtils.GetValidHousesInZone(villageZone.ToTileCoordinates(), NPCUtils.VillagerTypeToNPCType(shrineType));

                respawnTimeCap = (int)MathHelper.Lerp(FullVillageRespawnTime, EmptyVillageRespawnTime, _currentValidHouses > 0 ? _currentVillagerCount / (float)_currentValidHouses : 0f);
                remainingRespawnTime = (int)MathHelper.Clamp(remainingRespawnTime, 0f, respawnTimeCap);

                SyncDataToClients();

                return;
            }

            remainingRespawnTime = (int)MathHelper.Clamp(remainingRespawnTime - 1, 0f, respawnTimeCap);
            if (remainingRespawnTime <= 0 && remainingRespawnItems <= _currentValidHouses) {
                remainingRespawnTime = respawnTimeCap;
                remainingRespawnItems++;

                SyncDataToClients();
            }

            if (_currentVillagerCount < _currentValidHouses && remainingRespawnItems > 0) {
                Rectangle housingRectangle = villageZone.ToTileCoordinates().ToRectangle();
                int villagerNPCType = NPCUtils.VillagerTypeToNPCType(shrineType);

                for (int i = 0; i < housingRectangle.Width; i++) {
                    for (int j = 0; j < housingRectangle.Height; j++) {
                        Point position = new Point(housingRectangle.X + i, housingRectangle.Y + j);

                        if (WorldGen.StartRoomCheck(position.X, position.Y) && WorldGen.RoomNeeds(villagerNPCType)) {
                            WorldGen.ScoreRoom(npcTypeAskingToScoreRoom: villagerNPCType);

                            if (Main.npc.Any(npc => npc.homeTileX == WorldGen.bestX && npc.homeTileY == WorldGen.bestY)) {
                                continue;
                            }

                            int npc = NPC.NewNPC(Entity.GetSource_TownSpawn(), WorldGen.bestX * 16, WorldGen.bestY * 16, villagerNPCType);

                            Main.npc[npc].homeTileX = WorldGen.bestX;
                            Main.npc[npc].homeTileY = WorldGen.bestY;
                        }
                    }
                }

                remainingRespawnItems--;
                SyncDataToClients();
            }
        }

        public override void NetSend(BinaryWriter writer) {
            writer.Write((int)shrineType);

            writer.WriteVector2(villageZone.center);
            writer.Write(villageZone.radius);

            writer.Write(remainingRespawnTime);
            writer.Write(respawnTimeCap);

            writer.Write(_currentVillagerCount);
            writer.Write(_currentValidHouses);
        }

        public override void NetReceive(BinaryReader reader) {
            shrineType = (VillagerType)reader.ReadInt32();

            villageZone = new Circle(reader.ReadVector2(), reader.ReadSingle());
            remainingRespawnTime = reader.ReadInt32();
            respawnTimeCap = reader.ReadInt32();

            _currentVillagerCount = reader.ReadInt32();
            _currentValidHouses = reader.ReadInt32();
        }

        public override void OnNetPlace() {
            SyncDataToClients(false);
        }

        public override void SaveData(TagCompound tag) {
            tag["ShrineType"] = (int)shrineType;
            tag["RemainingItems"] = remainingRespawnItems;
            tag["RemainingTime"] = remainingRespawnTime;
        }

        public override void LoadData(TagCompound tag) {
            shrineType = (VillagerType)tag.GetInt("ShrineType");
            remainingRespawnItems = tag.GetInt("RemainingItems");
            remainingRespawnTime = tag.GetInt("RemainingTime");
            respawnTimeCap = EmptyVillageRespawnTime;

            InstantiateVillageZone();
        }

        public override int Hook_AfterPlacement(int i, int j, int type, int style, int direction, int alternate) {
            shrineType = (VillagerType)style;

            if (Main.netMode == NetmodeID.MultiplayerClient) {
                NetMessage.SendTileSquare(Main.myPlayer, i, j, 4, 5);

                NetMessage.SendData(MessageID.TileEntityPlacement, -1, -1, null, i, j, Type);
            }

            Point16 tileOrigin = ModContent.GetInstance<VillageShrineTile>().tileOrigin;
            int placedEntity = Place(i - tileOrigin.X, j - tileOrigin.Y);
            if (placedEntity != -1) {
                (ByID[placedEntity] as VillageShrineEntity)!.InstantiateVillageZone();
            }

            return placedEntity;
        }

        /// <summary>
        /// Called when the tile this entity is associated with is right clicked.
        /// </summary>
        public void RightClicked() {
            VillageShrineUISystem shrineSystem = ModContent.GetInstance<VillageShrineUISystem>();

            switch (shrineSystem.correspondingInterface.CurrentState) {
                case null:
                case VillageShrineUIState state when state.CurrentEntity != this:
                    shrineSystem.OpenOrRegenShrineState(this);
                    break;
                case VillageShrineUIState:
                    shrineSystem.CloseShrineState();
                    break;
            }
        }

        /// <summary>
        /// Really simple method that just sets the village zone field to its proper values given
        /// the the tile entity's current position.
        /// </summary>
        private void InstantiateVillageZone() {
            villageZone = new Circle(Position.ToWorldCoordinates(32f, 40f), DefaultVillageRadius);
        }

        /// <summary>
        /// Little helper method that syncs this tile entity from Server to clients.
        /// </summary>
        /// <param name="doServerCheck"> Whether or not to check if the current Netmode is a Server. </param>
        private void SyncDataToClients(bool doServerCheck = true) {
            if (doServerCheck && Main.netMode != NetmodeID.Server) {
                return;
            }

            NetMessage.SendData(MessageID.TileEntitySharing, -1, -1, null, ID, Position.X, Position.Y);
        }
    }
}