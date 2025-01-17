﻿using Vintagestory.API;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace TemporalHack
{
    public class AiTaskGetSeed : AiTaskBase
    {
        bool stop = false;
        Vec3d targetPos;
        EntityBehaviorProgram prog;
        float moveSpeed = 0.01f;
        float minDist = 1.3f;
        long failureTime;
        AssetLocation[] plantBlocks = new AssetLocation[] { new AssetLocation("game:sapling-*") };
        AssetLocation modSeed = new AssetLocation("wildfarming:wildseeds-*");

        public AiTaskGetSeed(EntityAgent entity) : base(entity)
        {
        }

        public override void LoadConfig(JsonObject taskConfig, JsonObject aiConfig)
        {
            base.LoadConfig(taskConfig, aiConfig);

            if (taskConfig["movespeed"] != null)
            {
                moveSpeed = taskConfig["movespeed"].AsFloat(0.02f);
            }

            if (taskConfig["plantBlocks"] != null)
            {
                plantBlocks = AssetLocation.toLocations(taskConfig["plantBlocks"].AsArray<string>(new string[] { "game:sapling-*" }));
            }
        }

        public override bool ShouldExecute()
        {
            if (cooldownUntilMs > entity.World.ElapsedMilliseconds || failureTime > entity.World.ElapsedMilliseconds) return false;
            prog = entity.GetBehavior<EntityBehaviorProgram>();
            if (prog == null) return false;
            if (prog.dormant || !entity.LeftHandItemSlot.Empty || prog?.CheckChest() != true || prog.workStack != null) return false;
            Block check = world.BlockAccessor.GetBlock(prog.workChest);
            if (check != null && check.Code.Path.Contains("translocatorchest"))
            {
                if (!TakeFromChest()) failureTime = entity.World.ElapsedMilliseconds + TemporalHackerConfig.Loaded.BotFailureCooldownMs; else prog.ConsumeEnergy(1);
                return false;
            }

            targetPos = prog.workChest.ToVec3d();


            return targetPos != null;
        }

        public override void StartExecute()
        {
            base.StartExecute();
            stop = false;
            pathTraverser.NavigateTo(targetPos, moveSpeed, minDist, OnGoalReached, OnStuck, false, 1000);
        }

        public override bool ContinueExecute(float dt)
        {
            return !stop;
        }

        public override void FinishExecute(bool cancelled)
        {
            base.FinishExecute(cancelled);
            targetPos = null;
            pathTraverser.Stop();
        }

        private void OnStuck()
        {
            stop = true;
            failureTime = entity.World.ElapsedMilliseconds + 60000;
        }

        private void OnGoalReached()
        {
            stop = true;
            if (!TakeFromChest()) failureTime = entity.World.ElapsedMilliseconds + TemporalHackerConfig.Loaded.BotFailureCooldownMs; else prog.ConsumeEnergy(1);

        }

        public bool TakeFromChest()
        {
            if (!prog.CheckChest()) return false;
            BlockEntityGenericTypedContainer bc = entity.World.BlockAccessor.GetBlockEntity(prog.workChest) as BlockEntityGenericTypedContainer;

            if (bc.Inventory != null)
            {
                foreach (ItemSlot slot in bc.Inventory)
                {
                    if (slot.Itemstack == null || !CanPlant(slot.Itemstack.Collectible)) continue;
                    slot.TryPutInto(entity.World, entity.LeftHandItemSlot, slot.StackSize);
                    if (!entity.LeftHandItemSlot.Empty && entity.LeftHandItemSlot.StackSize >= entity.LeftHandItemSlot.Itemstack.Collectible.MaxStackSize) return true;
                }
            }

            return !entity.LeftHandItemSlot.Empty;
        }

        public bool CanPlant(CollectibleObject seed)
        {
            if (seed == null) return false;
            if (seed is ItemPlantableSeed || seed.WildCardMatch(modSeed) || FindMatchCode(seed.Code)) return true;

            return false;
        }

        public bool FindMatchCode(AssetLocation needle)
        {
            if (needle == null) return false;

            foreach (AssetLocation hay in plantBlocks)
            {
                if (hay.Equals(needle)) return true;

                if (hay.IsWildCard && WildcardUtil.GetWildcardValue(hay, needle) != null) return true;
            }

            return false;
        }
    }
}
