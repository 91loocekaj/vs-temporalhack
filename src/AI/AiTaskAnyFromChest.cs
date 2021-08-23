using Vintagestory.API;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace TemporalHack
{
    public class AiTaskAnyFromChest : AiTaskBase
    {
        bool stop = false;
        Vec3d targetPos;
        EntityBehaviorProgram prog;
        float moveSpeed = 0.01f;
        float minDist = 1.3f;
        long failureTime;

        public AiTaskAnyFromChest(EntityAgent entity) : base(entity)
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
            if (prog == null) return false;
            if (prog.dormant || !entity.LeftHandItemSlot.Empty || prog?.CheckChest() != true || prog.workStack != null) return false;
            Block check = world.BlockAccessor.GetBlock(prog.workChest);
            if (check != null && check.Code.Path.Contains("translocatorchest"))
            {
                if (!prog.TakeAnyFromChest()) failureTime = entity.World.ElapsedMilliseconds + TemporalHackerConfig.Loaded.BotFailureCooldownMs; else prog.ConsumeEnergy(1);
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
            if (!prog.TakeAnyFromChest()) failureTime = entity.World.ElapsedMilliseconds + TemporalHackerConfig.Loaded.BotFailureCooldownMs; else prog.ConsumeEnergy(1);

        }
    }
}
