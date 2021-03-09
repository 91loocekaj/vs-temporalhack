using System;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace TemporalHack
{
    public class ItemDeployBot : Item
    {
        public override string GetHeldTpUseAnimation(ItemSlot activeHotbarSlot, Entity byEntity)
        {
            return null;
        }

        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handHandling)
        {
            if (blockSel == null || !byEntity.Controls.Sneak) return;

            IPlayer player = byEntity.World.PlayerByUid((byEntity as EntityPlayer).PlayerUID);

            if (!byEntity.World.Claims.TryAccess(player, blockSel.Position, EnumBlockAccessFlags.BuildOrBreak))
            {
                return;
            }


            AssetLocation location = new AssetLocation(Code.Domain, CodeEndWithoutParts(1));
            EntityProperties type = byEntity.World.GetEntityType(location);
            if (type == null)
            {
                byEntity.World.Logger.Error("ItemCreature: No such entity - {0}", location);
                if (api.World.Side == EnumAppSide.Client)
                {
                    (api as ICoreClientAPI).TriggerIngameError(this, "nosuchentity", "No such entity '{0}' loaded.");
                }
                return;
            }

            Entity entity = byEntity.World.ClassRegistry.CreateEntity(type);

            if (entity != null)
            {
                entity.ServerPos.X = blockSel.Position.X + (blockSel.DidOffset ? 0 : blockSel.Face.Normali.X) + 0.5f;
                entity.ServerPos.Y = blockSel.Position.Y + (blockSel.DidOffset ? 0 : blockSel.Face.Normali.Y);
                entity.ServerPos.Z = blockSel.Position.Z + (blockSel.DidOffset ? 0 : blockSel.Face.Normali.Z) + 0.5f;
                entity.ServerPos.Yaw = (float)byEntity.World.Rand.NextDouble() * 2 * GameMath.PI;

                entity.Pos.SetFrom(entity.ServerPos);
                entity.PositionBeforeFalling.Set(entity.ServerPos.X, entity.ServerPos.Y, entity.ServerPos.Z);

                entity.Attributes.SetString("origin", "playerplaced");

                ITreeAttribute reprog = entity.WatchedAttributes.GetOrAddTreeAttribute("reprog");
                reprog.SetString("ownerId", player.PlayerUID);                
                reprog.SetInt("workEnergy", slot.Itemstack.Attributes.GetInt("workEnergy"));
                entity.WatchedAttributes.MarkPathDirty("reprog");





                byEntity.World.SpawnEntity(entity);

                if (api.Side == EnumAppSide.Server && slot.Itemstack.Attributes.GetDecimal("workHealth") > 0)
                {
                    DamageSource dmg = new DamageSource() { Type = EnumDamageType.Crushing };
                    entity.ReceiveDamage(dmg, (float)slot.Itemstack.Attributes.GetDecimal("workHealth"));
                }

                if (!(byEntity is EntityPlayer) || player.WorldData.CurrentGameMode != EnumGameMode.Creative)
                {
                    slot.TakeOut(1);
                    slot.MarkDirty();
                }
                handHandling = EnumHandHandling.PreventDefaultAction;
            }
        }

        public override string GetHeldTpIdleAnimation(ItemSlot activeHotbarSlot, Entity byEntity, EnumHand hand)
        {
            EntityProperties type = byEntity.World.GetEntityType(new AssetLocation(Code.Domain, CodeEndWithoutParts(1)));
            if (type == null) return base.GetHeldTpIdleAnimation(activeHotbarSlot, byEntity, hand);

            float size = Math.Max(type.HitBoxSize.X, type.HitBoxSize.Y);

            if (size > 1) return "holdunderarm";
            return "holdbothhands";
        }

        public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot inSlot)
        {
            return new WorldInteraction[] {
                new WorldInteraction()
                {
                    ActionLangCode = "heldhelp-place",
                    MouseButton = EnumMouseButton.Right,
                    HotKeyCode = "sneak"
                }
            }.Append(base.GetHeldInteractionHelp(inSlot));
        }

        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);
            if (inSlot.Itemstack.Attributes.GetInt("workEnergy") > 0) dsc.AppendLine(Lang.Get("temporalhack:work-energy", inSlot.Itemstack.Attributes.GetInt("workEnergy"), TemporalHackerConfig.Loaded.BotMaximumEnergy));
        }
    }
}
