using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace TemporalHack
{
    public class ItemProgCard : Item
    {
        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            if (blockSel != null)
            {
                if ((byEntity as EntityPlayer)?.Player != null && !api.World.Claims.TryAccess((byEntity as EntityPlayer)?.Player, blockSel.Position, EnumBlockAccessFlags.BuildOrBreak)) return;
                if (byEntity.Controls.Sprint)
                {                    

                    handling = EnumHandHandling.PreventDefault;
                    //if (byEntity.Api.Side == EnumAppSide.Client) return;
                    ITreeAttribute start;
                    Vec3d pos = blockSel.Position.ToVec3d();

                    if ((start = slot.Itemstack.TempAttributes.GetTreeAttribute("startPoint")) != null)
                    {
                        ITreeAttribute box = slot.Itemstack.Attributes.GetOrAddTreeAttribute("workArea");

                        box.SetDouble("x1", start.GetDouble("x1") > pos.X ? pos.X : start.GetDouble("x1"));
                        box.SetDouble("y1", start.GetDouble("y1") > pos.Y ? pos.Y : start.GetDouble("y1"));
                        box.SetDouble("z1", start.GetDouble("z1") > pos.Z ? pos.Z : start.GetDouble("z1"));
                        box.SetDouble("x2", (start.GetDouble("x1") > pos.X ? start.GetDouble("x1") : pos.X) + 1);
                        box.SetDouble("y2", (start.GetDouble("y1") > pos.Y ? start.GetDouble("y1") : pos.Y));
                        box.SetDouble("z2", (start.GetDouble("z1") > pos.Z ? start.GetDouble("z1") : pos.Z) + 1);

                        slot.Itemstack.TempAttributes.RemoveAttribute("startPoint");
                        ((byEntity as EntityPlayer)?.Player as IServerPlayer)?.SendMessage(GlobalConstants.InfoLogChatGroup, Lang.Get("temporalhack:work-area"), EnumChatType.Notification);
                    }
                    else
                    {
                        slot.Itemstack.Attributes.RemoveAttribute("workArea");

                        start = slot.Itemstack.TempAttributes.GetOrAddTreeAttribute("startPoint");
                        start.SetDouble("x1", pos.X);
                        start.SetDouble("y1", pos.Y);
                        start.SetDouble("z1", pos.Z);

                        ((byEntity as EntityPlayer)?.Player as IServerPlayer)?.SendMessage(GlobalConstants.InfoLogChatGroup, Lang.Get("temporalhack:work-start"), EnumChatType.Notification);
                    }

                    return;
                }

                if (byEntity.Controls.Sneak)
                {
                    handling = EnumHandHandling.PreventDefault;
                    //if (byEntity.Api.Side == EnumAppSide.Client) return;
                    ITreeAttribute tree;
                    BlockEntity sel = api.World.BlockAccessor.GetBlockEntity(blockSel.Position);

                    if (sel is BlockEntityBarrel)
                    {
                        tree = slot.Itemstack.Attributes.GetOrAddTreeAttribute("workBarrel");
                        tree.SetInt("x", blockSel.Position.X);
                        tree.SetInt("y", blockSel.Position.Y);
                        tree.SetInt("z", blockSel.Position.Z);

                        ((byEntity as EntityPlayer)?.Player as IServerPlayer)?.SendMessage(GlobalConstants.InfoLogChatGroup, Lang.Get("temporalhack:work-barrel"), EnumChatType.Notification);
                    }
                    else if (sel is BlockEntityGenericTypedContainer)
                    {
                        //System.Diagnostics.Debug.WriteLine(blockSel.Position.X + "," + blockSel.Position.Y + "," + blockSel.Position.Z);
                        tree = slot.Itemstack.Attributes.GetOrAddTreeAttribute("workChest");
                        tree.SetInt("x", blockSel.Position.X);
                        tree.SetInt("y", blockSel.Position.Y);
                        tree.SetInt("z", blockSel.Position.Z);

                        ((byEntity as EntityPlayer)?.Player as IServerPlayer)?.SendMessage(GlobalConstants.InfoLogChatGroup, Lang.Get("temporalhack:work-chest"), EnumChatType.Notification);
                    }

                    return;
                }
            }
            else
            {
                if (byEntity.Controls.Sprint && !(entitySel?.Entity is EntityLocust))
                {
                    handling = EnumHandHandling.PreventDefault;
                    if (slot.Itemstack.Attributes.GetBool("recordMode"))
                    {
                        slot.Itemstack.Attributes.SetBool("recordMode", false);
                        ((byEntity as EntityPlayer)?.Player as IServerPlayer)?.SendMessage(GlobalConstants.InfoLogChatGroup, Lang.Get("temporalhack:work-itemcacheoff"), EnumChatType.Notification);
                    }
                    else
                    {
                        slot.Itemstack.Attributes.SetBool("recordMode", true);
                        ((byEntity as EntityPlayer)?.Player as IServerPlayer)?.SendMessage(GlobalConstants.InfoLogChatGroup, Lang.Get("temporalhack:work-itemcacheon"), EnumChatType.Notification);
                    }
                }
            }

        }

        public override void OnHeldAttackStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandHandling handling)
        {
            if (!byEntity.Controls.Sneak) return;
            handling = EnumHandHandling.PreventDefault;
            ITreeAttribute box = slot.Itemstack.Attributes.GetTreeAttribute("workArea");
            //ITreeAttribute chest = slot.Itemstack.Attributes.GetTreeAttribute("workChest");
            //ITreeAttribute barrel = slot.Itemstack.Attributes.GetTreeAttribute("workBarrel");

            if (box != null)
            {
                BlockPos min = new Vec3d(box.GetDouble("x1"), box.GetDouble("y1"), box.GetDouble("z1")).AsBlockPos;
                BlockPos max = new Vec3d(box.GetDouble("x2"), box.GetDouble("y2") + 1, box.GetDouble("z2")).AsBlockPos;
                List<BlockPos> blocks = new List<BlockPos>() { min, max };
                List<int> colors = new List<int>() { ColorUtil.ColorFromRgba(215, 94, 94, 64), ColorUtil.ColorFromRgba(215, 94, 94, 64) };

                api.World.HighlightBlocks((byEntity as EntityPlayer)?.Player, 56, blocks, colors, EnumHighlightBlocksMode.Absolute, EnumHighlightShape.Cube);
                api.World.RegisterCallback((dt) => api.World.HighlightBlocks((byEntity as EntityPlayer)?.Player, 56, new List<BlockPos>(), new List<int>()), 3000);
            }

        }

        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);
            if (inSlot.Itemstack.TempAttributes.HasAttribute("startPoint")) dsc.AppendLine(Lang.Get("temporalhack:work-start"));
            if (inSlot.Itemstack.Attributes.HasAttribute("workArea")) dsc.AppendLine(Lang.Get("temporalhack:work-area")); else dsc.AppendLine(Lang.Get("temporalhack:work-noarea"));

            if (inSlot.Itemstack.Attributes.HasAttribute("workChest")) dsc.AppendLine(Lang.Get("temporalhack:work-chest")); else dsc.AppendLine(Lang.Get("temporalhack:work-nochest"));
            if (inSlot.Itemstack.Attributes.HasAttribute("workBarrel")) dsc.AppendLine(Lang.Get("temporalhack:work-barrel")); else dsc.AppendLine(Lang.Get("temporalhack:work-nobarrel"));
            if (inSlot.Itemstack.Attributes.HasAttribute("cacheName")) dsc.AppendLine(Lang.Get("temporalhack:work-itemcached", inSlot.Itemstack.Attributes.GetString("cacheName"))); else dsc.AppendLine(Lang.Get("temporalhack:work-noitemcached"));
            if (inSlot.Itemstack.Attributes.GetBool("recordMode")) dsc.AppendLine(Lang.Get("temporalhack:work-itemcacheon"));
        }

        public override int GetMergableQuantity(ItemStack sinkStack, ItemStack sourceStack, EnumMergePriority priority)
        {
            if (priority == EnumMergePriority.DirectMerge && sinkStack.Attributes.GetBool("recordMode"))
            {
                sinkStack.Attributes.SetBool("recordMode", false);
                sinkStack.Attributes.SetString("cacheName", sourceStack.GetName());
                sinkStack.Attributes.SetItemstack("workStack", sourceStack);
            }

            return base.GetMergableQuantity(sinkStack, sourceStack, priority);
        }

        WorldInteraction[] interactions;

        public override void OnLoaded(ICoreAPI api)
        {
            if (api.Side != EnumAppSide.Client) return;
            ICoreClientAPI capi = api as ICoreClientAPI;

            interactions = ObjectCacheUtil.GetOrCreate(api, "memoryGearInteractions", () =>
            {
                List<ItemStack> stacks = new List<ItemStack>();
                List<ItemStack> chests = new List<ItemStack>();
                List<ItemStack> barrel = new List<ItemStack>();

                foreach (Item item in api.World.Items)
                {
                    if (item.Code == null) continue;

                    if (item is ItemDeployBot)
                    {
                        stacks.Add(new ItemStack(item));
                    }
                }

                foreach (Block block in api.World.Blocks)
                {
                    if (block.Code == null) continue;

                    if (block is BlockBarrel)
                    {
                        barrel.Add(new ItemStack(block));
                    }

                    if (block.EntityClass == "GenericTypedContainer")
                    {
                        chests.Add(new ItemStack(block));
                    }
                }

                return new WorldInteraction[]
                {
                    new WorldInteraction()
                    {
                        ActionLangCode = "temporalhack:heldhelp-viewarea",
                        MouseButton = EnumMouseButton.Left,
                        HotKeyCode = "sneak"
                    },

                    new WorldInteraction()
                    {
                        ActionLangCode = "temporalhack:heldhelp-record",
                        MouseButton = EnumMouseButton.Right,
                        HotKeyCode = "sprint"
                    },

                    new WorldInteraction()
                    {
                        ActionLangCode = "temporalhack:heldhelp-select",
                        MouseButton = EnumMouseButton.Right,
                        HotKeyCode = "sprint"
                    },

                    new WorldInteraction()
                    {
                        ActionLangCode = "temporalhack:heldhelp-barrel",
                        MouseButton = EnumMouseButton.Right,
                        HotKeyCode = "sneak",
                        Itemstacks = barrel.ToArray()
                    },

                    new WorldInteraction()
                    {
                        ActionLangCode = "temporalhack:heldhelp-chests",
                        MouseButton = EnumMouseButton.Right,
                        HotKeyCode = "sneak",
                        Itemstacks = chests.ToArray()
                    },

                    new WorldInteraction()
                    {
                        ActionLangCode = "temporalhack:heldhelp-upload",
                        MouseButton = EnumMouseButton.Right,
                        HotKeyCode = "sneak",
                        Itemstacks = stacks.ToArray()
                    },
                };
            });
        }

        public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot inSlot)
        {
            return interactions;
        }
    }
}
