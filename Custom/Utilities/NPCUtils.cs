﻿using LivingWorldMod.Common.Systems;
using LivingWorldMod.Content.NPCs.Villagers;
using LivingWorldMod.Custom.Enums;
using LivingWorldMod.Custom.Structs;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using LivingWorldMod.Content.Items.Miscellaneous;
using Terraria.DataStructures;
using Terraria;
using Terraria.ModLoader;

namespace LivingWorldMod.Custom.Utilities {
    /// <summary>
    /// Class that handles all utility functions for NPCs.
    /// </summary>
    public static class NPCUtils {
        /// <summary>
        /// Returns the price multiplier that will affect shop prices depending on the status of a
        /// villager's relationship with the players.
        /// </summary>
        public static float GetPriceMultiplierFromRep(Villager villager) {
            if (villager.RelationshipStatus == VillagerRelationship.Neutral) {
                return 1f;
            }

            ReputationThresholdData thresholds = ReputationSystem.Instance.villageThresholdData[villager.VillagerType];

            float reputationValue = ReputationSystem.Instance.GetNumericVillageReputation(villager.VillagerType);
            float centerPoint = (thresholds.likeThreshold - thresholds.dislikeThreshold) / 2f;

            return MathHelper.Clamp(1 - reputationValue / (ReputationSystem.VillageReputationConstraint - centerPoint) / 2f, 0.67f, 1.67f);
        }

        /// <summary>
        /// Returns the count of all defined villager types as defined by the <seealso cref="VillagerType"/> enum.
        /// </summary>
        /// <returns></returns>
        public static int GetTotalVillagerTypeCount() => Enum.GetValues<VillagerType>().Length;

        /// <summary>
        /// Gets &amp; returns the respective NPC for the given villager type passed in.
        /// </summary>
        /// <param name="type"> The type of villager you want to NPC equivalent of. </param>
        public static int VillagerTypeToNPCType(VillagerType type) {
            return type switch {
                VillagerType.Harpy => ModContent.NPCType<HarpyVillager>(),
                _ => -1
            };
        }

        /// <summary>
        /// Gets &amp; returns the respective Respawn Item for the given villager type passed in.
        /// </summary>
        /// <param name="type"> The type of villager you want to Respawn Item equivalent of. </param>
        public static int VillagerTypeToRespawnItemType(VillagerType type) {
            return type switch {
                VillagerType.Harpy => ModContent.ItemType<HarpyEgg>(),
                _ => -1
            };
        }

        /// <summary>
        /// Searches through all active NPCs in the npc array and returns how many of them are villagers and
        /// are currently within the the circle zone provided.
        /// </summary>
        /// <param name="zone"> The zone that an NPC must be within to be considered "In The Zone." </param>
        /// <returns></returns>
        public static int GetVillagerCountInZone(Circle zone) {
            int count = 0;

            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];

                if (npc.active && npc.ModNPC is Villager && zone.ContainsPoint(npc.Center)) {
                    count++;
                }
            }

            return count;
        }

        /// <summary>
        /// Gets &amp; returns the count of all houses that are valid housing within the passed in zone with the passed in
        /// NPC type. Make sure the passed in zone is in tile coordinates.
        /// </summary>
        /// <param name="zone"> The zone to search in. This should be tile coordinates. </param>
        /// <param name="npcType"> The type of NPC to check the housing with. If you want "Normal" checking, pass in the Guide type. </param>
        public static int GetValidHousesInZone(Rectangle zone, int npcType) {
            List<Point16> foundHouses = new List<Point16>();

            for (int i = 0; i < zone.Width; i++) {
                for (int j = 0; j < zone.Height; j++) {
                    Point position = new Point(zone.X + i, zone.Y + j);

                    if (WorldGen.StartRoomCheck(position.X, position.Y) && WorldGen.RoomNeeds(npcType)) {
                        WorldGen.ScoreRoom(npcTypeAskingToScoreRoom: npcType);
                        Point16 bestPoint = new Point16(WorldGen.bestX, WorldGen.bestY);

                        if (foundHouses.Contains(bestPoint)) {
                            continue;
                        }

                        foundHouses.Add(bestPoint);
                    }
                }
            }

            return foundHouses.Count;
        }

        /// <summary>
        /// Gets &amp; returns the count of all houses that are valid housing within the passed in zone with the passed in
        /// NPC type. Make sure the passed in zone is in tile coordinates.
        /// </summary>
        /// <param name="zone"> The zone to search in. This should be tile coordinates. </param>
        /// <param name="npcType"> The type of NPC to check the housing with. If you want "Normal" checking, pass in the Guide type. </param>
        public static int GetValidHousesInZone(Circle zone, int npcType) {
            List<Point16> foundHouses = new List<Point16>();
            Rectangle rectangle = zone.ToRectangle();

            for (int i = 0; i < rectangle.Width; i++) {
                for (int j = 0; j < rectangle.Height; j++) {
                    Point position = new Point(rectangle.X + i, rectangle.Y + j);

                    if (WorldGen.StartRoomCheck(position.X, position.Y) && WorldGen.RoomNeeds(npcType)) {
                        WorldGen.ScoreRoom(npcTypeAskingToScoreRoom: npcType);
                        Point16 bestPoint = new Point16(WorldGen.bestX, WorldGen.bestY);

                        if (foundHouses.Contains(bestPoint) || !zone.ContainsPoint(bestPoint.ToVector2())) {
                            continue;
                        }

                        foundHouses.Add(bestPoint);
                    }
                }
            }

            return foundHouses.Count;
        }
    }
}