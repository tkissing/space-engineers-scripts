# Lino's Ingot Counter for Space Engineers 

## Installation

This script is currently not published in the Steam Workshop.
It is recommended to `git clone` this repository and build it using Visual Studio. 

## In-game Configuration

### Inventory Display

By default this script will display Component, Ingot and Ore counts on it's own screen and in it's own Control Panel info

You can optionally "project" this text onto any LCD on the same grid by editing the Custom Data of the Programmable Block to include

`display:whateveryouwant`

and then adding the same word (no spaces!) you used for `whateveryouwant` to the names of the LCDs you want to display the inventory on.
Make sure you set the LCDs to display "Text or Images".

### Auto-sorting

You can now optionally have this script sort your inventory for you.

To use this feature, you will have to use item type names as defined in the game. The complete list can be found at 

https://github.com/malware-dev/MDK-SE/wiki/Type-Definition-Listing

Every item name is made up of 3 parts: The string `MyObjectBuilder_` followed by the item type and subtype,
which are separated by a slash `/`.
The ATM block for example has the item name `MyObjectBuilder_StoreBlock/AtmBlock`, so the type is `StoreBlock` and the subtype is `AtmBlock`.

For the configuration of auto-sort with this script, you will mostly work with item types and only optionally with item subtypes. 

To opt-in to sorting, configure your cargo containers by adding a line to their Custom Data that starts with `insert:`
followed by one or more item types, e.g. `insert:Ore`. Be sure to not use any whitespace between the colo and the first type,
and use exactly one space to separate any additional types. You may have multiple containers configured for the same type.
The script will move items with matching types into containers with the correct `insert:` directive, but it will not move items out
of them if it there is no configured container to put them into.
If for example you have two Large Cargo Container A and B and you add `insert:Ingot` to the Custom Data of A, the script will move all 
ingots from B to A, but it will not move any items out of A that are not ingots. 

For a bit finer control, you can additionally configure subtype preferences for specific items by adding one more lines starting with `prefer:`
followed by a single item subtype to the Custom Data of a cargo container, e.g. `prefer:Iron`
Keep in mind that you still need to specify the type using insert. Also, the script will not move any items between containers that
include an `insert:` directive for the same type.
If for example you have 2 Large Cargo Container A and B with `insert:Ingot` and you add `prefer:Nickel` to B,
any nickel ingots in LCC A will stay there. Additionally, should B ever get close to being full and the script detects nickel ingots
that need to be moved in another cargo container C, it will move them to A.

It is strongly recommended to not have any sorters on the grid with Drain All enabled that push into containers that are auto-sorted.
It is fine to create a one-way pull away from the containers managed by this script. 
Generally speaking, if you see items moving back and forth, it's most certainly a sorter with Drain All causing it.

### Auto-crafting

You can now optionally have this script craft components for you.

To use this feature, edit the Custom Data for the assembler you want to do the crafting with `craft:` directives.

Here is a list of all currently supported components in the right format:

```
craft:Component/BulletproofGlass 1000
craft:Component/Computer 1000
craft:Component/Construction 1000
craft:Component/Detector 1000
craft:Component/Display 1000
craft:Component/Explosives 1000
craft:Component/Girder 1000
craft:Component/GravityGenerator 1000
craft:Component/InteriorPlate 1000
craft:Component/LargeTube 1000
craft:Component/Medical 1000
craft:Component/MetalGrid 1000
craft:Component/Motor 1000
craft:Component/PowerCell 1000
craft:Component/RadioCommunication 1000
craft:Component/Reactor 1000
craft:Component/SmallTube 1000
craft:Component/SolarCell 1000
craft:Component/SteelPlate 1000
craft:Component/Superconductor 1000
craft:Component/Thrust 1000
```

This feature is designed to fill up your containers as needed while you are "gone" from the base, not to keep up with active building. 
Whenever the script runs, it will look for configured assemblers that have an empty build queue. It will then add 1 item of each component
to the queue for which the actual count is below the target.
This should ensure that any manually (or build planner) created tasks are handled with minimal delay. 
If you have multiple assemblers, they all contribute to the overall inventory. In some cases that might lead to you having
slightly more items than your target. It also allows you to deprioritize certain components by listing no or a lower target for them
on some assemblers. 
If for example you have two assemblers, one with `craft:Component/SteelPlate 100` and another with `craft:Component/SteelPlate 5000`,
the first one will only build steel plates when you have less than 100 total while the other will keep building them until you are at 5000.


## Appendix

Go to https://github.com/malware-dev/MDK-SE/wiki/Quick-Introduction-to-Space-Engineers-Ingame-Scripts to learn more about ingame scripts.