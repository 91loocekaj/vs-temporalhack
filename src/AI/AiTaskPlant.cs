using System;
using Vintagestory.API;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace TemporalHack
{
    public class AiTaskPlant : AiTaskBase
    {
        POIRegistry porregistry;
        IPointOfInterest targetPoi;

        float moveSpeed = 0.02f;
        bool nowStuck = false;
        float plantTime;
        float plantTimeNow;
        bool animStart = false;
        AssetLocation[] plantBlocks = new AssetLocation[] { new AssetLocation("game:sapling-*")};
        AssetLocation modSeed = new AssetLocation("wildfarming:wildseeds-*");
        long failureTime;

        AnimationMetaData plantAnimMeta;

        public AiTaskPlant(EntityAgent entity) : base(entity)
        {
            porregistry = entity.Api.ModLoader.GetModSystem<POIRegistry>();
        }

        public override void LoadConfig(JsonObject taskConfig, JsonObject aiConfig)
        {
            base.LoadConfig(taskConfig, aiConfig);

            if (taskConfig["plantBlocks"] != null)
            {
                plantBlocks = AssetLocation.toLocations(taskConfig["plantBlocks"].AsArray<string>(new string[] { "game:sapling-*" }));
            }

            if (taskConfig["movespeed"] != null)
            {
                moveSpeed = taskConfig["movespeed"].AsFloat(0.02f);
            }

            if (taskConfig["plantTime"] != null)
            {
                plantTime = taskConfig["plantTime"].AsFloat(1.5f);
            }

            if (taskConfig["plantAnimation"].Exists)
            {
                plantAnimMeta = new AnimationMetaData()
                {
                    Code = taskConfig["plantAnimation"].AsString()?.ToLowerInvariant(),
                    Animation = taskConfig["plantAnimation"].AsString()?.ToLowerInvariant(),
                    AnimationSpeed = taskConfig["planrAnimationSpeed"].AsFloat(1f)
                }.Init();
            }
        }

        public override bool ShouldExecute()
        {
            EntityBehaviorProgram prog = entity.GetBehavior<EntityBehaviorProgram>();
            if (prog == null || prog.dormant) return false;
            if (failureTime > entity.World.ElapsedMilliseconds || cooldownUntilMs > entity.World.ElapsedMilliseconds || !prog.CheckArea() || !CanPlant(entity.LeftHandItemSlot?.Itemstack?.Collectible)) return false;

            targetPoi = porregistry.GetNearestPoi((prog.workArea.End + prog.workArea.Start)/2, (float)(prog.workArea.Length + prog.workArea.Height + prog.workArea.Width), (poi) =>
            {
                if (prog.workArea.ContainsOrTouches(poi.Position) && poi is BlockEntityFarmland && world.BlockAccessor.GetBlock(poi.Position.AsBlockPos).BlockId == 0) return true;
                

                return false;
            });

            return targetPoi != null;
        }

        public override void StartExecute()
        {
            base.StartExecute();
            nowStuck = false;
            animStart = false;
            plantTimeNow = 0;
            pathTraverser.NavigateTo(targetPoi.Position, moveSpeed, MinDistanceToTarget() - 0.1f, OnGoalReached, OnStuck, false, 1000);
        }

        public override bool ContinueExecute(float dt)
        {
            if (entity.LeftHandItemSlot.Empty) return false;

            Vec3d pos = targetPoi.Position;

            pathTraverser.CurrentTarget.X = pos.X;
            pathTraverser.CurrentTarget.Y = pos.Y;
            pathTraverser.CurrentTarget.Z = pos.Z;

            Cuboidd targetBox = entity.CollisionBox.ToDouble().Translate(entity.ServerPos.X, entity.ServerPos.Y, entity.ServerPos.Z);
            double distance = targetBox.ShortestDistanceFrom(pos);

            float minDist = MinDistanceToTarget();

            if (distance <= minDist)
            {

                if (plantAnimMeta != null && !animStart)
                {
                    entity.AnimManager.StartAnimation(plantAnimMeta);
                    animStart = true;
                }

                plantTimeNow += dt;

                if (plantTimeNow >= plantTime)
                {
                    EntityBehaviorProgram prog = entity.GetBehavior<EntityBehaviorProgram>();
                    if (world.BlockAccessor.GetBlock(targetPoi.Position.AsBlockPos).BlockId != 0) return false;
                    if (entity.LeftHandItemSlot.Itemstack.Collectible is ItemPlantableSeed)
                    {
                        Block cropBlock = world.GetBlock(new AssetLocation("game:crop-" + entity.LeftHandItemSlot.Itemstack.Collectible.LastCodePart() + "-1"));
                        if ((targetPoi as BlockEntityFarmland).TryPlant(cropBlock))
                        {
                            entity.LeftHandItemSlot.TakeOut(1);
                            prog?.ConsumeEnergy(4);
                        }
                    }
                    else if (entity.LeftHandItemSlot.Itemstack.Collectible.WildCardMatch(modSeed))
                    {
                        Block cropBlock = world.GetBlock(new AssetLocation("wildfarming:wildplant-" + entity.LeftHandItemSlot.Itemstack.Collectible.CodeEndWithoutParts(1)));
                        world.BlockAccessor.SetBlock(cropBlock.Id, targetPoi.Position.AsBlockPos);
                        entity.LeftHandItemSlot.TakeOut(1);
                        prog?.ConsumeEnergy(4);
                    }
                    else if (entity.LeftHandItemSlot.Itemstack.Block != null)
                    {
                        world.BlockAccessor.SetBlock(entity.LeftHandItemSlot.Itemstack.Block.Id, targetPoi.Position.AsBlockPos);
                        entity.LeftHandItemSlot.TakeOut(1);
                        prog?.ConsumeEnergy(4);
                    }

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

            if (plantAnimMeta != null)
            {
                entity.AnimManager.StopAnimation(plantAnimMeta.Code);
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
