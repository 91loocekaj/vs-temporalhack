using System;
using System.Collections.Generic;
using Vintagestory.API;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace TemporalHack
{
  /*    -----Ranged Attack AI-----
  burstCooldownMs = Integer of number of milliseconds between shots
  burstSize = Integer number of rounds per attack. (Note having burstCooldownMs below 600 reduces number of shots for some reason)
  projectileCount = Integer number of projectiles shot per round
  damagePlayerAtMs = Integer; When to start firing
  minDist = Float; Minimum horizontal distance the target must be at
  minVerDist = Float; Minimum vertical distance the target must be at
  maxDist = Float; Maximum horizontal distance the target can be from
  maxVertDist = Float; Maximum vertical distance the target can be at
  spreadAngle = Float; How big the spread/accuracy is
  shotVelocity =  Float; How fast the shot is
  bodyPitch = A bool that determines if the entity should move its pitch when aiming
  fireThrough = A bool of whether the entity should fire through obstacles
  projectiles = An array of potential projectiles to shoot.A projectile should look like
    {
    projectileEntityCode: String of the entity code for the projectile

    damage: A float for how much damage to do
                weight: A float of the weight of the projectile(Only affects knockback)
      dropChance: A float that determines if this projectile item gets picked up
      stack: A Json Item Stack the projectile contains
    }*/

    public class AiTaskProgRangedAttack : AiTaskBase, IWorldIntersectionSupplier
    {
        Entity targetEntity;
        EntityPartitioning entityUtil;
        EntityBehaviorProgram prog;

        long lastCheckOrAttackMs;

        
        float minDist = 1.5f; //Minimum distance to fire
        float minVerDist = 1f;
        float maxDist = 10f;// Maximum distance to fire
        float maxVerDist = 5f;

        int damagePlayerAtMs = 500;

        BlockSelection blockSel = new BlockSelection();
        EntitySelection entitySel = new EntitySelection();

        string[] seekEntityCodesExact = new string[] { "player" };
        string[] seekEntityCodesBeginsWith = new string[0];

        int burstCooldownMs = 1000;
        int burstSize = 1;
        float spreadAngle = 0;
        int projectileCount = 1;
        float shotVelocity = 1;
        bool fireThrough = false; //Can the entity fire with out direct line of sight
        bool bodyPitch = false; //Should it look up and down when aiming

        int shotsFired = 0; //Count how many shots we fired
        long nextShotMs = 0;
        int fullClip
        {
            get { return (int)(burstSize * (burstCooldownMs + (burstCooldownMs * (1 - entity.Stats.GetBlended("rangedWeaponsSpeed"))))); }
        }

        public Vec3i MapSize { get { return entity.World.BlockAccessor.MapSize; } }

        public AiTaskProgRangedAttack(EntityAgent entity) : base(entity)
        {
        }

        public override void LoadConfig(JsonObject taskConfig, JsonObject aiConfig)
        {
            //Modified
            base.LoadConfig(taskConfig, aiConfig);

            entityUtil = entity.Api.ModLoader.GetModSystem<EntityPartitioning>();

            //this.damage = taskConfig["damage"].AsFloat(2) * entity.Stats.GetBlended("rangedWeaponsDamage"); ;
            this.burstCooldownMs = taskConfig["burstCooldownMs"].AsInt(100);
            this.burstSize = taskConfig["burstSize"].AsInt(1);
            this.projectileCount = taskConfig["projectileCount"].AsInt(1);
            this.damagePlayerAtMs = taskConfig["damagePlayerAtMs"].AsInt(500);

            this.minDist = taskConfig["minDist"].AsFloat(2f);
            this.minVerDist = taskConfig["minVerDist"].AsFloat(0f);
            this.maxDist = taskConfig["maxDist"].AsFloat(20f);
            this.maxVerDist = taskConfig["maxVerDist"].AsFloat(10f);
            this.spreadAngle = taskConfig["spreadAngle"].AsFloat(0f);
            this.shotVelocity = taskConfig["shotVelocity"].AsFloat(1f);

            this.bodyPitch = taskConfig["bodyPitch"].AsBool(false);
            this.fireThrough = taskConfig["fireThrough"].AsBool(false);

            if (taskConfig["entityCodes"] != null)
            {
                string[] codes = taskConfig["entityCodes"].AsArray<string>(new string[] { "player" });

                List<string> exact = new List<string>();
                List<string> beginswith = new List<string>();

                for (int i = 0; i < codes.Length; i++)
                {
                    string code = codes[i];
                    if (code.EndsWith("*")) beginswith.Add(code.Substring(0, code.Length - 1));
                    else exact.Add(code);
                }

                seekEntityCodesExact = exact.ToArray();
                seekEntityCodesBeginsWith = beginswith.ToArray();
            }
        }

        public override bool ShouldExecute()
        {
            //Modified
            prog = entity.GetBehavior<EntityBehaviorProgram>();

            if (prog?.dormant != false || !(entity.LeftHandItemSlot.Itemstack?.Collectible is ItemArrow)) return false;
            long ellapsedMs = entity.World.ElapsedMilliseconds;
            if (ellapsedMs - lastCheckOrAttackMs < fullClip || cooldownUntilMs > ellapsedMs)
            {
                return false;
            }

            Vec3d pos = entity.ServerPos.XYZ.Add(0, entity.CollisionBox.Y2 / 2, 0).Ahead((entity.CollisionBox.X2 - entity.CollisionBox.X1) / 2, 0, entity.ServerPos.Yaw);

            targetEntity = entityUtil.GetNearestEntity(pos, maxDist > maxVerDist ? maxDist : maxVerDist, (e) => {
                if (!e.Alive || !e.IsInteractable || e.EntityId == this.entity.EntityId) return false;

                for (int i = 0; i < seekEntityCodesExact.Length; i++)
                {
                    if (e.Code.Path == seekEntityCodesExact[i])
                    {                       
                        if (hasDirectContact(e))
                        {
                            return true;
                        }
                    }
                }


                for (int i = 0; i < seekEntityCodesBeginsWith.Length; i++)
                {
                    if (e.Code.Path.StartsWithFast(seekEntityCodesBeginsWith[i]) && hasDirectContact(e))
                    {
                        return true;
                    }
                }

                return false;
            });

            lastCheckOrAttackMs = entity.World.ElapsedMilliseconds;
            nextShotMs = 0;
            shotsFired = 0;

            return targetEntity != null;
        }


        //float curTurnRadPerSec;

        public override void StartExecute()
        {
            base.StartExecute();
            //curTurnRadPerSec = entity.GetBehavior<EntityBehaviorTaskAI>().PathTraverser.curTurnRadPerSec;
            nextShotMs = lastCheckOrAttackMs + damagePlayerAtMs;
        }

        public override bool ContinueExecute(float dt)
        {
            if (!(entity.LeftHandItemSlot.Itemstack?.Collectible is ItemArrow)) return false;
            float dx = (float)(targetEntity.ServerPos.X - entity.ServerPos.X);
            float dy = (float)((targetEntity.ServerPos.Y + targetEntity.LocalEyePos.Y) - (entity.ServerPos.Y + (entity.LocalEyePos.Y - 0.2)));
            float dz = (float)(targetEntity.ServerPos.Z - entity.ServerPos.Z);


            float desiredYaw = (float)Math.Atan2(dx, dz);

            float yawDist = GameMath.AngleRadDistance(entity.ServerPos.Yaw, desiredYaw);
            entity.ServerPos.Yaw += yawDist;
            entity.ServerPos.Yaw = entity.ServerPos.Yaw % GameMath.TWOPI;

            float pitch = entity.ServerPos.Pitch;

            float desiredPitch = (float)Math.Atan2(dy, Math.Sqrt(dz * dz + dx * dx));

            float pitchDist = GameMath.AngleRadDistance(pitch, desiredPitch);
            pitch += pitchDist;
            pitch = pitch % GameMath.TWOPI;
            if (bodyPitch) entity.ServerPos.Pitch = pitch;

            if (lastCheckOrAttackMs + damagePlayerAtMs > entity.World.ElapsedMilliseconds) return true;

            if (hasDirectContact(targetEntity))
            {
                AmmuntionPouch pouch = new AmmuntionPouch()
                {
                    Damage = 3 + entity.LeftHandItemSlot.Itemstack.ItemAttributes["damage"].AsFloat(0),
                    DropChance = 1 - entity.LeftHandItemSlot.Itemstack.ItemAttributes["breakChanceOnImpact"].AsFloat(0.5f),
                    Weight = 0.2f,
                    Projectile = new AssetLocation("arrow-" + entity.LeftHandItemSlot.Itemstack.Collectible.Variant["material"]),
                    ProjectileStack = entity.LeftHandItemSlot.TakeOut(1)
                };
                entity.LeftHandItemSlot.MarkDirty();
                EntityProperties type = entity.World.GetEntityType(pouch.Projectile);
                Entity projectile = entity.World.ClassRegistry.CreateEntity(type);

                projectile = pouch.getRangedEntity(projectile, prog.owner?.Entity != null ? prog.owner?.Entity : entity, entity.Stats.GetBlended("rangedWeaponsDamage"));



                Vec3d pos = entity.ServerPos.XYZ.Add(0, entity.LocalEyePos.Y - 0.2, 0);
                Vec3d aheadPos = pos.AheadCopy(1, pitch + randomizeAngle(spreadAngle), entity.ServerPos.Yaw + (90 * GameMath.DEG2RAD * 50 * 0.02f) + randomizeAngle(spreadAngle));
                Vec3d velocity = (aheadPos - pos) * shotVelocity * entity.Stats.GetBlended("bowDrawingStrength");

                projectile.ServerPos.SetPos(entity.ServerPos.BehindCopy(0.21).XYZ.Add(0, entity.LocalEyePos.Y - 0.2, 0).Ahead(0.25, 0, entity.ServerPos.Yaw + GameMath.PIHALF));
                projectile.ServerPos.Motion.Set(velocity);

                projectile.Pos.SetFrom(projectile.ServerPos);
                projectile.World = entity.World;

                entity.World.SpawnEntity(projectile);
            }


            if (lastCheckOrAttackMs + fullClip > entity.World.ElapsedMilliseconds) return true;
            return false;
        }

        public override void FinishExecute(bool cancelled)
        {
            base.FinishExecute(cancelled);
            if (bodyPitch) entity.ServerPos.Pitch = 0;
        }

        bool hasDirectContact(Entity targetEntity)
        {
            //Modified
            Cuboidd targetBox = targetEntity.CollisionBox.ToDouble().Translate(targetEntity.ServerPos.X, targetEntity.ServerPos.Y, targetEntity.ServerPos.Z);
            Vec3d pos = entity.ServerPos.XYZ.Add(0, entity.CollisionBox.Y2 / 2, 0).Ahead((entity.CollisionBox.X2 - entity.CollisionBox.X1) / 2, 0, entity.ServerPos.Yaw);
            double dist = targetBox.ShortestDistanceFrom(pos);
            double vertDist = Math.Abs(targetBox.ShortestVerticalDistanceFrom(pos.Y));
            if (dist < minDist || vertDist < minVerDist || dist > maxDist * targetEntity.Stats.GetBlended("animalSeekingRange") || vertDist > maxVerDist * targetEntity.Stats.GetBlended("animalSeekingRange")) return false;
            if (fireThrough) return true;

            Vec3d rayTraceFrom = entity.ServerPos.XYZ;
            rayTraceFrom.Y += 1 / 32f;
            Vec3d rayTraceTo = targetEntity.ServerPos.XYZ;
            rayTraceTo.Y += 1 / 32f;
            bool directContact = false;

            entity.World.RayTraceForSelection(this, rayTraceFrom, rayTraceTo, ref blockSel, ref entitySel);
            directContact = blockSel == null;

            if (!directContact)
            {
                rayTraceFrom.Y += entity.CollisionBox.Y2 * 7 / 16f;
                rayTraceTo.Y += targetEntity.CollisionBox.Y2 * 7 / 16f;
                entity.World.RayTraceForSelection(this, rayTraceFrom, rayTraceTo, ref blockSel, ref entitySel);
                directContact = blockSel == null;
            }

            if (!directContact)
            {
                rayTraceFrom.Y += entity.CollisionBox.Y2 * 7 / 16f;
                rayTraceTo.Y += targetEntity.CollisionBox.Y2 * 7 / 16f;
                entity.World.RayTraceForSelection(this, rayTraceFrom, rayTraceTo, ref blockSel, ref entitySel);
                directContact = blockSel == null;
            }

            if (!directContact) return false;

            return true;
        }


        public Block GetBlock(BlockPos pos)
        {
            return entity.World.BlockAccessor.GetBlock(pos);
        }

        public Cuboidf[] GetBlockIntersectionBoxes(BlockPos pos)
        {
            return entity.World.BlockAccessor.GetBlock(pos).GetCollisionBoxes(entity.World.BlockAccessor, pos);
        }

        public bool IsValidPos(BlockPos pos)
        {
            return entity.World.BlockAccessor.IsValidPos(pos);
        }


        public Entity[] GetEntitiesAround(Vec3d position, float horRange, float vertRange, ActionConsumable<Entity> matches = null)
        {
            return new Entity[0];
        }

        public float randomizeAngle(float deviation)
        {
            deviation = Math.Abs(deviation);
            if (deviation <= 0) return 0f;
            if (deviation > 180) deviation = 180;

            float acc = 0;
            if (entity.Stats.GetBlended("rangedWeaponsAcc") != 1)
            {
                acc = (1 - entity.Stats.GetBlended("rangedWeaponsAcc")) * deviation;
            }

            float rangle = (float)entity.World.Rand.NextDouble() * (deviation - (-deviation)) + (-deviation);

            if (acc > 0)
            {
                rangle = entity.World.Rand.NextDouble() >= 0.5 ? rangle + acc : rangle - acc;
            }
            else if (acc < 0)
            {
                if (rangle > 0)
                {
                    rangle = Math.Min(0, rangle - acc);
                }
                else if (rangle < 0)
                {
                    rangle = Math.Max(0, rangle + acc);
                }
            }

            return rangle * GameMath.DEG2RAD * 50 * 0.02f;



        }
    }

    public delegate Entity projectileConverter(Entity projectile, Entity Firedby, ItemStack stack, float dmg, float dc, float weight);
    public class AmmuntionPouch
    {
        //Used for temp storage

        public static event projectileConverter modConverts = (missile, owner, stack, dmg, dc, weight) => missile;

        public AssetLocation Projectile = null;

        public ItemStack ProjectileStack = null;

        public float Damage = 1f;

        public float DropChance = 0f;

        public float Weight = 0.1f;

        public Entity getRangedEntity(Entity projectile, Entity Firedby, float DamageMult = 1)
        {
            if (modConverts != null)
            {
                foreach (projectileConverter dele in modConverts.GetInvocationList())
                {
                    projectile = dele.Invoke(projectile, Firedby, ProjectileStack?.Clone(), Damage * DamageMult, DropChance, Weight);
                }
            }

            return projectile;
        }

        static AmmuntionPouch()
        {
            modConverts += (projectile, Firedby, ProjectileStack, Damage, DropChance, Weight) =>
            {
                if (projectile is EntityThrownBeenade)
                {
                    ((EntityThrownBeenade)projectile).FiredBy = Firedby;
                    ((EntityThrownBeenade)projectile).ProjectileStack = ProjectileStack;
                }

                return projectile;
            };

            modConverts += (projectile, Firedby, ProjectileStack, Damage, DropChance, Weight) =>
            {
                if (projectile is EntityProjectile)
                {
                    ((EntityProjectile)projectile).FiredBy = Firedby;
                    ((EntityProjectile)projectile).Damage = Damage;
                    ((EntityProjectile)projectile).ProjectileStack = ProjectileStack;
                    ((EntityProjectile)projectile).DropOnImpactChance = DropChance;
                    ((EntityProjectile)projectile).Weight = Weight;
                }

                return projectile;
            };

            modConverts += (projectile, Firedby, ProjectileStack, Damage, DropChance, Weight) =>
            {
                if (projectile is EntityThrownStone)
                {
                    ((EntityThrownStone)projectile).FiredBy = Firedby;
                    ((EntityThrownStone)projectile).Damage = Damage;
                    ((EntityThrownStone)projectile).ProjectileStack = ProjectileStack;
                }

                return projectile;
            };
        }
    }
}
