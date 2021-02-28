using System;
using System.Collections.Generic;
using System.Text;
using Vintagestory.API;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace TemporalHack
{
    public class EntityBehaviorProgram : EntityBehavior
    {
        ITreeAttribute maintree
        {
            get { return entity.WatchedAttributes.GetOrAddTreeAttribute("reprog"); }
        }
        public Cuboidd workArea
        {
            get
            {
                if (maintree.GetTreeAttribute("workArea") == null) return null;
                return place;
            }
            set { place = value; }
        }
        public BlockPos workChest
        {
            get
            {
                if (maintree.GetTreeAttribute("workChest") == null) return null;
                return chest;
            }
            set { chest = value; }
        }
        public BlockPos workBarrel
        {
            get
            {
                if (maintree.GetTreeAttribute("workBarrel") == null) return null;
                return barrel;
            }
            set { barrel = value; }
        }
        public ItemStack workStack;
        public EntityAgent agent
        {
            get { return entity as EntityAgent; }
        }
        public IPlayer owner
        {
            get {return entity.World.PlayerByUid(maintree.GetString("ownerId")); }
        }
        public bool relaxed
        {
            get { return maintree.GetBool("relaxed"); }
            set { maintree.SetBool("relaxed", value); entity.WatchedAttributes.MarkPathDirty("reprog"); }
        }
        bool nochest;
        bool nobarrel;
        public bool dormant
        {
            get { return maintree.GetInt("workEnergy") <= 0; }
        }
        InventoryGeneric workInv;
        BlockPos chest;
        BlockPos barrel;
        Cuboidd place;
        bool restricted;
        float timer;
        bool invLocked;
        ICachingBlockAccessor blockCheck;
        int energy
        {
            get { return maintree.GetInt("workEnergy"); }
            set { maintree.SetInt("workEnergy", GameMath.Clamp(value, 0, TemporalHackerConfig.Loaded.BotMaximumEnergy)); entity.WatchedAttributes.MarkPathDirty("reprog"); }
        }
        public int getEnergy
        {
            get { return energy; }
        }

        public BlockPos workPoint
        {
            get
            {
                if (workArea != null) return ((workArea.Start + workArea.End)/2).AsBlockPos;
                return null;
            }
        }

        public override void Initialize(EntityProperties properties, JsonObject attributes)
        {
            base.Initialize(properties, attributes);

            blockCheck = entity.World.GetCachingBlockAccessor(false, false);

            if (attributes.IsTrue("relaxed")) relaxed = true;
            if (attributes.IsTrue("nochest")) nochest = true;
            if (attributes.IsTrue("nobarrel")) nobarrel = true;
            if (attributes.IsTrue("invLocked")) invLocked = true;

            if (attributes["restrictItem"] != null)
            {
                JsonItemStack prestack = attributes["restrictItem"].AsObject<JsonItemStack>();
                if (prestack != null)
                {
                    prestack.Resolve(entity.World, "Locust Program");
                    if (prestack.ResolvedItemstack != null)
                    {
                        maintree.SetItemstack("workStack", prestack.ResolvedItemstack);
                        maintree.SetBool("restrictItem", true);
                        restricted = true;
                    }
                }
            }

            ITreeAttribute area = maintree.GetTreeAttribute("workArea");
            ITreeAttribute box = maintree.GetTreeAttribute("workChest");
            ITreeAttribute barrel = maintree.GetTreeAttribute("workBarrel");

            
            workStack = maintree.GetItemstack("workStack");
            workStack?.ResolveBlockOrItem(entity.World);
            if (entity.Api.Side == EnumAppSide.Client && maintree.GetBool("restrictItem")) restricted = true;

            if (area != null)
            {
                workArea = cuboiddFromTree(area);
            }
            if (box != null) workChest = new BlockPos(box.GetInt("x"), box.GetInt("y"), box.GetInt("z"));
            if (barrel != null ) workBarrel = new BlockPos(barrel.GetInt("x"), barrel.GetInt("y"), barrel.GetInt("z"));

            ITreeAttribute tree = maintree["workInv"] as ITreeAttribute;
            if (tree != null) workInv.FromTreeAttributes(tree);
        }

        public override void OnGameTick(float deltaTime)
        {
            base.OnGameTick(deltaTime);

            timer += deltaTime;

            if (timer >= 10)
            {
                ConsumeEnergy(1);
                timer = 0;
            }
        }

        public override void OnInteract(EntityAgent byEntity, ItemSlot itemslot, Vec3d hitPosition, EnumInteractMode mode, ref EnumHandling handled)
        {
            base.OnInteract(byEntity, itemslot, hitPosition, mode, ref handled);

            IPlayer byPlayer = byEntity.World.PlayerByUid((byEntity as EntityPlayer).PlayerUID);
            if (owner != null && owner != byPlayer) return;

            if (mode == EnumInteractMode.Interact)
            {
                if (byEntity.Controls.Sneak && byEntity.Controls.Sprint && !invLocked)
                {
                    workInv[0].TryFlipWith(itemslot);
                    return;
                }
                if (itemslot.Itemstack?.Collectible is ItemHammer)
                {
                    if (entity.GetBehavior<EntityBehaviorHealth>()?.Health < entity.GetBehavior<EntityBehaviorHealth>()?.MaxHealth)
                    {
                        itemslot.Itemstack.Collectible.DamageItem(entity.World, byEntity, itemslot, 5);
                        entity.World.PlaySoundAt(new AssetLocation("game:sounds/effect/anvilhit3.ogg"), entity, byPlayer);
                        entity.ReceiveDamage(new DamageSource() { Type = EnumDamageType.Heal }, 1);
                        return;
                    }
                }
                if (byEntity.Controls.Sprint && !restricted && itemslot?.Itemstack?.Collectible is ItemProgCard)
                {
                    ItemStack stack = itemslot.Itemstack.Attributes.GetItemstack("workStack");
                    if (stack == null) return;
                    stack.ResolveBlockOrItem(entity.World);

                    if (stack != null)
                    {
                        workInv.DropAll(entity.ServerPos.XYZ);
                        workStack = stack;
                        maintree.SetItemstack("workStack", workStack);
                        (byPlayer as IServerPlayer)?.SendMessage(GlobalConstants.InfoLogChatGroup, Lang.Get("temporalhack:work-itemprogram"), EnumChatType.Notification);
                    }

                    return;
                }
                else if (byEntity.Controls.Sprint && itemslot.Empty && byPlayer != null && workArea != null)
                {
                    BlockPos min = workArea.Start.AsBlockPos;
                    BlockPos max = workArea.End.AsBlockPos;
                    max.Y += 1;
                    List<BlockPos> blocks = new List<BlockPos>() { min, max };
                    List<int> colors = new List<int>() { ColorUtil.ColorFromRgba(215, 94, 94, 64), ColorUtil.ColorFromRgba(215, 94, 94, 64) };

                    entity.Api.World.HighlightBlocks(byPlayer, 56, blocks, colors, EnumHighlightBlocksMode.Absolute, EnumHighlightShape.Cube);
                    entity.Api.World.RegisterCallback((dt) => entity.Api.World.HighlightBlocks(byPlayer, 56, new List<BlockPos>(), new List<int>()), 3000);
                }

                if (byEntity.Controls.Sneak && itemslot?.Itemstack?.Collectible is ItemProgCard)
                {
                    ITreeAttribute area = itemslot.Itemstack.Attributes.GetTreeAttribute("workArea");
                    ITreeAttribute box = null;
                    ITreeAttribute barrel = null;

                    if (!nochest) box = itemslot.Itemstack.Attributes.GetTreeAttribute("workChest");
                    if (!nobarrel) barrel = itemslot.Itemstack.Attributes.GetTreeAttribute("workBarrel");

                    StopAll();

                    if (area != null)
                    {
                        maintree["workArea"] = area.Clone();
                        workArea = cuboiddFromTree(area);
                    }

                    if (box != null)
                    {
                        maintree["workChest"] = box.Clone();
                        workChest = new BlockPos(box.GetInt("x"), box.GetInt("y"), box.GetInt("z"));
                    }

                    if (barrel != null)
                    {
                        maintree["workBarrel"] = barrel.Clone();
                        workBarrel = new BlockPos(barrel.GetInt("x"), barrel.GetInt("y"), barrel.GetInt("z"));
                    }

                    if (area != null || box != null || barrel != null) (byPlayer as IServerPlayer)?.SendMessage(GlobalConstants.InfoLogChatGroup, Lang.Get("temporalhack:work-program"), EnumChatType.Notification);
                }
                else if (byEntity.Controls.Sneak && itemslot.Empty)
                {
                    AssetLocation item = new AssetLocation(entity.Code.Domain, "deploybot-" + entity.Code.Path);
                    if (item != null)
                    {
                        EntityBehaviorHealth bh = entity.GetBehavior<EntityBehaviorHealth>();
                        ItemStack stack = new ItemStack(entity.World.GetItem(item));

                        stack.Attributes.SetInt("workEnergy", energy);
                        if (bh != null)
                        {
                            stack.Attributes.SetFloat("workHealth", bh.MaxHealth - bh.Health);
                        }

                        byEntity.World.SpawnItemEntity(stack, entity.SidedPos.XYZ);


                        workInv.DropAll(entity.ServerPos.XYZ);
                        entity.Die(EnumDespawnReason.Removed);
                    }
                }
                else if (byEntity.Controls.Sneak && itemslot?.Itemstack?.Collectible?.CombustibleProps?.BurnDuration != null)
                {
                    if (itemslot.Itemstack.Collectible.CombustibleProps.BurnTemperature >= 200)
                    {
                        AddEnergy((itemslot.Itemstack.Collectible.CombustibleProps.BurnTemperature/100) * (int)itemslot.Itemstack.Collectible.CombustibleProps.BurnDuration);
                        itemslot.TakeOut(1);
                        entity.World.PlaySoundAt(new AssetLocation("game:sounds/effect/extinguish2.ogg"), entity, byPlayer);
                    }
                }
            }
            else
            {
                if (byEntity.Controls.Sneak)
                {
                    StopAll();
                    workStack = null;
                    workInv.DropAll(entity.ServerPos.XYZ);
                    RemoveArea();
                    RemoveBarrel();
                    RemoveChest();
                    (byPlayer as IServerPlayer)?.SendMessage(GlobalConstants.InfoLogChatGroup, Lang.Get("temporalhack:work-memoryloss"), EnumChatType.Notification);
                }
            }
        }

        public override void GetInfoText(StringBuilder infotext)
        {
            base.GetInfoText(infotext);

            if (owner != null) infotext.AppendLine(Lang.Get("temporalhack:work-boss", owner.PlayerName)); else infotext.AppendLine(Lang.Get("temporalhack:work-noboss"));
            infotext.AppendLine(Lang.Get("temporalhack:work-energy", energy, TemporalHackerConfig.Loaded.BotMaximumEnergy));
            if (workArea != null) infotext.AppendLine(Lang.Get("temporalhack:work-area")); else infotext.AppendLine(Lang.Get("temporalhack:work-noarea"));
            if (!nochest)
            {
                if (workChest != null) infotext.AppendLine(Lang.Get("temporalhack:work-chest")); else infotext.AppendLine(Lang.Get("temporalhack:work-nochest"));
            }
            if (!nobarrel)
            {
                if (workBarrel != null) infotext.AppendLine(Lang.Get("temporalhack:work-barrel")); else infotext.AppendLine(Lang.Get("temporalhack:work-nobarrel"));
            }
            if (!relaxed)
            {
                if (workStack != null) infotext.AppendLine(Lang.Get("temporalhack:work-stack", workStack.GetName())); else infotext.AppendLine(Lang.Get("temporalhack:work-nostack"));
            }
            if (workInv[0]?.Itemstack != null) infotext.AppendLine(Lang.Get("temporalhack:work-carrying", workInv[0].StackSize, workInv[0].Itemstack.GetName()));
        }

        public static Cuboidd cuboiddFromTree(ITreeAttribute tree)
        {
            return new Cuboidd(tree.GetDouble("x1"), tree.GetDouble("y1"), tree.GetDouble("z1"), tree.GetDouble("x2"), tree.GetDouble("y2"), tree.GetDouble("z2"));
        }

        public EntityBehaviorProgram(Entity entity) : base(entity)
        {
            workInv = new InventoryGeneric(1, "workslot-" + entity.EntityId, entity.Api, (id, inv) => new ItemSlotWork(this, inv));
            workInv.SlotModified += WorkInv_SlotModified;

            agent.LeftHandItemSlot = workInv[0];
        }

        private void WorkInv_SlotModified(int slotid)
        {
            ITreeAttribute tree = new TreeAttribute();
            maintree["workInv"] = tree;
            entity.WatchedAttributes.MarkPathDirty("reprog");
            workInv.ToTreeAttributes(tree);

            if (entity.Api is ICoreServerAPI sapi)
            {
                sapi.Network.BroadcastEntityPacket(entity.EntityId, 2450, SerializerUtil.ToBytes((w) => tree.ToBytes(w)));
            }
        }

        public override void OnReceivedServerPacket(int packetid, byte[] data, ref EnumHandling handled)
        {
            if (packetid == 2450)
            {
                TreeAttribute tree = new TreeAttribute();
                SerializerUtil.FromBytes(data, (r) => tree.FromBytes(r));
                workInv.FromTreeAttributes(tree);
            }
        }

        public override void OnEntityDeath(DamageSource damageSourceForDeath)
        {
            if (entity.World.Side == EnumAppSide.Server)
            {
                workInv.DropAll(entity.ServerPos.XYZ);
            }

            base.OnEntityDeath(damageSourceForDeath);
        }

        public void ConsumeEnergy(int nrg)
        {
            energy -= nrg * TemporalHackerConfig.Loaded.BotEnergyConsumption;
        }

        public void AddEnergy(int nrg)
        {
            energy += nrg * TemporalHackerConfig.Loaded.BotEnergyGain;
        }

        public bool DumpInChest()
        {
            if (workChest == null) return false;
            BlockEntityGenericTypedContainer bc = entity.World.BlockAccessor.GetBlockEntity(workChest) as BlockEntityGenericTypedContainer;
            //System.Diagnostics.Debug.WriteLine(workChest.X + "," + workChest.Y + "," + workChest.Z);
            if (bc == null)
            {
                workChest = null;
                RemoveChest();
                return false;
            }

            if (bc.Inventory != null)
            {
                foreach (ItemSlot slot in bc.Inventory)
                {
                    workInv[0].TryPutInto(entity.World, slot, workInv[0].StackSize);
                    if (workInv[0].Empty) return true;
                }
            }

            return false;
        }

        public bool TakeFromChest()
        {
            if (workChest == null || workStack == null) return false;
            BlockEntityGenericTypedContainer bc = entity.World.BlockAccessor.GetBlockEntity(workChest) as BlockEntityGenericTypedContainer;
            
            if (bc == null)
            {
                workChest = null;
                RemoveChest();
                return false;
            }

            if (bc.Inventory != null)
            {
                foreach (ItemSlot slot in bc.Inventory)
                {
                    if (!slot.Empty && slot.Itemstack.Equals(entity.World, workStack, GlobalConstants.IgnoredStackAttributes)) slot.TryPutInto(entity.World, workInv[0], slot.StackSize);
                    if (!workInv[0].Empty && workInv[0].StackSize >= workInv[0].Itemstack.Collectible.MaxStackSize) return true;
                }
            }

            return !workInv[0].Empty;
        }

        public bool TakeAnyFromChest()
        {
            if (workChest == null) return false;
            BlockEntityGenericTypedContainer bc = entity.World.BlockAccessor.GetBlockEntity(workChest) as BlockEntityGenericTypedContainer;

            if (bc == null)
            {
                workChest = null;
                RemoveChest();
                return false;
            }

            if (bc.Inventory != null)
            {
                foreach (ItemSlot slot in bc.Inventory)
                {
                    if (!slot.Empty) slot.TryPutInto(entity.World, workInv[0], slot.StackSize);
                    if (!workInv[0].Empty && workInv[0].StackSize >= workInv[0].Itemstack.Collectible.MaxStackSize) return true;
                }
            }

            return !workInv[0].Empty;
        }

        public bool PutInBarrel(ItemSlot liq)
        {
            if (liq?.Itemstack?.ItemAttributes?["waterTightContainerProps"]?["itemsPerLitre"]?.AsInt(0) == null || workBarrel == null) return false;
            BlockEntityBarrel bb = entity.World.BlockAccessor.GetBlockEntity(workBarrel) as BlockEntityBarrel;
            if (bb == null)
            {
                workBarrel = null;
                RemoveBarrel();
                return false;
            }

            if (bb.Sealed) return false;

            int maxitems = bb.CapacityLitres * liq.Itemstack.ItemAttributes["waterTightContainerProps"]["itemsPerLitre"].AsInt(0);
            if (maxitems < 1) return false;
            int put = Math.Min(liq.StackSize, maxitems - bb.Inventory[1].StackSize);
            if (!bb.Inventory[1].Empty && liq.Itemstack.Equals(entity.World, bb.Inventory[1].Itemstack, GlobalConstants.IgnoredStackAttributes))
            {
                bb.Inventory[1].Itemstack.StackSize = bb.Inventory[1].StackSize + put;
                liq.TakeOut(put);
            }
            else liq.TryPutInto(entity.World, bb.Inventory[1], put);

            bb.MarkDirty();

            return liq.Empty;
        }

        public void RemoveChest()
        {
            maintree.RemoveAttribute("workChest");
            entity.WatchedAttributes.MarkPathDirty("reprog");
        }

        public void RemoveBarrel()
        {
            maintree.RemoveAttribute("workBarrel");
            entity.WatchedAttributes.MarkPathDirty("reprog");
        }

        public void RemoveArea()
        {
            maintree.RemoveAttribute("workArea");
            entity.WatchedAttributes.MarkPathDirty("reprog");
        }

        public bool CheckChest()
        {
            if (workChest == null) return false;

            blockCheck.Begin();
            Block check = blockCheck.GetBlock(workChest);
            if (!blockCheck.LastChunkLoaded) return false;
            if (entity.World.BlockAccessor.GetBlockEntity(workChest) is BlockEntityGenericTypedContainer) return true;

            RemoveChest();
            return false;
        }

        public bool CheckAltChest()
        {
            if (workPoint == null) return false;

            blockCheck.Begin();
            Block check = blockCheck.GetBlock(workPoint);
            if (!blockCheck.LastChunkLoaded) return false;
            if (entity.World.BlockAccessor.GetBlockEntity(workPoint) is BlockEntityGenericTypedContainer) return true;

            return false;
        }

        public bool DumpInAltChest()
        {
            if (workPoint == null) return false;
            BlockEntityGenericTypedContainer bc = entity.World.BlockAccessor.GetBlockEntity(workPoint) as BlockEntityGenericTypedContainer;

            if (bc == null)
            {
                workChest = null;
                RemoveChest();
                return false;
            }

            if (bc.Inventory != null)
            {
                foreach (ItemSlot slot in bc.Inventory)
                {
                    workInv[0].TryPutInto(entity.World, slot, workInv[0].StackSize);
                    if (workInv[0].Empty) return true;
                }
            }

            return false;
        }

        public bool CheckBarrel()
        {
            if (workBarrel == null) return false;

            blockCheck.Begin();
            Block check = blockCheck.GetBlock(workBarrel);
            if (!blockCheck.LastChunkLoaded) return false;
            if (entity.World.BlockAccessor.GetBlockEntity(workBarrel) is BlockEntityBarrel) return true;

            RemoveBarrel();
            return false;
        }

        public bool CheckArea()
        {
            if (workArea == null) return false;

            if (workPoint == null)
            {
                blockCheck.Begin();
                blockCheck.GetBlock(workArea.Start.AsBlockPos);
                if (!blockCheck.LastChunkLoaded) return false;
                blockCheck.GetBlock(workArea.End.AsBlockPos);
                if (!blockCheck.LastChunkLoaded) return false;
            }
            else
            {
                blockCheck.Begin();
                blockCheck.GetBlock(workArea.Start.AsBlockPos);
                if (!blockCheck.LastChunkLoaded) return false;
            }

            return true;
        }

        public void StopAll()
        {
            AiTaskManager mang = entity.GetBehavior<EntityBehaviorTaskAI>()?.taskManager;
            if (mang != null)
            {
                mang.StopTask(typeof(AiTaskHarvest));
                mang.StopTask(typeof(AiTaskPlant));
                mang.StopTask(typeof(AiTaskSqueezeHoney));
                mang.StopTask(typeof(AiTaskMilk));
                mang.StopTask(typeof(AiTaskToChest));

                mang.StopTask(typeof(AiTaskFromChest));
                mang.StopTask(typeof(AiTaskSeekItem));
                mang.StopTask(typeof(AiTaskToBarrel));
                mang.StopTask(typeof(AiTaskGetFuel));
                mang.StopTask(typeof(AiTaskRefuel));

                mang.StopTask(typeof(AiTaskGetSeed));
                mang.StopTask(typeof(AiTaskSeekItemAny));
                mang.StopTask(typeof(AiTaskProgMeleeAttack));
                mang.StopTask(typeof(AiTaskSeekEntity));
                mang.StopTask(typeof(AiTaskQuarry));

                mang.StopTask(typeof(AiTaskStayCloseToOwner));
                mang.StopTask(typeof(AiTaskPathBuilder));
                mang.StopTask(typeof(AiTaskQuarry));
                mang.StopTask(typeof(AiTaskToAltChest));
                mang.StopTask(typeof(AiTaskProgRangedAttack));

                mang.StopTask(typeof(AiTaskForm));
                mang.StopTask(typeof(AiTaskToPoint));
                mang.StopTask(typeof(AiTaskGetItem));
                mang.StopTask(typeof(AiTaskAnyFromChest));
                mang.StopTask(typeof(AiTaskGetTool));
            }
        }

        public override string PropertyName()
        {
            return "program";
        }
    }
}
