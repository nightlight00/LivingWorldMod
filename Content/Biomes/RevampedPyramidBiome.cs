﻿using LivingWorldMod.Content.Walls.WorldGen;
using LivingWorldMod.Custom.Interfaces;
using Terraria;
using Terraria.ModLoader;

namespace LivingWorldMod.Content.Biomes {
    /// <summary>
    /// "Biome" for the Revamped Pyramid dungeon.
    /// </summary>
    public class RevampedPyramidBiome : ModBiome, IModifyLightingBrightness {
        public bool LightingEffectActive => IsBiomeActive(Main.LocalPlayer);

        public override bool IsBiomeActive(Player player) => Framing.GetTileSafely((int)(player.Center.X / 16f), (int)(player.Center.Y / 16f)).WallType == ModContent.WallType<PyramidBrickWall>();

        public void LightingEffect(ref float scale) {
            scale *= 0.25f;
        }
    }
}