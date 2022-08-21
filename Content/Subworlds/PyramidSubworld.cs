﻿using System;
using System.Collections.Generic;
using System.Linq;
using LivingWorldMod.Common.Players;
using LivingWorldMod.Common.VanillaOverrides.WorldGen.GenShapes;
using LivingWorldMod.Content.Walls.WorldGen;
using LivingWorldMod.Custom.Classes;
using LivingWorldMod.Custom.Structs;
using LivingWorldMod.Custom.Utilities;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using SubworldLibrary;
using Terraria;
using Terraria.GameContent;
using Terraria.GameContent.Generation;
using Terraria.ID;
using Terraria.IO;
using Terraria.ModLoader;
using Terraria.UI.Chat;
using Terraria.Utilities;
using Terraria.WorldBuilding;

namespace LivingWorldMod.Content.Subworlds {
    public class PyramidSubworld : Subworld {
        /// <summary>
        /// The grid that the dungeon is composed of. Each room at maximum can be 100x100.
        /// </summary>
        public PyramidRoomGrid Grid {
            get;
            private set;
        }

        /// <summary>
        /// The "correct" path that leads from the starter room to the boss room.
        /// </summary>
        public List<PyramidRoom> CorrectPath {
            get;
            private set;
        }

        /// <summary>
        /// The "fake" paths that are tertiary from the correct path, that always lead to a dead end.
        /// </summary>
        public List<List<PyramidRoom>> FakePaths {
            get;
            private set;
        }

        public override int Width => _roomSideLength * _gridSideLength + _worldBorderPadding * 2;

        public override int Height => _roomSideLength * _gridSideLength + _bossRoomPadding + _worldBorderPadding * 2;

        public override bool ShouldSave => false;

        public override List<GenPass> Tasks => new List<GenPass>() {
            new PassLegacy("Initialize", Initialize),
            new PassLegacy("Fill World", FillWorld),
            new PassLegacy("Set Spawn", SetSpawn),
            new PassLegacy("Room Skeletons", GenerateRoomSkeleton),
            new PassLegacy("Paths' Room Generation", GenerateRoomsOnPath),
            new PassLegacy("Debug Draw Paths", DebugDrawPaths)
        };

        private readonly int _totalVanillaSaveOrLoadSteps = 4;

        private Asset<Texture2D> _pyramidBackground;
        private Asset<Texture2D> _pyramidWorldGenBar;
        private bool _isExiting;
        private string _lastStatusText = "";
        private int _vanillaLoadStepsPassed;

        private readonly int _worldBorderPadding = 150;
        private readonly int _roomSideLength = 101; //We want 100x100, for the internal room, the additional 1 is to compensate for the outline
        private readonly int _gridSideLength = 10;
        private readonly int _bossRoomPadding = 150;
        private int _spawnTileX;
        private int _spawnTileY;
        private UnifiedRandom _pyramidRandom;

        public override void Load() {
            _pyramidBackground = ModContent.Request<Texture2D>($"{LivingWorldMod.LWMSpritePath}Backgrounds/Loading/PyramidBG");
            _pyramidWorldGenBar = ModContent.Request<Texture2D>($"{LivingWorldMod.LWMSpritePath}UI/SubworldGeneration/GenPyramidBar");
            _pyramidRandom = new UnifiedRandom();
        }

        public override void OnEnter() {
            _vanillaLoadStepsPassed = -1;
            _lastStatusText = "";
            _isExiting = false;
        }

        public override void OnExit() {
            _vanillaLoadStepsPassed = -1;
            _lastStatusText = "";
            _isExiting = true;
        }

        public override void OnLoad() {
            //TODO: Multiplayer compat
            Main.LocalPlayer.GetModPlayer<PyramidDungeonPlayer>().currentRoom = CorrectPath.First();
        }

        public override void DrawMenu(GameTime gameTime) {
            //A bit of a hacky solution, but this avoids reflection; essentially, we want to add the steps of unloading the main world when
            //we are loading into the subworld (or the bar will stay stagnant for awhile which is boring) as a part of the progress, so we
            //check every time the status text changes (which denotes a step was completed)
            string deNumberedStatusText = string.Concat(Main.statusText.Where(character => !char.IsDigit(character)));
            if (deNumberedStatusText != _lastStatusText) {
                _vanillaLoadStepsPassed++;
            }
            _lastStatusText = deNumberedStatusText;

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullCounterClockwise);

            //Background Draw
            Main.spriteBatch.Draw(_pyramidBackground.Value, new Rectangle(0, 0, Main.screenWidth, Main.screenHeight), Color.White);

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, Main.Rasterizer, null, Main.UIScaleMatrix);

