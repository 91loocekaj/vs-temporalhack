using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace TemporalHack
{
    public class ItemSlotWork : ItemSlotSurvival
    {
        EntityBehaviorProgram beh;


        public ItemSlotWork(EntityBehaviorProgram beh, InventoryGeneric inventory) : base(inventory)
        {
            this.beh = beh;
        }

        public override bool CanTakeFrom(ItemSlot sourceSlot, EnumMergePriority priority = EnumMergePriority.AutoMerge)
        {
            return base.CanTakeFrom(sourceSlot, priority) && work(sourceSlot);
        }

        public override bool CanHold(ItemSlot itemstackFromSourceSlot)
        {
            return base.CanHold(itemstackFromSourceSlot) && work(itemstackFromSourceSlot);
        }

        public bool work(ItemSlot sourceSlot)
        {

            if (beh.relaxed || beh.workStack == null || beh.workStack.Equals(beh.entity.World, sourceSlot.Itemstack, GlobalConstants.IgnoredStackAttributes))
            {
                return true;
            }


            return false;
        }



    }
}
