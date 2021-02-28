using System.Collections.Generic;
using Vintagestory.API;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace TemporalHack
{
    public class AiTaskRepair : AiTaskBase
    {
        EntityPartitioning entityUtil;
        bool stop = false;
        Entity targetEntity;
        EntityBehaviorProgram prog;
        float moveSpeed = 0.01f;
        float minDist = 1.3f;
        long failureTime;
        Dictionary<Vec3d, long> failures = new Dictionary<Vec3d, long>();

        public AiTaskRepair(EntityAgent entity) : base(entity)
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
            if (prog == null || prog.dormant || !prog.CheckArea()) return false;
            
            entityUtil.WalkEntities((prog.workArea.Start + prog.workArea.End)/2, prog.workArea.Length + prog.workArea.Height + prog.workArea.Width, (ent) =>
            {
                if (!prog.workArea.ContainsOrTouches(ent.ServerPos.XYZ) || ent.GetBehavior<EntityBehaviorProgram>() == null) return true;
                EntityBehaviorHealth bh = ent.GetBehavior<EntityBehaviorHealth>();
                if (bh == null || bh.Health >= bh.MaxHealth) return true;

                targetEntity = ent;
                return false;
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
            targetEntity.ReceiveDamage(new DamageSource() { Type = EnumDamageType.Heal }, 5);
            prog.ConsumeEnergy(35);
            stop = true;
        }
    }
}