            //Progress Bar Drawing
            Vector2 totalProgBarPos = new Vector2(Main.screenWidth / 2f - 274f, Main.screenHeight / 2f - 18f);
            Vector2 totalProgBarSize = new Vector2(_pyramidWorldGenBar.Width() - 8f, 18f);

            Vector2 passProgBarPos = new Vector2(Main.screenWidth / 2f - 252f, Main.screenHeight / 2f);
            Vector2 passProgBarSize = new Vector2(_pyramidWorldGenBar.Width() - 48f, 12f);

            Color progressBarBackgroundColor = new Color(63, 63, 63);

            //Progress Bar Background Colors
            Main.spriteBatch.Draw(
                TextureAssets.MagicPixel.Value,
                new Rectangle((int)totalProgBarPos.X, (int)totalProgBarPos.Y, (int)totalProgBarSize.X, (int)totalProgBarSize.Y),
                progressBarBackgroundColor
            );
            Main.spriteBatch.Draw(
                TextureAssets.MagicPixel.Value,
                new Rectangle((int)passProgBarPos.X, (int)passProgBarPos.Y, (int)passProgBarSize.X, (int)passProgBarSize.Y),
                progressBarBackgroundColor
            );
            //Total Progress Color
            int totalProgBarWidth = (int)(totalProgBarSize.X * (_vanillaLoadStepsPassed / (float)_totalVanillaSaveOrLoadSteps * 0.34f + (WorldGenerator.CurrentGenerationProgress?.TotalProgress ?? (_isExiting ? 1f : 0f) * 0.66f)));
            Main.spriteBatch.Draw(
                TextureAssets.MagicPixel.Value,
                new Rectangle((int)totalProgBarPos.X, (int)totalProgBarPos.Y, totalProgBarWidth, (int)totalProgBarSize.Y),
                Color.LightCyan
            );
            //Pass Progress Color
            int passProgBarWidth = (int)(passProgBarSize.X * (WorldGenerator.CurrentGenerationProgress?.Value ?? 0f));
            Main.spriteBatch.Draw(
                TextureAssets.MagicPixel.Value,
                new Rectangle((int)passProgBarPos.X, (int)passProgBarPos.Y, passProgBarWidth, (int)passProgBarSize.Y),
                Color.GhostWhite
            );
            //Frame Sprite
            Main.spriteBatch.Draw(
                _pyramidWorldGenBar.Value,
                Utils.CenteredRectangle(new Vector2(Main.screenWidth, Main.screenHeight) / 2f, _pyramidWorldGenBar.Size()),
                Color.White
            );
            //Text Draw
            string drawnText = (WorldGenerator.CurrentGenerationProgress?.TotalProgress ?? 1) < 1 ? WorldGenerator.CurrentGenerationProgress.Message : Main.statusText;
            ChatManager.DrawColorCodedStringWithShadow(
                Main.spriteBatch,
                FontAssets.DeathText.Value,
                drawnText,
                new Vector2(Main.screenWidth, Main.screenHeight) / 2f - new Vector2(0f, 74f) - FontAssets.DeathText.Value.MeasureString(drawnText) / 2f,
                Color.White,
                0f,
                Vector2.Zero,
                Vector2.One
            );
        }

        /// <summary>
        /// Changes the internal UnifiedRandom RNG with the passed in seed. Call this before
        /// the subworld generation process starts.
        /// </summary>
        public void GenerateNewRandom(int seed) {
            _pyramidRandom = new UnifiedRandom(seed);
        }

        private void Initialize(GenerationProgress progress, GameConfiguration config) {
            progress.Message = "Initializing";
            //Default spawn spot, in case generation goes awry
            _spawnTileX = 225;
            _spawnTileY = 245;

            //Generate grid
            Grid = new PyramidRoomGrid(_gridSideLength, _roomSideLength, _worldBorderPadding, _pyramidRandom);
            Grid.GenerateGrid();

            //Generate correct path first
            CorrectPath = new List<PyramidRoom>() { Grid.GetRoom(_pyramidRandom.Next(_gridSideLength), 0) };
            PyramidRoom currentRoom = CorrectPath.First();
            currentRoom.pathSearched = true;

            while (currentRoom is not null) {
                PyramidRoom roomBelow = Grid.GetRoomBelow(currentRoom);
                PyramidRoom roomLeft = Grid.GetRoomToLeft(currentRoom);
                PyramidRoom roomRight = Grid.GetRoomToRight(currentRoom);

                List<Tuple<PyramidRoom, double>> movementChoices = new List<Tuple<PyramidRoom, double>>();
                movementChoices.Add(new Tuple<PyramidRoom, double>(roomBelow, 34)); //Down
                movementChoices.AddConditionally(new Tuple<PyramidRoom, double>(roomLeft, 33), roomLeft is { pathSearched: false }); //Left
                movementChoices.AddConditionally(new Tuple<PyramidRoom, double>(roomRight, 33), roomRight is { pathSearched: false }); //Right

                PyramidRoom selectedRoom = new WeightedRandom<PyramidRoom>(_pyramidRandom, movementChoices.ToArray()).Get();
                CorrectPath.Add(selectedRoom);
                if (selectedRoom is not null) {
                    selectedRoom.pathSearched = true;
                }

                currentRoom = selectedRoom;
            }

            //Properly set spawn point
            PyramidRoom starterRoom = CorrectPath.First();
            _spawnTileX = starterRoom.region.Center.X;
            _spawnTileY = starterRoom.region.Center.Y;

            //Generate Fake paths along the correct path
            FakePaths = new List<List<PyramidRoom>>();
            GenerateFakePath(CorrectPath, 5, 50);
            int originalCount = FakePaths.Count;
            for (int i = 0; i < originalCount; i++) {
                GenerateFakePath(FakePaths[i], 8, 30);
            }
        }

        private void FillWorld(GenerationProgress progress, GameConfiguration config) {
            progress.Message = "Wall Slabs";
            for (int i = 0; i < Main.maxTilesX; i++) {
                for (int j = 0; j < Main.maxTilesY; j++) {
                    progress.Set((j + i * Main.maxTilesY) / (float)(Main.maxTilesX * Main.maxTilesY));
                    Tile tile = Framing.GetTileSafely(i, j);
                    tile.WallType = (ushort)ModContent.WallType<PyramidBrickWall>();
                    if (i < _worldBorderPadding || i > Width - _worldBorderPadding || j < _worldBorderPadding || j > Height - _bossRoomPadding - _worldBorderPadding) {
                        tile.HasTile = true;
                        tile.TileType = TileID.SandStoneSlab;
                    }
                }
            }
        }

        private void SetSpawn(GenerationProgress progress, GameConfiguration config) {
            progress.Message = "Spawn Point";
            Main.spawnTileX = _spawnTileX;
            Main.spawnTileY = _spawnTileY;
        }

        private void GenerateRoomSkeleton(GenerationProgress progress, GameConfiguration config) {
            progress.Message = "Boxing the Rooms";

            for (int i = 0; i < _gridSideLength; i++) {
                progress.Set(i / (float)_gridSideLength);
                List<PyramidRoom> column = Grid.GetRoomColumn(i);

                foreach (PyramidRoom room in column) {
                    Rectangle roomRegion = room.region;

                    for (int x = roomRegion.X; x <= roomRegion.X + roomRegion.Width; x++) {
                        for (int y = roomRegion.Y; y <= roomRegion.Y + roomRegion.Height; y++) {
                            Tile tile = Framing.GetTileSafely(x, y);
                            if (!room.pathSearched || x == roomRegion.X || x == roomRegion.X + roomRegion.Width || y == roomRegion.Y || y == roomRegion.Y + roomRegion.Height) {
                                tile.HasTile = true;
                                tile.TileType = TileID.SandStoneSlab;
                            }
                        }
                    }
                }
            }
        }

        public void GenerateRoomsOnPath(GenerationProgress progress, GameConfiguration config) {
            progress.Message = "Shifting the Rooms";

            //Generate Rooms along the correct path, ignoring the last room (the boss room)
            for (int i = 0; i < CorrectPath.Count - 1; i++) {
                progress.Set(i / (float)CorrectPath.Count * 0.5f);

                PyramidRoom room = CorrectPath[i];
                Rectangle roomRegion = room.region;
                if (room.worldGenned) {
                    continue;
                }

                room.worldGenned = true;
                if (room.gridWidth != 1 || room.gridHeight != 1) {
                    continue;
                }

                StructureData roomData = IOUtils.GetStructureFromFile(LivingWorldMod.LWMStructurePath + $"PyramidRooms/{room.gridWidth}x{room.gridHeight}/Room0.struct");
                WorldGenUtils.GenerateStructure(roomData, roomRegion.X + 1, roomRegion.Y + 1, false);
            }

            //Generate Rooms along the fake paths
            for (int i = 0; i < FakePaths.Count; i++) {
                progress.Set(0.5f + i / (float)FakePaths.Count * 0.5f);
                List<PyramidRoom> fakePath = FakePaths[i];

                foreach (PyramidRoom room in fakePath) {
                    Rectangle roomRegion = room.region;
                    if (room.worldGenned) {
                        continue;
                    }

                    room.worldGenned = true;
                    if (room.gridWidth != 1 || room.gridHeight != 1) {
                        continue;
                    }

                    StructureData roomData = IOUtils.GetStructureFromFile(LivingWorldMod.LWMStructurePath + $"PyramidRooms/{room.gridWidth}x{room.gridHeight}/Room0.struct");
                    WorldGenUtils.GenerateStructure(roomData, roomRegion.X + 1, roomRegion.Y + 1, false);
                }
            }
        }

        private void DebugDrawPaths(GenerationProgress progress, GameConfiguration config) {
            if (!LivingWorldMod.IsDebug) {
                return;
            }
            progress.Message = "Visualizing Paths";

            for (int i = 0; i < CorrectPath.Count - 1; i++) {
                if (CorrectPath[i + 1] is null) {
                    break;
                }
                Point firstCenter = CorrectPath[i].region.Center;
                Point secondCenter = CorrectPath[i + 1].region.Center;

                WorldUtils.Gen(firstCenter, new StraightLine(2f, secondCenter), new Actions.PlaceTile(TileID.LivingCursedFire));
            }

            foreach (List<PyramidRoom> fakePath in FakePaths) {
                for (int i = 0; i < fakePath.Count - 1; i++) {
                    Point firstCenter = fakePath[i].region.Center;
                    Point secondCenter = fakePath[i + 1].region.Center;

                    WorldUtils.Gen(firstCenter, new StraightLine(2f, secondCenter), new Actions.PlaceTile(TileID.LivingFire));
                }
            }
        }

        /// <summary>
        /// Scans the passed in path and randomly generates more fake paths branching off of it.
        /// Make sure the fake path list is properly initialized before calling this.
        /// </summary>
        /// <param name="pathToSearchAlong"> The path this method will search along to potentially branch off of. </param>
        /// <param name="branchOccurrenceDenominator">
        /// The starting value of 1/value, determining whether or not a new path will be created. Every time the RNG fails,
        /// this value will decrease by 1 until success, when it is reset to this value.
        /// </param>
        /// <param name="branchEndChanceDenominator">
        /// The starting value of 1/value, determining whether or not a path generating will end. Every time the RNG fails,
        /// this value will decrease by 5 until success, when it is reset to this value.
        /// </param>
        private void GenerateFakePath(List<PyramidRoom> pathToSearchAlong, int branchOccurrenceDenominator, int branchEndChanceDenominator) {
            int branchChanceDenominator = branchOccurrenceDenominator;

            foreach (PyramidRoom originalPathRoom in pathToSearchAlong) {
                if (originalPathRoom is null || Grid.GetRoomBelow(originalPathRoom) is { pathSearched: true } && Grid.GetRoomToLeft(originalPathRoom) is { pathSearched: true } && Grid.GetRoomToRight(originalPathRoom) is { pathSearched: true }) {
                    continue;
                }

                if (_pyramidRandom.NextBool(branchChanceDenominator)) {
                    branchChanceDenominator = branchOccurrenceDenominator;
                    int endChanceDenominator = branchEndChanceDenominator;

                    List<PyramidRoom> newPath = new List<PyramidRoom>() { originalPathRoom };
                    PyramidRoom currentFakeRoom = newPath[0];

                    while (true) {
                        PyramidRoom roomBelow = Grid.GetRoomBelow(currentFakeRoom);
                        PyramidRoom roomLeft = Grid.GetRoomToLeft(currentFakeRoom);
                        PyramidRoom roomRight = Grid.GetRoomToRight(currentFakeRoom);

                        List<Tuple<PyramidRoom, double>> movementChoices = new List<Tuple<PyramidRoom, double>>();
                        movementChoices.AddConditionally(new Tuple<PyramidRoom, double>(roomBelow, 25), roomBelow is { pathSearched: false }); //Down
                        movementChoices.AddConditionally(new Tuple<PyramidRoom, double>(roomLeft, 37.5), roomLeft is { pathSearched: false }); //Left
                        movementChoices.AddConditionally(new Tuple<PyramidRoom, double>(roomRight, 37.5), roomRight is { pathSearched: false }); //Right
                        if (!movementChoices.Any()) {
                            break;
                        }

                        PyramidRoom selectedRoom = new WeightedRandom<PyramidRoom>(_pyramidRandom, movementChoices.ToArray()).Get();
                        if (_pyramidRandom.NextBool(endChanceDenominator) || selectedRoom is null || selectedRoom.pathSearched) {
                            break;
                        }
                        newPath.Add(selectedRoom);
                        selectedRoom.pathSearched = true;

                        endChanceDenominator = (int)MathHelper.Clamp(endChanceDenominator - 5, 1, branchEndChanceDenominator);
                        currentFakeRoom = selectedRoom;
                    }

                    if (newPath.Count > 1) {
                        FakePaths.Add(newPath);
                    }
                }
                else {
                    branchChanceDenominator = (int)MathHelper.Clamp(branchChanceDenominator - 1, 1, branchChanceDenominator);
                }
            }
        }
    }
}