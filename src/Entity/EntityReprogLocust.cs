using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace TemporalHack
{
    public class EntityReprogLocust : EntityLocust
    {
        public override bool TryGiveItemStack(ItemStack itemstack)
        {
            if (itemstack == null || itemstack.StackSize == 0) return false;

            ItemSlot dummySlot = new DummySlot(null);
            dummySlot.Itemstack = itemstack.Clone();

            ItemStackMoveOperation op = new ItemStackMoveOperation(World, EnumMouseButton.Left, 0, EnumMergePriority.DirectMerge, itemstack.StackSize);

            if (GearInventory != null)
            {
                WeightedSlot wslot = GearInventory.GetBestSuitedSlot(dummySlot, new List<ItemSlot>());
                if (wslot.weight > 0)
                {
                    dummySlot.TryPutInto(wslot.slot, ref op);
                    itemstack.StackSize -= op.MovedQuantity;
                    WatchedAttributes.MarkAllDirty();
                    return op.MovedQuantity > 0;
                }
            }

            if (LeftHandItemSlot?.Inventory != null)
            {
                WeightedSlot wslot = LeftHandItemSlot.Inventory.GetBestSuitedSlot(dummySlot, new List<ItemSlot>());
                if (wslot.weight > 0)
                {
                    dummySlot.TryPutInto(wslot.slot, ref op);
                    itemstack.StackSize -= op.MovedQuantity;
                    WatchedAttributes.MarkAllDirty();
                    return op.MovedQuantity > 0;
                }
            }

            return false;
        }
    }
}
