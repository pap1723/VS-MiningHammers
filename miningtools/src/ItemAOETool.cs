using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace miningtools
{
    public class ItemAOETool : Item
    {
        string[] allowedPrefixes;
        private int minWidth, minHeight, minDepth, xMin, yMin, zMin;
        private int maxWidth, maxHeight, maxDepth, xMax, yMax, zMax;
        SkillItem[] toolModes;
        private int curMode = 3;

        public virtual int MultiBreakQuantity => 8;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            allowedPrefixes = Attributes["codePrefixes"].AsArray<string>();

            if (api is ICoreClientAPI capi)
            {
                toolModes = ObjectCacheUtil.GetOrCreate(api, "aoeToolModes", () =>
                {
                    SkillItem[] modes = new SkillItem[7];

                    modes[0] = new SkillItem() { Code = new AssetLocation("1size"), Name = Lang.Get("1x1x1") }.WithIcon(capi, capi.Gui.LoadSvgWithPadding(new AssetLocation("miningtools:textures/icons/1x1x1.svg"), 48, 48, 30, ColorUtil.WhiteArgb));
                    modes[1] = new SkillItem() { Code = new AssetLocation("1x2x1size"), Name = Lang.Get("1x2x1") }.WithIcon(capi, capi.Gui.LoadSvgWithPadding(new AssetLocation("miningtools:textures/icons/1x2x1.svg"), 48, 48, 15, ColorUtil.WhiteArgb));
                    modes[2] = new SkillItem() { Code = new AssetLocation("2size"), Name = Lang.Get("2x2x1") }.WithIcon(capi, capi.Gui.LoadSvgWithPadding(new AssetLocation("miningtools:textures/icons/2x2x1.svg"), 48, 48, 5, ColorUtil.WhiteArgb));
                    modes[3] = new SkillItem() { Code = new AssetLocation("3size"), Name = Lang.Get("3x3x1") }.WithIcon(capi, capi.Gui.LoadSvgWithPadding(new AssetLocation("miningtools:textures/icons/3x3x1.svg"), 48, 48, 5, ColorUtil.WhiteArgb));
                    modes[4] = new SkillItem() { Code = new AssetLocation("3x2x1size"), Name = Lang.Get("3x2x1") }.WithIcon(capi, capi.Gui.LoadSvgWithPadding(new AssetLocation("miningtools:textures/icons/3x2x1.svg"), 48, 48, 5, ColorUtil.WhiteArgb));
                    modes[5] = new SkillItem() { Code = new AssetLocation("1x3x3size"), Name = Lang.Get("1x3x3") }.WithIcon(capi, capi.Gui.LoadSvgWithPadding(new AssetLocation("miningtools:textures/icons/1x3x3.svg"), 48, 48, 5, ColorUtil.WhiteArgb));
                    modes[6] = new SkillItem() { Code = new AssetLocation("checker"), Name = Lang.Get("Checkerboard") }.WithIcon(capi, capi.Gui.LoadSvgWithPadding(new AssetLocation("miningtools:textures/icons/checker.svg"), 48, 48, 5, ColorUtil.WhiteArgb));
                    foreach (var mode in modes)
                    {
                        mode.TexturePremultipliedAlpha = false;
                    }

                    return modes;
                });
            }
        }

        public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot itemSlot)
        {
            return base.GetHeldInteractionHelp(itemSlot).Append(new WorldInteraction
            {
                ActionLangCode = "heldhelp-settoolmode",
                HotKeyCode = "toolmodeselect"
            });
        }

        public override void OnUnloaded(ICoreAPI api)
        {
            for (int i = 0; toolModes != null && i < toolModes.Length; i++)
            {
                toolModes[i]?.Dispose();
            }
        }

        public virtual bool CanMultiBreak(Block block)
        {
            for (int i = 0; i < allowedPrefixes.Length; i++)
            {
                if (block.Code.Path.StartsWith(allowedPrefixes[i]))
                {
                    return true;
                }
            }

            return false;
        }

        public override float OnBlockBreaking(IPlayer player, BlockSelection blockSel, ItemSlot itemSlot, float remainingResistance, float dt, int counter)
        {
            float newResist = base.OnBlockBreaking(player, blockSel, itemSlot, remainingResistance, dt, counter);
            int leftDurability = itemSlot.Itemstack.Attributes.GetInt("durability", Durability);
            DamageNearbyBlocks(player, blockSel, remainingResistance - newResist, leftDurability, itemSlot);

            return newResist;
        }

        private void DamageNearbyBlocks(IPlayer player, BlockSelection blockSel, float damage, int leftDurability, ItemSlot itemSlot)
        {
            Block block = player.Entity.World.BlockAccessor.GetBlock(blockSel.Position);

            if (!CanMultiBreak(block)) return;

            Vec3d hitPos = blockSel.Position.ToVec3d().Add(blockSel.HitPosition);

            curMode = GetToolMode(itemSlot, player, blockSel);
            float playerFacing = player.WorldData.EntityPlayer.SidedPos.Yaw;
            api.Logger.Notification("DamageNearbyBlocks() playerFacing: {0}", playerFacing);

            if (curMode != 6) SetRangesFromFace(blockSel, curMode);
            List<BlockPos> blockPositions = new List<BlockPos>(GetBlocksToBreak(player.Entity.World, blockSel, xMin, xMax, yMin, yMax, zMin, zMax, curMode));

            //OrderedDictionary<BlockPos, float> dict = GetNearbyMultiBreakables(player.Entity.World, blockSel, hitPos, minWidth, minHeight, minDepth, maxWidth, maxHeight, maxDepth, curMode);
            //var orderedPositions = dict.OrderBy(x => x.Value).Select(x => x.Key);

            int q = Math.Min(MultiBreakQuantity, leftDurability);
            foreach (var pos in blockPositions)
            {
                if (q == 0) break;
                BlockFacing facing = BlockFacing.FromNormal(player.Entity.ServerPos.GetViewVector()).Opposite;
                
                if (!player.Entity.World.Claims.TryAccess(player, pos, EnumBlockAccessFlags.BuildOrBreak)) continue;

                Block theBlock = player.Entity.World.BlockAccessor.GetBlock(pos);
                IBlockAccessor blockAccessor = player.Entity.World.BlockAccessor;
                theBlock.GetSelectionBoxes(blockAccessor, pos);
                blockAccessor.DamageBlock(pos, facing, damage);
                q--;
            }
        }

        public override bool OnBlockBrokenWith(IWorldAccessor world, Entity byEntity, ItemSlot itemslot, BlockSelection blockSel, float dropQuantityMultiplier = 1)
        {
            Block block = world.BlockAccessor.GetBlock(blockSel.Position);

            base.OnBlockBrokenWith(world, byEntity, itemslot, blockSel, dropQuantityMultiplier);

            if (byEntity as EntityPlayer == null || itemslot.Itemstack == null) return true;

            IPlayer player = world.PlayerByUid((byEntity as EntityPlayer).PlayerUID);

            if (!CanMultiBreak(block)) return true;

            curMode = GetToolMode(itemslot, player, blockSel);
            float playerFacing = player.WorldData.EntityPlayer.SidedPos.Yaw;
            api.Logger.Notification("OnBlockBrokenWith() playerFacing: {0}", playerFacing);

            if (curMode != 6) SetRangesFromFace(blockSel, curMode);
            List<BlockPos> blockPositions = new List<BlockPos>(GetBlocksToBreak(world, blockSel, xMin, xMax, yMin, yMax, zMin, zMax, curMode));

            //Vec3d hitPos = blockSel.Position.ToVec3d().Add(blockSel.HitPosition);

            //var orderedPositions = GetNearbyMultiBreakables(player.Entity.World, blockSel, hitPos, minWidth, minHeight, minDepth, maxWidth, maxHeight, maxDepth, curMode).OrderBy(x => x.Value);

            int leftDurability = itemslot.Itemstack.Attributes.GetInt("durability", Durability);
            int q = 0;

            foreach (var val in blockPositions)
            {
                if (!player.Entity.World.Claims.TryAccess(player, val, EnumBlockAccessFlags.BuildOrBreak)) continue;

                world.BlockAccessor.BreakBlock(val, player);
                world.BlockAccessor.MarkBlockDirty(val);
                DamageItem(world, byEntity, itemslot);

                q++;
                if (q >= MultiBreakQuantity || itemslot.Itemstack == null) break;
            }

            return true;
        }

        /// <summary>
        /// We need to break the "correct" blocks depending on which face the player is breaking.
        /// </summary>
        /// <param name="sel"></param>
        public void SetRangesFromFace(BlockSelection sel, int mode)
        {
            GetRangesFromToolMode(mode);
            switch (sel.Face.Opposite.Code)
            {
                case "north":
                    // Left = East
                    // Right = West
                    // Front = South
                    // Back = North
                    api.Logger.Notification("sel.Face.Opposite.Code: {0}", sel.Face.Opposite.Code);
                    xMin = minWidth;
                    xMax = maxWidth;
                    yMin = minHeight;
                    yMax = maxHeight;
                    zMin = -maxDepth;
                    zMax = -minDepth;
                    break;
                case "south":
                    // Left = West
                    // Right = East
                    // Away = North
                    // To = South
                    api.Logger.Notification("sel.Face.Opposite.Code: {0}", sel.Face.Opposite.Code);
                    xMin = -maxWidth;
                    xMax = -minWidth;
                    yMin = minHeight;
                    yMax = maxHeight;
                    zMin = minDepth;
                    zMax = maxDepth;
                    break;
                case "east":
                    // Left = South
                    // Right = North
                    // Away = West
                    // To = East
                    api.Logger.Notification("sel.Face.Opposite.Code: {0}", sel.Face.Opposite.Code);
                    xMin = minDepth;
                    xMax = maxDepth;
                    yMin = minHeight;
                    yMax = maxHeight;
                    zMin = minWidth;
                    zMax = maxWidth;
                    break;
                case "west":
                    // Left = North
                    // Right = South
                    // Away = East
                    // To = Wast
                    api.Logger.Notification("sel.Face.Opposite.Code: {0}", sel.Face.Opposite.Code);
                    xMin = -maxDepth;
                    xMax = -minDepth;
                    yMin = minHeight;
                    yMax = maxHeight;
                    zMin = -maxWidth;
                    zMax = -minWidth;
                    break;
            }
        }

        List<BlockPos> GetBlocksToBreak(IWorldAccessor world, BlockSelection sel, int minX, int maxX, int minY,
            int maxY, int minZ, int maxZ, int mode)
        {
            List<BlockPos> positions = new List<BlockPos>();

            if (curMode == 6)
            {
                BlockPos dpos;
                switch (sel.Face.Axis)
                {
                    case EnumAxis.X:
                        dpos = sel.Position.AddCopy(0, -1, 1);
                        if (CanMultiBreak(world.BlockAccessor.GetBlock(dpos)))
                        {
                            positions.Add(dpos);
                        }
                        dpos = sel.Position.AddCopy(0, -1, -1);
                        if (CanMultiBreak(world.BlockAccessor.GetBlock(dpos)))
                        {
                            positions.Add(dpos);
                        }
                        dpos = sel.Position.AddCopy(0, 1, 1);
                        if (CanMultiBreak(world.BlockAccessor.GetBlock(dpos)))
                        {
                            positions.Add(dpos);
                        }
                        dpos = sel.Position.AddCopy(0, 1, -1);
                        if (CanMultiBreak(world.BlockAccessor.GetBlock(dpos)))
                        {
                            positions.Add(dpos);
                        }
                        break;
                    case EnumAxis.Y:
                        dpos = sel.Position.AddCopy(-1, 0, 1);
                        if (CanMultiBreak(world.BlockAccessor.GetBlock(dpos)))
                        {
                            positions.Add(dpos);
                        }
                        dpos = sel.Position.AddCopy(-1, 0, -1);
                        if (CanMultiBreak(world.BlockAccessor.GetBlock(dpos)))
                        {
                            positions.Add(dpos);
                        }
                        dpos = sel.Position.AddCopy(1, 0, 1);
                        if (CanMultiBreak(world.BlockAccessor.GetBlock(dpos)))
                        {
                            positions.Add(dpos);
                        }
                        dpos = sel.Position.AddCopy(1, 0, -1);
                        if (CanMultiBreak(world.BlockAccessor.GetBlock(dpos)))
                        {
                            positions.Add(dpos);
                        }
                        break;
                    case EnumAxis.Z:
                        dpos = sel.Position.AddCopy(-1, 1, 0);
                        if (CanMultiBreak(world.BlockAccessor.GetBlock(dpos)))
                        {
                            positions.Add(dpos);
                        }
                        dpos = sel.Position.AddCopy(-1, -1, 0);
                        if (CanMultiBreak(world.BlockAccessor.GetBlock(dpos)))
                        {
                            positions.Add(dpos);
                        }
                        dpos = sel.Position.AddCopy(1, 1, 0);
                        if (CanMultiBreak(world.BlockAccessor.GetBlock(dpos)))
                        {
                            positions.Add(dpos);
                        }
                        dpos = sel.Position.AddCopy(1, -1, 0);
                        if (CanMultiBreak(world.BlockAccessor.GetBlock(dpos)))
                        {
                            positions.Add(dpos);
                        }
                        break;
                }

                return positions;
            }

            for (int dx = minX; dx <= maxX; dx++)
            {
                for (int dy = minY; dy <= maxY; dy++)
                {
                    for (int dz = minZ; dz <= maxZ; dz++)
                    {
                        if (dx == 0 && dy == 0 && dz == 0) continue;

                        BlockPos pos = sel.Position.AddCopy(dx, dy, dz);

                        if (CanMultiBreak(world.BlockAccessor.GetBlock(pos)))
                        {
                            positions.Add(pos);
                        }
                    }
                }
            }
            return positions;
        }

        public void GetRangesFromToolMode(int toolMode)
        {
            switch (toolMode)
            {
                case 0: // 1x1x1
                    minWidth = 0;
                    maxWidth = 0;
                    minHeight = 0;
                    maxHeight = 0;
                    minDepth = 0;
                    maxDepth = 0;
                    break;
                case 1: // 1x2x1
                    minWidth = 0;
                    maxWidth = 0;
                    minHeight = -1;
                    maxHeight = 0;
                    minDepth = 0;
                    maxDepth = 0;
                    break;
                case 2: // 2x2x1
                    minWidth = 0;
                    maxWidth = 1;
                    minHeight = -1;
                    maxHeight = 0;
                    minDepth = 0;
                    maxDepth = 0;
                    break;
                case 3: // 3x3x1
                    minWidth = -1;
                    maxWidth = 1;
                    minHeight = -1;
                    maxHeight = 1;
                    minDepth = 0;
                    maxDepth = 0;
                    break;
                case 4: // 3x2x1
                    minWidth = -1;
                    maxWidth = 1;
                    minHeight = -1;
                    maxHeight = 0;
                    minDepth = 0;
                    maxDepth = 0;
                    break;
                case 5: // 1x3x3
                    minWidth = 0;
                    maxWidth = 0;
                    minHeight = -1;
                    maxHeight = 1;
                    minDepth = 0;
                    maxDepth = 2;
                    break;
            }
        }

        public override SkillItem[] GetToolModes(ItemSlot slot, IClientPlayer forPlayer, BlockSelection blockSel)
        {
            return toolModes;
        }

        public override int GetToolMode(ItemSlot slot, IPlayer byPlayer, BlockSelection blockSel)
        {
            return slot.Itemstack.Attributes.GetInt("toolMode");
        }

        public override void SetToolMode(ItemSlot slot, IPlayer byPlayer, BlockSelection blockSel, int toolMode)
        {
            slot.Itemstack.Attributes.SetInt("toolMode", toolMode);
        }


    }
}
