using System.Collections.Generic;
using Vintagestory.API;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace TemporalHack
{
    public class AiTaskSeekItemAny : AiTaskBase
    {
        EntityPartitioning entityUtil;
        bool stop = false;
        Vec3d targetPos;
        EntityBehaviorProgram prog;
        float moveSpeed = 0.01f;
        float minDist = 1.3f;
        Dictionary<Vec3d, long> failures = new Dictionary<Vec3d, long>();
        long failureTime;

        public AiTaskSeekItemAny(EntityAgent entity) : base(entity)
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
            if (prog == null || prog.dormant) return false;
            if (!prog.CheckArea() || (!prog.relaxed && prog.workStack != null)) return false;
            if (entity.LeftHandItemSlot?.Itemstack != null && entity.LeftHandItemSlot.StackSize >= entity.LeftHandItemSlot.Itemstack.Collectible.MaxStackSize) return false;

            entityUtil.WalkEntities((prog.workArea.Start + prog.workArea.End)/2, prog.workArea.Length + prog.workArea.Height + prog.workArea.Width, (ent) => 
            {
                if ((ent is EntityItem) && prog.workArea.ContainsOrTouches(ent.ServerPos.XYZ) && (entity.LeftHandItemSlot.Itemstack == null || entity.LeftHandItemSlot.Itemstack.Equals(entity.World, (ent as EntityItem).Itemstack, GlobalConstants.IgnoredStackAttributes)))
                {
                    targetPos = ent.ServerPos.XYZ;
                    return false;
                }

                return true;
            });

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
            prog.ConsumeEnergy(1);
            //System.Diagnostics.Debug.WriteLine("Reached");
        }
    }
}
