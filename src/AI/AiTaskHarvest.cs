using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace TemporalHack
{
    public class AiTaskHarvest : AiTaskBase
    {
        POIRegistry porregistry;
        IAnimalFoodSource targetPoi;

        float moveSpeed = 0.02f;
        bool nowStuck = false;
        float harvestTime;
        float harvestTimeNow;
        bool animStart = false;
        bool lumber = false;
        long failureTime;
        AssetLocation[] harvestBlocks = new AssetLocation[0];

        AnimationMetaData harvestAnimMeta;

        public AiTaskHarvest(EntityAgent entity) : base(entity)
        {
            porregistry = entity.Api.ModLoader.GetModSystem<POIRegistry>();
        }

        public override void LoadConfig(JsonObject taskConfig, JsonObject aiConfig)
        {
            base.LoadConfig(taskConfig, aiConfig);

            if (taskConfig["harvestBlocks"] != null)
            {
                harvestBlocks = taskConfig["harvestBlocks"].AsArray<AssetLocation>(new AssetLocation[] { new AssetLocation("pumpkin-fruit-4")});
            }

            if (taskConfig.IsTrue("lumber")) lumber = true;

            if (taskConfig["movespeed"] != null)
            {
                moveSpeed = taskConfig["movespeed"].AsFloat(0.02f);
            }

            if (taskConfig["harvestTime"] != null)
            {
                harvestTime = taskConfig["harvestTime"].AsFloat(1.5f);
            }

            if (taskConfig["harvestAnimation"].Exists)
            {
                harvestAnimMeta = new AnimationMetaData()
                {
                    Code = taskConfig["harvestAnimation"].AsString()?.ToLowerInvariant(),
                    Animation = taskConfig["harvestAnimation"].AsString()?.ToLowerInvariant(),
                    AnimationSpeed = taskConfig["harvestAnimationSpeed"].AsFloat(1f)
                }.Init();
            }
        }

        public override bool ShouldExecute()
        {
            EntityBehaviorProgram prog = entity.GetBehavior<EntityBehaviorProgram>();
            if (prog == null || prog.dormant) return false;
            if (failureTime > entity.World.ElapsedMilliseconds || cooldownUntilMs > entity.World.ElapsedMilliseconds || !prog.CheckArea() || entity.LeftHandItemSlot.Itemstack?.Collectible?.Tool == null) return false;

            targetPoi = porregistry.GetNearestPoi((prog.workArea.End + prog.workArea.Start)/2, (float)(prog.workArea.Length + prog.workArea.Height + prog.workArea.Width), (poi) =>
            {
                if (poi.Type != "food" || !prog.workArea.ContainsOrTouches(poi.Position)) return false;
                IAnimalFoodSource foodPoi;

                if ((poi is BlockEntityBerryBush || poi is BlockEntityBeehive) && (foodPoi = poi as IAnimalFoodSource)?.IsSuitableFor(entity) == true)
                {
                    return true;
                }

                if (poi is BlockEntityFarmland)
                {
                    Block harvest = world.BlockAccessor.GetBlock(poi.Position.AsBlockPos);
                    int stage;
                    int.TryParse(harvest.LastCodePart(), out stage);

                    if ((lumber && (harvest?.Code.Path.Contains("log") == true || harvest?.Code.Path.Contains("bamboo-grown") == true) && 
                    (!harvest.Code.Path.Contains("resin") || !harvest.Code.Path.Contains("resinharvested")) && entity.LeftHandItemSlot.Itemstack.Collectible.Tool == EnumTool.Axe)
                    || FindMatchCode(harvest.Code) || stage == harvest.CropProps?.GrowthStages || harvest?.GetBehavior<BlockBehaviorHarvestable>() != null)
                    {
                        return true;
                    }
                }

                return false;
            }) as IAnimalFoodSource;

            return targetPoi != null;
        }

        public override void StartExecute()
        {
            base.StartExecute();
            nowStuck = false;
            animStart = false;
            harvestTimeNow = 0;
            pathTraverser.NavigateTo(targetPoi.Position, moveSpeed, 1.5f, OnGoalReached, OnStuck, false, 1000);
        }

        public override bool ContinueExecute(float dt)
        {
            if (entity.LeftHandItemSlot.Itemstack?.Collectible.Tool == null) return false;
            Vec3d pos = targetPoi.Position;

            pathTraverser.CurrentTarget.X = pos.X;
            pathTraverser.CurrentTarget.Y = pos.Y;
            pathTraverser.CurrentTarget.Z = pos.Z;

            Cuboidd targetBox = entity.CollisionBox.ToDouble().Translate(entity.ServerPos.X, entity.ServerPos.Y, entity.ServerPos.Z);
            double distance = targetBox.ShortestDistanceFrom(pos);

            float minDist = 1.5f;

            if (distance <= minDist)
            {
                //if (targetPoi.IsSuitableFor(entity) != true) return false;

                if (harvestAnimMeta != null && !animStart)
                {
                    entity.AnimManager.StartAnimation(harvestAnimMeta);
                    animStart = true;
                }

                harvestTimeNow += dt;

                if (harvestTimeNow >= harvestTime)
                {
                    EntityBehaviorProgram prog = entity.GetBehavior<EntityBehaviorProgram>();
                    
                    if (targetPoi is BlockEntityFarmland)
                    {
                        Block harvest = world.BlockAccessor.GetBlock(targetPoi.Position.AsBlockPos);
                        BlockBehaviorHarvestable bh;                  

                        if ((bh = harvest?.GetBehavior<BlockBehaviorHarvestable>()) != null && bh.harvestedStack != null)
                        {
                            ItemStack dropStack = bh.harvestedStack.GetNextItemStack();
                            world.PlaySoundAt(bh.harvestingSound, entity.SidedPos.X, entity.SidedPos.Y, entity.SidedPos.Z);
                            world.SpawnItemEntity(dropStack, targetPoi.Position);
                            JsonObject jasset = new JsonObject(JToken.Parse(bh.propertiesAtString));
                            world.BlockAccessor.SetBlock(world.BlockAccessor.GetBlock(new AssetLocation(jasset["harvestedBlockCode"].AsString())).BlockId, targetPoi.Position.AsBlockPos);
                            prog?.ConsumeEnergy(3);
                            entity.LeftHandItemSlot.Itemstack?.Collectible.DamageItem(world, entity, entity.LeftHandItemSlot, 1);
                            return false;
                        }
                        if ((harvest.Code.Path.Contains("log") || harvest.Code.Path.Contains("bamboo-grown")) && (!harvest.Code.Path.Contains("resin") || !harvest.Code.Path.Contains("resinharvested")) && entity.LeftHandItemSlot.Itemstack.Collectible.Tool == EnumTool.Axe)
                        {
                            string treetype;
                            Stack<BlockPos> tree = FindTree(world, targetPoi.Position.AsBlockPos, out treetype);
                            prog?.ConsumeEnergy(tree.Count);
                            entity.LeftHandItemSlot.Itemstack?.Collectible.DamageItem(world, entity, entity.LeftHandItemSlot, tree.Count);

                            while (tree.Count > 0)
                            {
                                world.BlockAccessor.BreakBlock(tree.Pop(), null);
                            }
                            
                            return false;
                        }
                        if (harvest is BlockReeds)
                        {
                            AssetLocation reed = world.BlockAccessor.GetBlock(targetPoi.Position.AsBlockPos).CodeWithPart("harvested", 3);
                            world.BlockAccessor.SetBlock(world.BlockAccessor.GetBlock(reed).Id, targetPoi.Position.AsBlockPos);
                            world.SpawnItemEntity(new ItemStack(world.GetItem(new AssetLocation("game:cattailtops")), 1), entity.ServerPos.XYZ);
                            prog?.ConsumeEnergy(4);
                            entity.LeftHandItemSlot.Itemstack?.Collectible.DamageItem(world, entity, entity.LeftHandItemSlot, 1);
                            return false;
                        }
                        if (harvest.Code.Path.Contains("tallgrass"))
                        {
                            world.BlockAccessor.BreakBlock(targetPoi.Position.AsBlockPos, null);
                            world.SpawnItemEntity(new ItemStack(world.GetItem(new AssetLocation("game:drygrass")), 1), entity.ServerPos.XYZ);
                            prog?.ConsumeEnergy(3);
                            entity.LeftHandItemSlot.Itemstack?.Collectible.DamageItem(world, entity, entity.LeftHandItemSlot, 1);
                            return false;
                        }

                        world.BlockAccessor.BreakBlock(targetPoi.Position.AsBlockPos, null);
                        prog?.ConsumeEnergy(3);
                        entity.LeftHandItemSlot.Itemstack?.Collectible.DamageItem(world, entity, entity.LeftHandItemSlot, 1);
                        return false;
                    }

                    if (targetPoi is BlockEntityBeehive && targetPoi.IsSuitableFor(entity))
                    {
                        AssetLocation hive = world.BlockAccessor.GetBlock(targetPoi.Position.AsBlockPos).CodeWithPart("empty", 1);
                        world.BlockAccessor.SetBlock(world.BlockAccessor.GetBlock(hive).Id, targetPoi.Position.AsBlockPos);

                        world.SpawnItemEntity(new ItemStack(world.GetItem(new AssetLocation("game:honeycomb")), 3), entity.ServerPos.XYZ);
                        prog?.ConsumeEnergy(8);
                        entity.LeftHandItemSlot.Itemstack?.Collectible.DamageItem(world, entity, entity.LeftHandItemSlot, 2);
                        return false;
                    }

                    if (targetPoi.IsSuitableFor(entity)) targetPoi.ConsumeOnePortion();
                    prog?.ConsumeEnergy(3);
                    entity.LeftHandItemSlot.Itemstack?.Collectible.DamageItem(world, entity, entity.LeftHandItemSlot, 1);
                    return false;
                }
            }
            else
            {
                if (!pathTraverser.Active)
                {
                    float rndx = (float)entity.World.Rand.NextDouble() * 0.3f - 0.15f;
                    float rndz = (float)entity.World.Rand.NextDouble() * 0.3f - 0.15f;
                    if (!pathTraverser.NavigateTo(targetPoi.Position.AddCopy(rndx, 0, rndz), moveSpeed, MinDistanceToTarget() - 0.15f, OnGoalReached, OnStuck, false, 500))
                    {
                        return false;
                    }
                }
            }

            if (nowStuck) return false;

            return true;
        }

        public override void FinishExecute(bool cancelled)
        {
            base.FinishExecute(cancelled);
            pathTraverser.Stop();

            if (harvestAnimMeta != null)
            {
                entity.AnimManager.StopAnimation(harvestAnimMeta.Code);
            }
        }

        public float MinDistanceToTarget()
        {
            return Math.Max(0.6f, entity.CollisionBox.XSize / 2 + 0.05f);
        }

        private void OnStuck()
        {
            failureTime = entity.World.ElapsedMilliseconds + TemporalHackerConfig.Loaded.BotFailureCooldownMs;
            nowStuck = true;

        }

        private void OnGoalReached()
        {
            pathTraverser.Active = true;
        }

        public Stack<BlockPos> FindTree(IWorldAccessor world, BlockPos startPos, out string treeType)
        {
            Queue<Vec4i> queue = new Queue<Vec4i>();
            HashSet<BlockPos> checkedPositions = new HashSet<BlockPos>();
            Stack<BlockPos> foundPositions = new Stack<BlockPos>();

            treeType = "";


            Block block = world.BlockAccessor.GetBlock(startPos);

            if (block.Code == null) return foundPositions;

            if (block.Code.Path.StartsWith("log-grown") || block.Code.Path.StartsWith("beehive-inlog-") || block.Code.Path.StartsWith("log-resin") || block.Code.Path.StartsWith("bamboo-grown-"))
            {
                treeType = block.FirstCodePart(2);

                queue.Enqueue(new Vec4i(startPos.X, startPos.Y, startPos.Z, 2));
                foundPositions.Push(startPos);
                checkedPositions.Add(startPos);
            }

            string logcode = block.Code.Path.StartsWith("bamboo") ? "bamboo-grown-" + treeType : "log-grown-" + treeType;

            if (block is BlockFernTree)
            {
                treeType = "fern";
                logcode = "ferntree-normal";
                queue.Enqueue(new Vec4i(startPos.X, startPos.Y, startPos.Z, 2));
                foundPositions.Push(startPos);
                checkedPositions.Add(startPos);
            }

            string logcode2 = "log-resin-" + treeType;
            string logcode3 = "log-resinharvested-" + treeType;
            string leavescode = block.Code.Path.StartsWith("bamboo") ? "bambooleaves-" + treeType + "-grown" : "leaves-grown-" + treeType;
            string leavesbranchycode = "leavesbranchy-grown-" + treeType;



            while (queue.Count > 0)
            {
                if (foundPositions.Count > 2000)
                {
                    break;
                }

                Vec4i pos = queue.Dequeue();

                for (int i = 0; i < Vec3i.DirectAndIndirectNeighbours.Length; i++)
                {
                    Vec3i facing = Vec3i.DirectAndIndirectNeighbours[i];
                    BlockPos neibPos = new BlockPos(pos.X + facing.X, pos.Y + facing.Y, pos.Z + facing.Z);

                    float hordist = GameMath.Sqrt(neibPos.HorDistanceSqTo(startPos.X, startPos.Z));
                    float vertdist = (neibPos.Y - startPos.Y);

                    // "only breaks blocks inside an upside down square base pyramid"
                    if (hordist - 1 >= 2 * vertdist) continue;
                    if (checkedPositions.Contains(neibPos)) continue;

                    block = world.BlockAccessor.GetBlock(neibPos);
                    if (block.Code == null) continue;

                    if (block.Code.Path.StartsWith(logcode) || block.Code.Path.StartsWith(logcode2) || block.Code.Path.StartsWith(logcode3))
                    {
                        if (pos.W < 2) continue;

                        foundPositions.Push(neibPos.Copy());
                        queue.Enqueue(new Vec4i(neibPos.X, neibPos.Y, neibPos.Z, 2));
                    }
                    else if (block.Code.Path.StartsWith(leavesbranchycode))
                    {
                        if (pos.W < 1) continue;

                        foundPositions.Push(neibPos.Copy());
                        queue.Enqueue(new Vec4i(neibPos.X, neibPos.Y, neibPos.Z, 1));
                    }
                    else if (block.Code.Path.StartsWith(leavescode))
                    {
                        foundPositions.Push(neibPos.Copy());
                        queue.Enqueue(new Vec4i(neibPos.X, neibPos.Y, neibPos.Z, 0));
                    }

                    checkedPositions.Add(neibPos);
                }
            }

            return foundPositions;
        }

        public bool FindMatchCode(AssetLocation needle)
        {
            if (needle == null) return false;

            foreach (AssetLocation hay in harvestBlocks)
            {
                if (hay.Equals(needle)) return true;
                
                if (hay.IsWildCard && WildcardUtil.GetWildcardValue(hay, needle) != null) return true;
            }

            return false;
        }

    }
}
