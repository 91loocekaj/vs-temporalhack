﻿using Vintagestory.API;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace TemporalHack
{
    public class AiTaskStayCloseToOwner : AiTaskBase
    {
        Entity targetEntity;
        float moveSpeed = 0.03f;
        float range = 8f;
        float maxDistance = 3f;
        bool stuck = false;
        EntityBehaviorProgram prog;

        Vec3d targetOffset = new Vec3d();

        public AiTaskStayCloseToOwner(EntityAgent entity) : base(entity)
        {
        }

        public override void LoadConfig(JsonObject taskConfig, JsonObject aiConfig)
        {
            base.LoadConfig(taskConfig, aiConfig);

            if (taskConfig["movespeed"] != null)
            {
                moveSpeed = taskConfig["movespeed"].AsFloat(0.03f);
            }

            if (taskConfig["searchRange"] != null)
            {
                range = taskConfig["searchRange"].AsFloat(8f);
            }

            if (taskConfig["maxDistance"] != null)
            {
                maxDistance = taskConfig["maxDistance"].AsFloat(3f);
            }
        }


        public override bool ShouldExecute()
        {
            if (rand.NextDouble() > 0.33f) return false;

            prog = entity.GetBehavior<EntityBehaviorProgram>();
            if (prog?.dormant != false || prog.workArea != null || prog.owner == null) return false;

            if (targetEntity == null || !targetEntity.Alive)
            {
                targetEntity = entity.World.GetNearestEntity(entity.ServerPos.XYZ, range, 2, (e) => {
                    return (e as EntityPlayer)?.PlayerUID == prog.owner.PlayerUID;
                });
            }
            
            if (targetEntity != null && (!targetEntity.Alive || targetEntity.ShouldDespawn)) targetEntity = null;
            if (targetEntity == null) return false;

            double x = targetEntity.ServerPos.X;
            double y = targetEntity.ServerPos.Y;
            double z = targetEntity.ServerPos.Z;

            double dist = entity.ServerPos.SquareDistanceTo(x, y, z);

            return dist > maxDistance * maxDistance;
        }


        public override void StartExecute()
        {
            base.StartExecute();

            float size = targetEntity.CollisionBox.XSize;

            pathTraverser.NavigateTo(targetEntity.ServerPos.XYZ, moveSpeed, size + 0.2f, OnGoalReached, OnStuck, false, 1000, true);

            targetOffset.Set(entity.World.Rand.NextDouble() * 2 - 1, 0, entity.World.Rand.NextDouble() * 2 - 1);

            stuck = false;
        }


        public override bool ContinueExecute(float dt)
        {
            double x = targetEntity.ServerPos.X + targetOffset.X;
            double y = targetEntity.ServerPos.Y;
            double z = targetEntity.ServerPos.Z + targetOffset.Z;

            if (entity.ServerPos.SquareDistanceTo(x, y, z) > 40*40)
            {
                float pitch = entity.SidedPos.Pitch;
                float roll = entity.SidedPos.Roll;
                entity.TeleportTo(targetEntity.SidedPos);
                pathTraverser.Stop();
                entity.SidedPos.Pitch = pitch;
                entity.SidedPos.Roll = roll;
                return false;
            }

            pathTraverser.CurrentTarget.X = x;
            pathTraverser.CurrentTarget.Y = y;
            pathTraverser.CurrentTarget.Z = z;

            if (entity.ServerPos.SquareDistanceTo(x, y, z) < maxDistance * maxDistance / 4)
            {
                pathTraverser.Stop();
                return false;
            }

            return targetEntity.Alive && !stuck && pathTraverser.Active && prog?.dormant == false;
        }

        private void OnStuck()
        {
            stuck = true;
        }

        private void OnGoalReached()
        {

        }
    }
}
