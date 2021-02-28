using System.Collections.Generic;
using Vintagestory.API;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace TemporalHack
{
    public class AiTaskMilk : AiTaskBase
    {
        EntityPartitioning entityUtil;
        bool stop = false;
        Entity targetEntity;
        EntityBehaviorProgram prog;
        float moveSpeed = 0.01f;
        float minDist = 1.3f;
        long failureTime;
        Dictionary<Vec3d, long> failures = new Dictionary<Vec3d, long>();

        public AiTaskMilk(EntityAgent entity) : base(entity)
        {
            entityUtil = entity.Api.ModLoader.GetModSystem<EntityPartitioning>();
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
            if (prog == null || prog.dormant || !prog.CheckArea() || prog.workStack == null) return false;
            if (entity.LeftHandItemSlot.Itemstack != null) return false;

            entityUtil.WalkEntities((prog.workArea.Start + prog.workArea.End)/2, prog.workArea.Length + prog.workArea.Height + prog.workArea.Width, (ent) =>
            {
                if (!prog.workArea.ContainsOrTouches(ent.ServerPos.XYZ) || ent.GetBehavior("milkable") == null && ent.GetBehavior("foodmilk") == null) return true;

                ITreeAttribute mult = ent.WatchedAttributes.GetTreeAttribute("multiply");
                
                if (ent.World.Calendar.TotalDays - mult.GetDouble("totalDaysLastBirth") <= 21 && ent.World.Calendar.TotalHours - ent.WatchedAttributes.GetFloat("lastMilkedTotalHours") >= ent.World.Calendar.HoursPerDay)
                {
                    
                    targetEntity = ent;
                    return false;
                }

                return true;
            });

            return targetEntity != null;
        }

        public override void StartExecute()
        {
            base.StartExecute();
            stop = false;
            pathTraverser.NavigateTo(targetEntity.ServerPos.XYZ, moveSpeed, minDist, OnGoalReached, OnStuck, false, 1000);
        }

        public override bool ContinueExecute(float dt)
        {
            pathTraverser.CurrentTarget.Set(targetEntity.ServerPos.XYZ);

            return !stop;
        }

        public override void FinishExecute(bool cancelled)
        {
            base.FinishExecute(cancelled);
            targetEntity = null;
            pathTraverser.Stop();
        }

        private void OnStuck()
        {
            stop = true;
            failureTime = entity.World.ElapsedMilliseconds + TemporalHackerConfig.Loaded.BotFailureCooldownMs;
        }

        private void OnGoalReached()
        {
            entity.LeftHandItemSlot.Itemstack = new ItemStack(world.GetItem(new AssetLocation("game:milkportion")), 10);
            entity.LeftHandItemSlot.MarkDirty();
            targetEntity.WatchedAttributes.SetFloat("lastMilkedTotalHours", (float)entity.World.Calendar.TotalHours);
            targetEntity.WatchedAttributes.MarkPathDirty("lastMilkedTotalHours");
            prog.ConsumeEnergy(30);
            stop = true;
        }
    }
}
