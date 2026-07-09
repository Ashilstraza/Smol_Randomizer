# Smol Randomizer

A small randomizer for Silksong. This is not an item randomizer. This randomizes miscellaneous things like those listed below.

To use, download and enable within your mod manager of choice (or by manually installing if that is your jam), start Silksong, and go into the mod options within. There you can enable, disable, and configure the options to your liking. These are currently global across whichever save file you choose, but in the future will be per-save.

## Enemy Currency

This randomizes the quantity of rosaries and shards that drops when killing enemies.
<details>
<Summary>The options for this are:</Summary>

* Currency Consistancy
	* None - The same enemy type will have inconsistant quantities of shards and/or rosaries drop when killed.
	* Scene - The same enemy type may drop different quantities depending on the scene you are in, but will always drop the same amount within that scene.
	* Enemy Type - The same enemy type will always have the same quantity of shard and/or rosaries drop each time.

* Rosary Quantity
	* Disabled - Enemies will drop their regular amount of rosaries.
	* Percent - Enemies will drop a random percentage of rosaries that they normally would. 50% would be half, and 200% would be double.
	* Value - Enemies will drop a random number of rosaries between a set pair of values.

* Rosary Percent Drop Range
	* Minimum and Maximum - Enemy rosary drops will be multiplied by a random amount between these two values.

* Rosary Value Drop Range
	* Minimum and Maximum - Enemy rosary drops will be a random amount between these two values.

* Shard Quantity
	* Disabled - Enemies will drop their regular amount of shards.
	* Percent - Enemies will drop a random percentage of shards that they normally would. 50% would be half, and 200% would be double.
	* Value - Enemies will drop a random number of shards between a set pair of values.

* Shard Percent Drop Range
	* Minimum and Maximum - Enemy shard drops will be multiplied by a random amount between these two values.

* Shard Value Drop Range
	* Minimum and Maximum - Enemy shard drops will be a random amount between these two values.
</Details>

## Enemy Damage

This randomizes the damage Enemies and Bosses does to Hornet. This includes both contact damage and attacks.
<details>
<Summary>The options for this are:</Summary>

* Enemy Damage Modifier Type
	* Disabled - Enemy damage will be normal.
	* Shift - Enemy damage will be shifted up or down within the given quantity. A quantity of 2 means damage my be changed anywher between -2 and +2 damage. A negative amount will be treated as 0 by the game.
	* Range - Enemy damage will be delt between a range of values.
 
* Damage Consistancy
	* None - The same enemy type will have inconsistant damage when attacking.
	* Scene - The same enemy type will have consistant damage within the same scene, but may be different in another scene.
	* Enemy Type - The same enemy type will always have the same damage when attacking.
 
* Damage Shift
	* Quantity - Damage will be shifted by an amount within this value.

* Damage Range
	* Minimum and Maximum - Enemy damage will be a random amount between these two values.

* Enemy Damage Minimum
	* On or Off - This will set the minimum damage an enemy can do to Hornet to 1 when on.

* Enemy Attack Consistancy
	* On or Off - This will make each part of an attack do the same amount of damage. If disabled, each part of an attack may have different damage.

</details>

## Enemy Health

This randomizes the health Enemies and Bosses have. Some bosses phases may not be properly affected as this is a simple adjustment of their health.
<details>
<Summary>The options for this are:</summary>

* Randomize Health
	* None - Health of all enemies and bosses are their normal amounts.
	* Enemy - Health of enemies is randimized, bosses are normal.
	* Boss - Health of bosses are randomized, regular enemies are normal.
	* Both - Health of both bosses and enemies are randomized.

* Health Consistancy
	* None - The same enemy type will have inconsistant health.
	* Scene - The same enemy type will have consistant health within the same scene, but may be different in another scene.
	* Enemy Type - The same enemy type will always have the same health.

* Enemy Health Range
	* Minimum and Maximum - Enemy health will be multiplied by a random amount between these two values.

* Boss Health Range
	* Minimum and Maximum - Boss health will be multiplied by a random amount between these two values.
</details>

## Enemy Size

This randomizes the size of Enemies and Bosses.
<details>
<summary>The options for this are:</summary>

* Randomize Size
	* None - All enemies and bosses are their normal size.
	* Enemy - Size of enemies is randimized, bosses are normal.
	* Boss - Size of bosses are randomized, regular enemies are normal.
	* Both - Size of both bosses and enemies are randomized.

* Size Consistancy
	* None - The same enemy type will have inconsistant sizes.
	* Scene - The same enemy type will have a consistant size within the same scene, but may be different in another scene.
	* Enemy Type - The same enemy type will always have the same size.

* Enemy Health Range
	* Minimum and Maximum - Enemy soze will be multiplied by a random amount between these two values.

* Boss Size Range
	* Minimum and Maximum  - Boss size will be multiplied by a random amount between these two values. Values that are too small or too large may bug out the boss encounter. The recommended minumum is 85% and maximum 125%.
</details>

## Hornet's Damage

This (currently) randomizes Hornet's needle damage.
<details>
<summary>The options for this are:</summary>

* Randomize Hornet's Damage
	* On or Off - If Hornet's needle damage should be randomized.

* Damage Consistancy
	* None - Each attack will have a different damage value.
	* Needle Upgrade Level - Each needle's level will have a different damage value.
	* Per Save - Each needle level will be changed by the same amount.

* Damage Shift
	* Quantity - Damage will be shifted by a randomized value that is within + or - this value. For example, if the value is 3 then the damage will be between -3 and +3.

* Hornet Damage Minimum
	* On or Off - If Hornet's needle damage should alway be a minimum of 1.
</details>

## World Currency

This (currently) randomizes the chance for shards to drop from certain walls. These are the metallic silver walls that when struck may drop shards.

The Architect crest has a special interaction with these walls which this acounts for.
<details>
<Summary>The options for this are:</Summary>

* Drop Chance Consistancy
	* None - Every time you enter the same scene with these walls, the chance will be different.
	* Scene - The walls will always have the same chance depending on which scene you are in.
	* Per Save - The chance is the same across the entire save.

* Wall Shard Shard Chance
	* On or Off - If the drop chance for shards from non-Architect Crest hits will be modified.

* Drop Chance Multiplier
	* Minimum and Maximum - The chance for shards to drop will be multiplied by a random value between these two numbers.

* Architect Shard Drop Chance
	* On or Off - If the drop chance for shards from Architect Crest hits will be modified.

* Architect Drop Chance Multiplier
	* Minimum and Maximum - The chance for shards to drop for the Architect Crest will be multiplied by a random value between these two numbers.
</details>

### Minor Things

When changing settings, most things will not take effect until the scene changes.

You can change the seed of the current save by going into the mod's options, Current Save Information, then either reroll or manually set the seed.

The settings for the randomizers can be set in such a way to make enemies always have certain values. Examples include:
* Enemies all being tiny. (Bugs should always be smol!)
* Enemies dropping a lot of rosaries and shards upon death. (No more grinding needed!)
* Enemies not dealing any damage. (Silksong now a walking and running simulator!)
* Bosses having way more health. (I am not responsible for wrist damage from having to attack Karmalita so much!)

## Future Plans

Things that I may implement in the future may include, but is not limited to:
* Tool Damage
* World Rosary Necklaces (types and values)
* Hornet's Size
* Environmental Damage (traps, spikes, etc.)

These are not guarenteed of course. Any issues or ideas can be submitted in my GitHub.