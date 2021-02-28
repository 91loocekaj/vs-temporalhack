using System.Linq;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace TemporalHack
{
    public class TemporalHack : ModSystem
    {
        public override void Start(ICoreAPI api)
        {
            base.Start(api);

            api.RegisterItemClass("ProgCard", typeof(ItemProgCard));
            api.RegisterItemClass("ItemDeployBot", typeof(ItemDeployBot));

            api.RegisterEntityBehaviorClass("program", typeof(EntityBehaviorProgram));

            api.RegisterEntity("EntityReprogLocust", typeof(EntityReprogLocust));

            AiTaskRegistry.Register<AiTaskHarvest>("harvest");
            AiTaskRegistry.Register<AiTaskSeekItem>("seekitem");
            AiTaskRegistry.Register<AiTaskToChest>("tochest");
            AiTaskRegistry.Register<AiTaskFromChest>("fromchest");
            AiTaskRegistry.Register<AiTaskPlant>("plant");
            AiTaskRegistry.Register<AiTaskSqueezeHoney>("squeezehoney");
            AiTaskRegistry.Register<AiTaskMilk>("milk");
            AiTaskRegistry.Register<AiTaskToBarrel>("tobarrel");
            AiTaskRegistry.Register<AiTaskGetSeed>("getseed");
            AiTaskRegistry.Register<AiTaskSeekItemAny>("seekanyitem");
            AiTaskRegistry.Register<AiTaskGetFuel>("getfuel");
            AiTaskRegistry.Register<AiTaskRefuel>("refuel");
            AiTaskRegistry.Register<AiTaskRepair>("repair");
            AiTaskRegistry.Register<AiTaskProgSeekEntity>("progseekentity");
            AiTaskRegistry.Register<AiTaskProgMeleeAttack>("progmeleeattack");
            AiTaskRegistry.Register<AiTaskQuarry>("quarry");
            AiTaskRegistry.Register<AiTaskStayCloseToOwner>("stayclosetoowner");
            AiTaskRegistry.Register<AiTaskPathBuilder>("pathbuilder");
            AiTaskRegistry.Register<AiTaskToAltChest>("toaltchest");
            AiTaskRegistry.Register<AiTaskProgRangedAttack>("prograngedattack");
            AiTaskRegistry.Register<AiTaskForm>("form");
            AiTaskRegistry.Register<AiTaskToPoint>("topoint");
            AiTaskRegistry.Register<AiTaskGetItem>("getitem");
            AiTaskRegistry.Register<AiTaskAnyFromChest>("anyfromchest");
            AiTaskRegistry.Register<AiTaskGetTool>("gettool");

            try
            {
                TemporalHackerConfig FromDisk;
                if ((FromDisk = api.LoadModConfig<TemporalHackerConfig>("TemporalHackerConfig.json")) == null)
                {
                    api.StoreModConfig<TemporalHackerConfig>(TemporalHackerConfig.Loaded, "TemporalHackerConfig.json");
                }
                else TemporalHackerConfig.Loaded = FromDisk;
            }
            catch
            {
                api.StoreModConfig<TemporalHackerConfig>(TemporalHackerConfig.Loaded, "TemporalHackerConfig.json");
            }
        }
    }

    public class TemporalHackerConfig
    {
        public static TemporalHackerConfig Loaded { get; set; } = new TemporalHackerConfig();
        public long BotFailureCooldownMs { get; set; } = 60000;

        public int BotEnergyConsumption { get; set; } = 1;

        public int BotEnergyGain { get; set; } = 1;

        public int BotMaximumEnergy { get; set; } = 1500;
    }
}
