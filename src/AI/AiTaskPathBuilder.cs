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
    public class AiTaskPathBuilder : AiTaskBase
    {
        Vec3d targetPos;
        float moveSpeed = 0.02f;
        bool nowStuck = false;
        bool reached = false;
        float placeTime;
        float placeTimeNow;
        bool animStart = false;
        long failureTime;
        EntityBehaviorProgram prog;
        Cuboidd currentarea;
        AssetLocation[] excludeBlocks = new AssetLocation[] { new AssetLocation("game:mushroom-*") };

        AnimationMetaData placeAnimMeta;

        public AiTaskPathBuilder(EntityAgent entity) : base(entity)
        {
        }

        public override void LoadConfig(JsonObject taskConfig, JsonObject aiConfig)
        {
            base.LoadConfig(taskConfig, aiConfig);

            if (taskConfig["movespeed"] != null)
            {
                moveSpeed = taskConfig["movespeed"].AsFloat(0.02f);
            }

            if (taskConfig["placeTime"] != null)
            {
                placeTime = taskConfig["placeTime"].AsFloat(1.5f);
            }

            if (taskConfig["placeAnimation"].Exists)
            {
                placeAnimMeta = new AnimationMetaData()
                {
                    Code = taskConfig["placeAnimation"].AsString()?.ToLowerInvariant(),
                    Animation = taskConfig["placeAnimation"].AsString()?.ToLowerInvariant(),
                    AnimationSpeed = taskConfig["placeAnimationSpeed"].AsFloat(1f)
                }.Init();
            }

            if (taskConfig["excludeBlocks"] != null)
            {
                excludeBlocks = AssetLocation.toLocations(taskConfig["excludeBlocks"].AsArray<string>(new string[] { "game:mushroom-*" }));
            }
        }

        public override bool ShouldExecute()
        {
            prog = entity.GetBehavior<EntityBehaviorProgram>();
            if (prog == null || prog.dormant) return false;
            if (failureTime > entity.World.ElapsedMilliseconds || cooldownUntilMs > entity.World.ElapsedMilliseconds || !prog.CheckArea() || prog.workArea.Height > 0) return false;
            if (entity.LeftHandItemSlot.Itemstack?.Block == null || ExcludeBlock(entity.LeftHandItemSlot.Itemstack.Collectible.Code)) return false;
            
            if (currentarea == null || !AreaCompare())
            {
                currentarea = prog.workArea.Clone();
                targetPos = currentarea.End.AddCopy(-1,0,-1);
                if (entity.World.BlockAccessor.GetBlock(targetPos.AsBlockPos).BlockId == 0) return true;      
            }

            if (checkFinished())
            {
                prog.RemoveArea();
                targetPos = null;
                currentarea = null;
                return false;
            }

            findNextPoint();

            while(entity.World.BlockAccessor.GetBlock(targetPos.AsBlockPos).BlockId != 0 && !checkFinished())
            {
                if (!findNextPoint())
                {
                    prog.RemoveArea();
                    targetPos = null;
                    currentarea = null;
                    return false;
                }
            }

            return true;
        }

        public override void StartExecute()
        {
            base.StartExecute();
            nowStuck = false;
            reached = false;
            animStart = false;
            placeTimeNow = 0;
            pathTraverser.NavigateTo(targetPos.AddCopy(0,1,0), moveSpeed, 3f, OnGoalReached, OnStuck, false, 1000);
        }

        public override bool ContinueExecute(float dt)
        {
            if (entity.LeftHandItemSlot.Itemstack?.Block == null || ExcludeBlock(entity.LeftHandItemSlot.Itemstack.Collectible.Code) || world.BlockAccessor.GetBlock(targetPos.AsBlockPos).BlockId != 0) return false;
            pathTraverser.CurrentTarget.X = targetPos.X;
            pathTraverser.CurrentTarget.Y = targetPos.Y+1;
            pathTraverser.CurrentTarget.Z = targetPos.Z;

            Cuboidd targetBox = entity.CollisionBox.ToDouble().Translate(entity.ServerPos.X, entity.ServerPos.Y, entity.ServerPos.Z);
            double distance = targetBox.ShortestDistanceFrom(targetPos);

            float minDist = 3f;

            if (distance <= minDist)
            {
                if (placeAnimMeta != null && !animStart)
                {
                    entity.AnimManager.StartAnimation(placeAnimMeta);
                    animStart = true;
                }

                placeTimeNow += dt;

                if (placeTimeNow >= placeTime)
                {
                    world.BlockAccessor.SetBlock(entity.LeftHandItemSlot.Itemstack.Block.BlockId, targetPos.AsBlockPos);
                    entity.LeftHandItemSlot.TakeOut(1);
                    entity.LeftHandItemSlot.MarkDirty();
                    return false;
                }
            }
            else
            {
                if (reached && distance > minDist) return false;
                if (!pathTraverser.Active)
                {
                    float rndx = (float)entity.World.Rand.NextDouble() * 0.3f - 0.15f;
                    float rndz = (float)entity.World.Rand.NextDouble() * 0.3f - 0.15f;
                    if (!pathTraverser.NavigateTo(targetPos.AddCopy(rndx, 0, rndz), moveSpeed, MinDistanceToTarget() - 0.15f, OnGoalReached, OnStuck, false, 500))
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

            if (placeAnimMeta != null)
            {
                entity.AnimManager.StopAnimation(placeAnimMeta.Code);
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
            reached = true;
        }

        private bool findNextPoint()
        {
            if (targetPos.Z == currentarea.Start.Z)
            {
                if (targetPos.X == currentarea.Start.X)
                {
                    if (targetPos.Y != currentarea.Start.Y)
                    {
                        targetPos.Y--;
                        targetPos.X = currentarea.End.X - 1;
                        targetPos.Z = currentarea.End.Z - 1;
                    }
                    else if (checkFinished())
                    {
                        return false;
                    }
                }
                else
                {
                    targetPos.X--;
                    targetPos.Z = currentarea.End.Z - 1;
                }
            }
            else
            {
                targetPos.Z--;
            }

            return true;
        }

        private bool checkFinished()
        {
            if (targetPos == null) return true;
            if (targetPos.X == currentarea.Start.X && targetPos.Y == currentarea.Start.Y && targetPos.Z == currentarea.Start.Z) return true;
            return false;
        }

        private bool AreaCompare()
        {
            if (prog.workArea.Start.X == currentarea.Start.X && prog.workArea.Start.Y == currentarea.Start.Y && prog.workArea.Start.Z == currentarea.Start.Z)
            {
                if (prog.workArea.End.X == currentarea.End.X && prog.workArea.End.Y == currentarea.End.Y && prog.workArea.End.Z == currentarea.End.Z) return true;
            }
            return false;
        }

        public bool ExcludeBlock(AssetLocation needle)
        {
            if (needle == null) return false;

            foreach (AssetLocation hay in excludeBlocks)
            {
                if (hay.Equals(needle)) return true;

                if (hay.IsWildCard && WildcardUtil.GetWildcardValue(hay, needle) != null) return true;
            }

            return false;
        }

    }
}
