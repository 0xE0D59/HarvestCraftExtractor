﻿using System.Collections.Generic;
using System.Text.RegularExpressions;
using ExileCore;

namespace HarvestCraftExtractor
{
    public static class Helpers
    {
        public static Dictionary<string, string> MatchNamedCaptures(this Regex regex, string input)
        {
            var namedCaptureDictionary = new Dictionary<string, string>();
            GroupCollection groups = regex.Match(input).Groups;
            string[] groupNames = regex.GetGroupNames();
            foreach (string groupName in groupNames)
                if (groups[groupName].Captures.Count > 0)
                    namedCaptureDictionary.Add(groupName, groups[groupName].Value);
            return namedCaptureDictionary;
        }

        public static class FileContent
        {
            public const string CraftClassification = @"# Format:
#craft_tag|tag_description
aug_attack_lucky|Augment an item with a new Attack modifier with Lucky values
aug_attack|Augment an item with a new Attack modifier
aug_caster_lucky|Augment a Magic or Rare item with a new Caster modifier with Lucky values
aug_caster|Augment a Magic or Rare item with a new Caster modifier
aug_chaos_lucky|Augment an item with a new Chaos modifier with Lucky values
aug_chaos|Augment an item with a new Chaos modifier
aug_cold_lucky|Augment an item with a new Cold modifier with Lucky values
aug_cold|Augment an item with a new Cold modifier
aug_critical_lucky|Augment an item with a new Critical modifier with lucky values
aug_critical|Augment an item with a new Critical modifier
aug_defence_lucky|Augment an item with a new Defence modifier with Lucky values
aug_defence|Augment an item with a new Defence modifier
aug_fire_lucky|Augment a Magic or Rare item with a new Fire modifier with Lucky values
aug_fire|Augment a Magic or Rare item with a new Fire modifier
aug_influence|Augment an item with a new Influence modifier
aug_life_lucky|Augment an item with a new Life modifier with Lucky values
aug_life|Augment an item with a new Life modifier
aug_lightning_lucky|Augment an item with a new Lightning modifier with Lucky values
aug_lightning|Augment an item with a new Lightning modifier
aug_physical_lucky|Augment a Magic or Rare item with a new Physical modifier with Lucky values
aug_physical|Augment a Magic or Rare item with a new Physical modifier
aug_random_lucky|Augment a Rare item with a new modifier, with Lucky modifier values
aug_speed|Augment an item with a new Speed modifier
cards_gamble|Sacrifice up to half a stack of Divination Cards to receive between 0 and twice that amount of the same Card
change_cold_fire|Change a modifier that grants Cold Resistance into a similar-tier modifier that grants Fire Resistance
change_cold_lightning|Change a modifier that grants Cold Resistance into a similar-tier modifier that grants Lightning Resistance
change_fire_cold|Change a modifier that grants Fire Resistance into a similar-tier modifier that grants Cold Resistance
change_fire_lightning|Change a modifier that grants Fire Resistance into a similar-tier modifier that grants Lightning Resistance
change_harbringer|Change a Harbinger Unique or Unique Piece into a random Beachhead Map
change_lightning_cold|Change a modifier that grants Lightning Resistance into a similar-tier modifier that grants Cold Resistance
change_lightning_fire|Change a modifier that grants Lightning Resistance into a similar-tier modifier that grants Fire Resistance
enchant_body_cold|Enchant Body Armour. Quality does not increase its Defences, grants 1% Cold Resistance per 2% quality
enchant_body_fire|Enchant Body Armour. Quality does not increase its Defences, grants 1% Fire Resistance per 2% quality
enchant_body_life|Enchant Body Armour. Quality does not increase its Defences, grants +1 Maximum Life per 2% quality
enchant_body_lightning|Enchant Body Armour. Quality does not increase its Defences, grants 1% Lightning Resistance per 2% quality
enchant_body_strength|Enchant Body Armour. Quality does not increase its Defences, grants +1 Strength per 2% quality
enchant_weapon_range|Enchant a Melee Weapon. Quality does not increase its Physical Damage, has +1 Weapon Range per 10% Quality
exchange_alteration_chaos|Exchange 10 Orbs of Alteration for 10 Chaos Orbs
exchange_bestiary|Change a Unique Bestiary item or item with an Aspect into Lures of the same beast family
exchange_gem|Change a Gem into another Gem, carrying over experience and quality if possible
exchange_pale|Change a Pale Court Key into another random Pale Court Key
exchange_resonator_fossil|Exchange a Resonator for a Fossil or vice versa. Rare outcomes are more likely with rare inputs
fracture_prefix|Fracture a random Prefix on an item with at least 3 Prefixes. This cant be used on Influenced, Synthesised, or Fractured items.
fracture_random|Fracture a random modifier on an item with at least 5 modifiers, locking it in place. This can't be used on Influenced, Synthesised, or Fractured items
fracture_suffix|Fracture a random Suffix on an item with least 3 Suffixes. This cant be used on Influenced, Synthesised, or Fractured items
gem_add_quality|Improves the Quality of a Gem by at least 10%. The maximum quality is 20%
randomise_attack|Randomise the numeric values of the random Attack modifiers on a Magic or Rare item
randomise_caster|Randomise the numeric values of the random Caster modifiers on a Magic or Rare item
randomise_chaos|Randomise the numeric values of the random Chaos modifiers on a Magic or Rare item
randomise_cold|Randomise the numeric values of the random Cold modifiers on a Magic or Rare item
randomise_critical|Randomise the numeric values of the random Critical modifiers on a Magic or Rare item
randomise_defence|Randomise the numeric values of the random Defence modifiers on a Magic or Rare item
randomise_fire|Randomise the numeric values of the random Fire modifiers on a Magic or Rare item
randomise_life|Randomise the numeric values of the random Life modifiers on a Magic or Rare item
randomise_lightning|Randomise the numeric values of the random Lightning modifiers on a Magic or Rare item
randomise_physical|Randomise the numeric values of the random Physical modifiers on a Magic or Rare item
randomise_speed|Randomise the numeric values of the random Speed modifiers on a Magic or Rare item
reforge_critical_common|Reforge a Rare item with new random modifiers, including a Critical modifier. Critical modifiers are more common
reforge_critical|Reforge a Rare item with new random modifiers, including a Critical modifier
remove_add_attack|Remove a random Attack modifier from an item and add a new Attack modifier
remove_add_caster|Remove a random Caster modifier from an item and add a new Caster modifier
remove_add_chaos|Remove a random Chaos modifier from an item and add a new Chaos modifier
remove_add_cold|Remove a random Cold modifier from an item and add a new Cold modifier
remove_add_critical|Remove a random Critical modifier from an item and add a new Critical modifier
remove_add_defence|Remove a random Defence modifier from an item and add a new Defence modifier
remove_add_fire|Remove a random Fire modifier from an item and add a new Fire modifier
remove_add_life|Remove a random Life modifier from an item and add a new Life modifier
remove_add_lightning|Remove a random Lightning modifier from an item and add a new Lightning modifier
remove_add_non_attack|Remove a random non-Attack modifier from an item and add a new Attack modifier
remove_add_non_caster|Remove a random non-Caster modifier from an item and add a new Caster modifier
remove_add_non_chaos|Remove a random non-Chaos modifier from an item and add a new Chaos modifier
remove_add_non_cold|Remove a random non-Cold modifier from an item and add a new Cold modifier
remove_add_non_critical|Remove a random non-Critical modifier from an item and add a new Critical modifier
remove_add_non_defence|Remove a random non-Defence modifier from an item and add a new Defence modifier
remove_add_non_fire|Remove a random non-Fire modifier from an item and add a new Fire modifier
remove_add_non_life|Remove a random non-Life modifier from an item and add a new Life modifier
remove_add_non_lightning|Remove a random non-Lightning modifier from an item and add a new Lightning modifier
remove_add_non_physical|Remove a random non-Physical modifier from an item and add a new Physical modifier
remove_add_non_speed|Remove a random non-Speed modifier from an item and add a new Speed modifier
remove_add_physical|Remove a random Physical modifier from an item and add a new Physical modifier
remove_add_speed|Remove a random Speed modifier from an item and add a new Speed modifier
remove_attack|Remove a random Attack modifier from an item
remove_caster|Remove a random Caster modifier from an item
remove_chaos|Remove a random Chaos modifier from an item
remove_cold|Remove a random Cold modifier from an item
remove_critical|Remove a random Critical modifier from an item
remove_defence|Remove a random Defence modifier from an item
remove_fire|Remove a random Fire modifier from an item
remove_influence|Remove a random Influence modifier from an item
remove_life|Remove a random Life modifier from an item
remove_lightning|Remove a random Lightning modifier from an item
remove_physical|Remove a random Physical modifier from an item
remove_speed|Remove a random Speed modifier from an item
reroll_all_lucky|Reroll the values of Prefix, Suffix and Implicit modifiers on a Rare item, with Lucky modifier values
sacrifice_gem_facetors_20|Sacrifice a Corrupted Gem to gain 20% of the gem's total experience stored as a Facetor's Lens
sacrifice_gem_facetors_30|Sacrifice a Corrupted Gem to gain 30% of the gem's total experience stored as a Facetor's Lens
sacrifice_gem_facetors_50|Sacrifice a Corrupted Gem to gain 50% of the gem's total experience stored as a Facetor's Lens
sacrifice_offering|Change an Offering to the Goddess into a Dedication to the Goddess
sacrifice_weaponarmor_amulet|Sacrifice a Weapon or Armour to create an Amulet with similar modifiers
set_implicit_abysstimeless|Set a new Implicit modifier on an Abyss Jewel or Timeless Jewel
set_implicit_cluster|Set a new Implicit modifier on a Cluster Jewel
set_implicit_normal|Set a new Implicit modifier on a Cobalt, Crimson, Viridian or Prismatic Jewel
socket_colour_blue|Reforge the colour of a non-Blue socket on an item, turning it Blue
socket_colour_green|Reforge the colour of a non-Green socket on an item, turning it Green
socket_colour_red|Reforge the colour of a non-Red socket on an item, turning it Red
socket_colour_white_random|Reforge the colour of a random socket on an item, turning it White
socket_colour_white|Reforge the colour of a non-white socket on an item, turning it White
socket_count_six|Set an item to six sockets
socket_link_five|Reforge the links between sockets on an item, linking five sockets
socket_link_six|Reforge the links between sockets on an item, linking six sockets
upgrade_essence|Upgrade the tier of an Essence
upgrade_magic_rare|Upgrade a Magic item to a Rare item, adding three random modifiers";

            public const string CraftListTemplate =
                @"## Here is the template that the crafts will be exported to. You can use tags to insert the count of specified crafts.
## Tags are located in the CraftClassification.txt file.

WTS Crafts: 
Augments:
- caster          x $aug_caster -> 50c
- life            x $aug_life > [ 50c ]
- lightning       x $aug_lightning > [ 50c ]
- physical        x $aug_physical > [ 50c ]
- caster lucky    x $aug_caster_lucky > [ 60c ]
- lightning lucky x $aug_lightning_lucky > [ 60c ]
- physical lucky  x $aug_physical_lucky > [ 60c ]";
        }
    }
}