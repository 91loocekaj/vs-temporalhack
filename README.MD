-----Entity Behavior Program Attributes-----
relaxed: A bool that when true, removes the restrictions on the inventory
nochest: A bool that when true, removes the work chest from the GUI display and disables it from being set
nobarrel: A bool that when true, removes the work barrel from the GUI display and disables it from being set
invLocked: A bool that when true, prevents the player from swapping items with the entity
restrictItem: A JsonItemStack that permanently sets the work stacks and prevents it from being changed

-----Ai Task Any From Chest-----
This Ai makes the entity collect anything from the given chest. It has all the attributes of its parent class AiTaskBase as well as:
movespeed: A float that determines how fast the entity moves

-----Ai Task Form-----
This Ai makes the entity attempt to finished clay formations or knapping. It has all the attributes of its parent class AiTaskBase as well as:
movespeed: A float that determines how fast the entity moves

-----Ai Task From Chest-----
This Ai makes the entity attempt to take an item out of a chest that matches its work stack. It has all the attributes of its parent class AiTaskBase as well as:
movespeed: A float that determines how fast the entity moves
onlyOnGuard: A bool that when true, the entity will only execute this ai if they have a work area

-----Ai Task Get Fuel-----
This Ai makes the entity get combustible items from the selected chest. It has all the attributes of its parent class AiTaskBase as well as:
movespeed: A float that determines how fast the entity moves

-----Ai Task Get Item-----
This Ai makes the entity get items defined in its attributes from the selected chest. It has all the attributes of its parent class AiTaskBase as well as:
movespeed: A float that determines how fast the entity moves
items: A string array that represent the codes or wildcards of collectibles to get

-----Ai Task Get Seed-----
This Ai makes the entity get plantable items from a defined chest. It has all the attributes of its parent class AiTaskBase as well as:
movespeed: A float that determines how fast the entity moves
plantBlocks: An array of strings that represent block codes the entity can plant

-----Ai Task Get Tool-----
This Ai makes the entity get a tool from the defined chest. It has all the attributes of its parent class AiTaskBase as well as:
movespeed: A float that determines how fast the entity moves
tool: A string which is converted to a EnumTool to determine what tool the entity gets

-----Ai Task Harvest-----
This Ai makes the entity harvest certain blocks within an area. It has all the attributes of its parent class AiTaskBase as well as:
movespeed: A float that determines how fast the entity moves
harvestBlocks: A string array that represents the codes or wildcards of blocks to break
harvestTime: The (float)amount of time after reaching the destination the entity actually breaks the block
harvestAnimation: A string representing the code of the animation to play when harvesting
lumber: A bool that determines if the entity can chop down trees

-----Ai Task Milk-----
This Ai makes the entity milk milkable entities in a given area. It has all the attributes of its parent class AiTaskBase as well as:
movespeed: A float that determines how fast the entity moves

-----Ai Task Path Builder-----
This Ai makes the entity place blocks in a given area. It has all the attributes of its parent class AiTaskBase as well as:
movespeed: A float that determines how fast the entity moves
excludeBlocks: A string array that represents the codes or wildcards of blocks to not place
placeTime: The (float)amount of time after reaching the destination the entity actually places the block
placeAnimation: A string representing the code of the animation to play when placing

-----Ai Task Quarry-----
This Ai makes the entity harvest certain blocks within an area. It has all the attributes of its parent class AiTaskBase as well as:
movespeed: A float that determines how fast the entity moves
mineTime: The (float)amount of time after reaching the destination the entity actually breaks the block
mineAnimation: A string representing the code of the animation to play when mining

-----Ai Task Repair-----
This Ai makes the entity repair other programmed entities in a given area. It has all the attributes of its parent class AiTaskBase as well as:
movespeed: A float that determines how fast the entity moves

-----Ai Task Seek Item-----
This Ai makes the entity seek an item the matches its work stack in a given area. It has all the attributes of its parent class AiTaskBase as well as:
movespeed: A float that determines how fast the entity moves

-----Ai Task Seek Any Item-----
This Ai makes the entity seek any item in a given area. It has all the attributes of its parent class AiTaskBase as well as:
movespeed: A float that determines how fast the entity moves

-----Ai Task Squeeze Honey-----
This Ai makes the entity squeeze honeycomb from a chest into a barrel. It has all the attributes of its parent class AiTaskBase as well as:
movespeed: A float that determines how fast the entity moves

-----Ai Task To Alt Chest-----
This Ai makes the entity drop the contents of its inventory into a alternate chest. It has all the attributes of its parent class AiTaskBase as well as:
movespeed: A float that determines how fast the entity moves

-----Ai Task To chest-----
This Ai makes the entity drop the contents of its inventory into a chest. It has all the attributes of its parent class AiTaskBase as well as:
movespeed: A float that determines how fast the entity moves

-----Ai Task To Barrel-----
This Ai makes the entity pour the contents of its inventory into a barrel. It has all the attributes of its parent class AiTaskBase as well as:
movespeed: A float that determines how fast the entity moves

-----Ai Task To Point-----
This Ai makes the entity move to a central point. It has all the attributes of its parent class AiTaskBase as well as:
movespeed: A float that determines how fast the entity moves

-----Ai Task Refuel-----
This Ai makes the entity refuel other programmed entities. It has all the attributes of its parent class AiTaskBase as well as:
movespeed: A float that determines how fast the entity moves
refuelpoint: A integer amount that determines the critical point where the entity will refuel another or itself
