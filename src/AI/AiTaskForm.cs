using System;
using Vintagestory.API;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace TemporalHack
{
    public class AiTaskForm : AiTaskBase
    {
        bool stop = false;
        Vec3d targetPos;
        EntityBehaviorProgram prog;
        float moveSpeed = 0.01f;
        float minDist = 1.3f;
        long failureTime;

        public AiTaskForm(EntityAgent entity) : base(entity)
        {
        }

        public override void LoadConfig(JsonObject taskConfig, JsonObject aiConfig)
        {
            base.LoadConfig(taskConfig, aiConfig);

            if (taskConfig["movespeed"] != null)
            {
                moveSpeed = taskConfig["movespeed"].AsFloat(0.02f);
            }
        }

        public override bool ShouldExecute()
        {
            if (cooldownUntilMs > entity.World.ElapsedMilliseconds || failureTime > entity.World.ElapsedMilliseconds) return false;
            prog = entity.GetBehavior<EntityBehaviorProgram>();
            if (prog == null || prog.dormant) return false;
            if (!IsCraftingSurface()) return false;

            targetPos = prog.workPoint.ToVec3d();
            

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
            failureTime = entity.World.ElapsedMilliseconds + TemporalHackerConfig.Loaded.BotFailureCooldownMs;
        }

        private void OnGoalReached()
        {
            stop = true;
            if (!Craft()) failureTime = entity.World.ElapsedMilliseconds + TemporalHackerConfig.Loaded.BotFailureCooldownMs;

        }

        private bool IsCraftingSurface()
        {
            if (prog.workPoint == null) return false;

            BlockEntity surface = world.BlockAccessor.GetBlockEntity(prog.workPoint);
            
            if ((surface is BlockEntityClayForm && entity.LeftHandItemSlot.Itemstack?.Collectible is ItemClay) || surface is BlockEntityKnappingSurface) return true;

            return false;
        }

        private bool Craft()
        {
            if (prog.workPoint == null) return false;
            BlockEntity surface = world.BlockAccessor.GetBlockEntity(prog.workPoint);
            if (surface == null) return false;

            if (surface is BlockEntityClayForm && entity.LeftHandItemSlot.Itemstack?.Collectible is ItemClay)
            {
                BlockEntityClayForm cf = surface as BlockEntityClayForm;
                int nrg = 0;

                if (cf.SelectedRecipe != null)
                {
                    for (int y = 0; y < 16; y++)
                    {
                        for (int x = 0; x < 16; x++)
                        {
                            for (int z = 0; z < 16; z++)
                            {
                                if (!cf.Voxels[x, y, z] && cf.Voxels[x, y, z] != cf.SelectedRecipe.Voxels[x, y, z])
                                {
                                    if (cf.AvailableVoxels == 0)
                                    {
                                        if (entity.LeftHandItemSlot.Empty) { cf.MarkDirty(true); return true; }

                                        entity.LeftHandItemSlot.TakeOut(1);
                                        entity.LeftHandItemSlot.MarkDirty();
                                        nrg += 5;
                                        cf.AvailableVoxels += 25;
                                    }

                                    cf.Voxels[x, y, z] = cf.SelectedRecipe.Voxels[x, y, z];
                                    cf.AvailableVoxels -= 1;
                                }
                                else if (cf.Voxels[x, y, z] != cf.SelectedRecipe.Voxels[x, y, z]) cf.Voxels[x, y, z] = cf.SelectedRecipe.Voxels[x, y, z];
                            }
                        }
                    }
                }
                else return false;

                cf.SelectedRecipe.Resolve(world, "locust clay forming");
                ItemStack result = cf.SelectedRecipe.Output.ResolvedItemstack.Clone();

                if (result.StackSize == 1 && result.Class == EnumItemClass.Block) world.BlockAccessor.SetBlock(result.Block.BlockId, prog.workPoint);
                else
                {
                    int tries = 500;
                    while (result.StackSize > 0 && tries-- > 0)
                    {
                        ItemStack dropStack = result.Clone();
                        dropStack.StackSize = Math.Min(result.StackSize, result.Collectible.MaxStackSize);
                        result.StackSize -= dropStack.StackSize;

                        world.SpawnItemEntity(dropStack, prog.workPoint.ToVec3d().Add(0.5, 0.5, 0.5));
                    }

                    if (tries <= 1)
                    {
                        world.Logger.Error("Tried to drop finished clay forming item but failed after 500 times?! Gave up doing so. Out stack was " + result);
                    }

                    world.BlockAccessor.SetBlock(0, prog.workPoint);
                }

                prog.ConsumeEnergy(nrg);
                return true;
            }

            if (surface is BlockEntityKnappingSurface)
            {
                BlockEntityKnappingSurface ks = surface as BlockEntityKnappingSurface;
                int nrg = 0;
                if (ks.SelectedRecipe == null) return false;

                for (int x = 0; x < 16; x++)
                {
                    for (int z = 0; z < 16; z++)
                    {
                        if (ks.Voxels[x,z] != ks.SelectedRecipe.Voxels[x, 0, z])
                        {
                            ks.Voxels[x, z] = ks.SelectedRecipe.Voxels[x, 0, z];
                            nrg += 1;
                        }
                    }
                }

                ks.SelectedRecipe.Resolve(world, "locust knapping");
                ItemStack result = ks.SelectedRecipe.Output.ResolvedItemstack.Clone();

                if (result.StackSize == 1 && result.Class == EnumItemClass.Block) world.BlockAccessor.SetBlock(result.Block.BlockId, prog.workPoint);
                else
                {
                    int tries = 500;
                    while (result.StackSize > 0 && tries-- > 0)
                    {
                        ItemStack dropStack = result.Clone();
                        dropStack.StackSize = Math.Min(result.StackSize, result.Collectible.MaxStackSize);
                        result.StackSize -= dropStack.StackSize;

                        world.SpawnItemEntity(dropStack, prog.workPoint.ToVec3d().Add(0.5, 0.5, 0.5));
                    }

                    if (tries <= 1)
                    {
                        world.Logger.Error("Tried to drop finished clay forming item but failed after 500 times?! Gave up doing so. Out stack was " + result);
                    }

                    world.BlockAccessor.SetBlock(0, prog.workPoint);
                }

                prog.ConsumeEnergy(nrg);
                return true;
            }

            return false;
        }
    }
}
