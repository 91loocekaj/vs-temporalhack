using Vintagestory.API;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace TemporalHack
{
    public class AiTaskSqueezeHoney : AiTaskBase
    {
        bool stop = false;
        Vec3d targetPos;
        EntityBehaviorProgram prog;
        float moveSpeed = 0.01f;
        float minDist = 1.3f;
        long failureTime;

        public AiTaskSqueezeHoney(EntityAgent entity) : base(entity)
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
            if (!(entity.LeftHandItemSlot?.Itemstack?.Collectible is ItemHoneyComb) || prog.CheckBarrel() != true) return false;
            Block check = world.BlockAccessor.GetBlock(prog.workBarrel);
            if (check != null && check.Code.Path.Contains("translocatorbarrel"))
            {
                if (!prog.PutInBarrel(new DummySlot(new ItemStack(world.GetItem(new AssetLocation("game:honeyportion")), 1)))) failureTime = entity.World.ElapsedMilliseconds + TemporalHackerConfig.Loaded.BotFailureCooldownMs;
                else
                {
                    entity.World.SpawnItemEntity(new ItemStack(world.GetItem(new AssetLocation("beeswax"))), entity.SidedPos.XYZ);
                    entity.LeftHandItemSlot.TakeOut(1);
                    prog.ConsumeEnergy(12);
                }
                return false;
            }

            targetPos = prog.workBarrel.ToVec3d();


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
            if (!prog.PutInBarrel(new DummySlot(new ItemStack(world.GetItem(new AssetLocation("game:honeyportion")), 1)))) failureTime = entity.World.ElapsedMilliseconds + TemporalHackerConfig.Loaded.BotFailureCooldownMs;
            else
            {
                entity.World.SpawnItemEntity(new ItemStack(world.GetItem(new AssetLocation("beeswax"))), entity.SidedPos.XYZ);
                entity.LeftHandItemSlot.TakeOut(1);
                prog.ConsumeEnergy(12);
            }

        }
    }
}
