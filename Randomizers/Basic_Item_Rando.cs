#if DEBUG
using Cute_Randomizer.Handlers;
using HarmonyLib;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Cute_Randomizer.Randomizers
{
    internal class Basic_Item_Rando
    {
        private static Scene currentScene;

        private static readonly Randomizer_Info randomizerItem;
        private static readonly Randomizer_Info randomizerOnSceneLoad = new("Basic Item Randomizer", RandomizerEventType.OnSceneLoad, AccessTools.Method(typeof(Basic_Item_Rando), nameof(OnSceneLoad)));
        private static readonly Randomizer_Info randomizerGameShutdown = new("Basic Item Randomizer", RandomizerEventType.GameShutdown, AccessTools.Method(typeof(Basic_Item_Rando), nameof(GameShutdown)));

        internal static void InitRandomizer()
        {
            if (!Cute_Rando_Core.RegisterRandomizer(randomizerOnSceneLoad)) return;
            if (!Cute_Rando_Core.RegisterRandomizer(randomizerGameShutdown)) return;

            ImportWorldObjectsFile();
        }

        private static void OnSceneLoad(Scene scene, LoadSceneMode mode)
        {
            currentScene = scene;

            Dictionary<string, HashSet<SavedItem>> savedItemDictionary = [];

            foreach (var savedItem in Resources.FindObjectsOfTypeAll<SavedItem>())
            {
                if (savedItemDictionary.TryGetValue(savedItem.GetType().ToString(), out HashSet<SavedItem> set))
                {
                    set.Add(savedItem);
                    savedItemDictionary[savedItem.GetType().ToString()] = set;
                }
                else savedItemDictionary.Add(savedItem.GetType().ToString(), [savedItem]);
            }

            UpdateObjects(scene.GetRootGameObjects());

            if (!Cute_Rando_Core.testing) return;
            LocalRandomize();
        }

        private static void GameShutdown()
        {
            ExportWorldObjectsFile();
        }

        private static void UpdateObjects(GameObject[] objects)
        {
            objectHolders.Clear();
            lastObjectSet.Clear();
            unprocessedObjects.Clear();
            ignoredObjects.Clear();
            RecursiveObjectUpdater(objects);
        }

        /// <summary>
        /// Processes the given GameObject array and adds the items into their respective lists. Will process the list to iterate over the black threaded and normal versions of the world.
        /// </summary>
        /// <param name="objects">the array of objects to process</param>
        /// <param name="blackThread">if we are recursively adding to black thread versions of the scene</param>
        /// <param name="normalWorld">if we are recursively adding to the normal world versions of the scene</param>
        private static void RecursiveObjectUpdater(GameObject[] objects, bool blackThread = false, bool normalWorld = false)
        {
            HashSet<GameObject> objectChildren = [];
            HashSet<GameObject> btObjectChildren = [];
            HashSet<GameObject> nwObjectChildren = [];
            string workingScene = currentScene.name;
            if (blackThread) workingScene += "(BlackThread)";
            if (normalWorld) workingScene += "(NormalWorld)";

            foreach (var obj in objects)
            {
                bool added = false;

                if (obj.GetComponent<GeoRock>())
                {
                    AddToList(workingScene, obj, worldObjects["geoRocks"], ref added);
                }
                if (obj.GetComponent<BreakableHolder>())
                {
                    AddToList(workingScene, obj, worldObjects["breakableHolders"], ref added);
                }
                if (obj.GetComponent<HealthManager>())
                {
                    AddToList(workingScene, obj, worldObjects["enemies"], ref added);
                }
                if (obj.GetComponent<ShopMenuStock>())
                {
                    AddToList(workingScene, obj, worldObjects["shopMenus"], ref added);
                }
                if (obj.GetComponent<RosaryCacheHanging>())
                {
                    AddToList(workingScene, obj, worldObjects["rosaryStrings"], ref added);
                }
                if (obj.GetComponent<GeoControl>())
                {
                    AddToList(workingScene, obj, worldObjects["floorGeo"], ref added);
                    AddToObjectHolders(obj, typeof(GeoControl), workingScene);
                }
                if (obj.GetComponent<TransitionPoint>())
                {
                    AddToList(workingScene, obj, worldObjects["transitionPoints"], ref added);
                }
                if (obj.GetComponent<RestBench>())
                {
                    AddToList(workingScene, obj, worldObjects["restBenches"], ref added);
                }
                if (obj.GetComponent<RosaryCacheShrine>())
                {
                    AddToList(workingScene, obj, worldObjects["rosaryShrines"], ref added);
                }
                if (obj.GetComponent<PersistentBoolItem>())
                {
                    AddToList(workingScene, obj, worldObjects["persistentItems"], ref added);

                }
                if (obj.GetComponent<CollectableItemPickup>())
                {
                    AddToList(workingScene, obj, worldObjects["collectableItems"], ref added);
                    AddToObjectHolders(obj, typeof(CollectableItemPickup), workingScene);
                }
                if (obj.GetComponent<SavedItemTrackerMarker>())
                {
                    AddToList(workingScene, obj, worldObjects["savedItemThingy"], ref added);
                    AddToObjectHolders(obj, typeof(SavedItemTrackerMarker), workingScene);
                }
                if (obj.GetComponent<Breakable>())
                {
                    AddToList(workingScene, obj, worldObjects["breakable"], ref added);
                }
                // Boss Additive Loader

                if (obj.CompareTag("Mapper NPC") || obj.GetComponent<PlayMakerNPC>())
                {
                    AddToList(workingScene, obj, worldObjects["NPCs"], ref added);
                }

                for (int i = 0; i < obj.transform.childCount; i++)
                {
                    if (blackThread || obj.transform.GetChild(i).gameObject.name.Equals("Black Thread World"))
                    {
                        btObjectChildren.Add(obj.transform.GetChild(i).gameObject);
                    }
                    else if (normalWorld || obj.transform.GetChild(i).gameObject.name.Equals("Normal World"))
                    {
                        nwObjectChildren.Add(obj.transform.GetChild(i).gameObject);
                    }
                    else objectChildren.Add(obj.transform.GetChild(i).gameObject);
                }

                if (!added)
                {
                    // add ignores here
                    if (obj.GetComponent<HeroCorpseMarker>() ||
                        obj.GetComponent<ParticleSystem>() ||
                        obj.GetComponent<TrackTriggerObjects>() ||
                        obj.GetComponent<AlertRange>() ||
                        obj.name.StartsWith("terrain collider", StringComparison.CurrentCultureIgnoreCase) ||
                        obj.name.StartsWith("temp fog", StringComparison.CurrentCultureIgnoreCase) ||
                        obj.name.StartsWith("lava haze", StringComparison.CurrentCultureIgnoreCase) ||
                        obj.GetComponent<HazardRespawnMarker>() ||
                        obj.GetComponent<NoClamberRegion>() ||
                        obj.GetComponent<Roof>() ||
                        obj.GetComponent<EnviroRegion>() ||
                        obj.GetComponent<NoTeleportRegion>() ||
                        obj.GetComponent<SendEnemyMessageTrigger>() ||
                        obj.GetComponent<GrassWind>() ||
                        obj.GetComponent<HitResponse>() ||
                        obj.GetComponent<TriggerEnterEvent>() ||
                        obj.GetComponent<PushableRubble>() ||
                        obj.layer.Equals(0) || // probably decorations, or unsorted?
                        obj.layer.Equals(1) || // vignettes
                        obj.layer.Equals(11) || // enemy fighting helpers?
                        obj.layer.Equals(13) || // detectors?
                        obj.layer.Equals(14) || // friendly npc fighting helpers?
                        obj.layer.Equals(15) || // detectors?
                        obj.layer.Equals(16) || // possession effect, garmond bind?
                        obj.layer.Equals(17) || // damaging things?
                        obj.layer.Equals(18) || // debris?
                        obj.layer.Equals(19) || // rosary string pieces and house pieces?
                        obj.layer.Equals(21) || // grass and spit?
                        obj.layer.Equals(22) || // damager?
                        obj.layer.Equals(24) || // tink detector?
                        obj.layer.Equals(25) || // moss clumps?
                        obj.layer.Equals(26) || // corpses?
                        obj.layer.Equals(27) || // physics pushers?
                        obj.layer.Equals(30)) // rosary string pieces?
                    {
                        if (ignoredObjects.TryGetValue(obj.layer, out HashSet<GameObject> value))
                        {
                            value.Add(obj);
                            ignoredObjects[obj.layer] = value;
                        }
                        else ignoredObjects.Add(obj.layer, [obj]);
                    }
                    else
                    {
                        if (unprocessedObjects.TryGetValue(obj.layer, out HashSet<GameObject> value))
                        {
                            value.Add(obj);
                            unprocessedObjects[obj.layer] = value;
                        }
                        else unprocessedObjects.Add(obj.layer, [obj]);
                    }
                }
                else
                {
                    if (lastObjectSet.TryGetValue(obj.layer, out HashSet<GameObject> value))
                    {
                        value.Add(obj);
                        lastObjectSet[obj.layer] = value;
                    }
                    else lastObjectSet.Add(obj.layer, [obj]);
                }
            }
            if (objectChildren.Count > 0) RecursiveObjectUpdater([.. objectChildren]);
            if (btObjectChildren.Count > 0) RecursiveObjectUpdater([.. btObjectChildren], blackThread: true);
            if (nwObjectChildren.Count > 0) RecursiveObjectUpdater([.. nwObjectChildren], normalWorld: true);
        }

        private static void LocalRandomize()
        {
            List<MultiObjectHolder> tempObjectHolders = [.. objectHolders];
            List<MultiObjectHolder> newObjectHolder = [];
            System.Random rng = new(8675309);
            bool done = tempObjectHolders.Count == 0;
            int currentTempObject;

            while (!done)
            {
                currentTempObject = rng.Next(0, tempObjectHolders.Count);
                newObjectHolder.Add(tempObjectHolders[currentTempObject]);
                tempObjectHolders.RemoveAt(currentTempObject);
                if (tempObjectHolders.Count == 0) done = true;
            }

            tempObjectHolders = [.. objectHolders];

            for (int i = 0; i < objectHolders.Count; i++)
            {
                newObjectHolder[i].SetNewLocations(tempObjectHolders[i].Locations);
            }

            foreach (var gameObjectSet in lastObjectSet)
            {
                foreach (var obj in gameObjectSet.Value)
                {
                    if (newObjectHolder.FirstOrDefault(o => o.Holders.Contains(obj.name)) is var objectHolder and not null)
                    {
                        if (obj.GetComponent<CollectableItemPickup>())
                        {
                            obj.GetComponent<CollectableItemPickup>().SetItem(objectHolder.Item);
                        }
                        else if (obj.GetComponent<SavedItemTrackerMarker>())
                        {
                            /*foreach (SavedItem item in items)
                            {
                                Adder(item);
                            }*/
                        }
                        else if (obj.GetComponent<GeoControl>())
                        {

                        }
                    }
                }
            }
        }

        private static readonly HashSet<MultiObjectHolder> objectHolders = [];
        /// <summary>
        /// List of objects we are ignoring
        /// </summary>
        private static readonly SortedDictionary<int, HashSet<GameObject>> ignoredObjects = [];
        /// <summary>
        /// List of objects we haven't sorted
        /// </summary>
        private static readonly SortedDictionary<int, HashSet<GameObject>> unprocessedObjects = [];
        /// <summary>
        /// Last set of objects added into the world object dictionary
        /// </summary>
        private static readonly SortedDictionary<int, HashSet<GameObject>> lastObjectSet = [];
        /// <summary>
        /// Master dictionary of all recorded world objects
        /// </summary>
        private static Dictionary<string, Dictionary<string, HashSet<string>>> worldObjects = new()
        {
            {"geoRocks", []}, // GeoRock
            {"breakableHolders", []}, // BreakableHolder
            {"NPCs", []}, // "Mapper NPC" "Mr Mushroom NPC" PlayMakerNPC?
            {"shopMenus", []}, // ShopMenuStock
            {"enemies", []}, // HealthManager
            {"rosaryStrings", []}, // RosaryCacheHanging
            {"rosaryShrines", []}, // RosaryCacheShrine
            {"floorGeo", []}, // GeoControl
            {"transitionPoints", []}, // TransitionPoint
            {"restBenches", []}, // RestBench
            {"persistentItems", []}, // PersistentBoolItem
            {"collectableItems", []}, // CollectableItemPickup
            {"savedItemThingy", []}, // SavedItemTrackerMarker
            {"breakable", []} // Breakable
        };

        private static void AddToObjectHolders(GameObject obj, Type componentType, string currentScene)
        {
            if (componentType.Equals(typeof(SavedItemTrackerMarker)))
            {
                SavedItem[] items = [.. obj.GetComponent<SavedItemTrackerMarker>().Items];
                foreach (SavedItem item in items)
                {
                    Adder(item);
                }
            }
            else if (componentType.Equals(typeof(CollectableItemPickup)))
            {
                SavedItem item = obj.GetComponent<CollectableItemPickup>().Item;
                Adder(item);
            }
            else if (componentType.Equals(typeof(GeoControl)))
            {
                CustomHolder item = ScriptableObject.CreateInstance<CustomHolder>().SetGameObject(obj);
                Adder(item);
            }

            void Adder(SavedItem item)
            {
                if (objectHolders.FirstOrDefault(o => o.Item == item) is var objectHolder and not null)
                {
                    if (objectHolder.IsDuplicate(currentScene, obj.name))
                    {
                        objectHolder.Locations.Add(new Location(currentScene, componentType.Name, obj.name));
                    }
                }
                else objectHolders.Add(new MultiObjectHolder(item, [new Location(currentScene, componentType.Name, obj.name)], 1));
            }
        }

        /// <summary>
        /// Handles adding new world objects into the master list
        /// </summary>
        /// <param name="scene">the scene we are recording</param>
        /// <param name="obj">the item being recorded</param>
        /// <param name="list">the list within the master list we are adding into</param>
        private static void AddToList(string scene, GameObject obj, Dictionary<string, HashSet<string>> list, ref bool added)
        {
            if (list.TryGetValue(scene, out HashSet<string> value)) // check to see if we have this scene in the list already
            {
                value.Add(obj.name);
                list[scene] = value;
            }
            else list.Add(scene, [obj.name]); // add this scene into the list
            added = true;
        }

        /// <summary>
        /// Imports the master world object file so we don't have to rebuild it constantly
        /// </summary>
        internal static void ImportWorldObjectsFile()
        {
            Cute_Rando_Core.ImportJsonFile("World_Objects", out string tempWorldObjects);

            worldObjects = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, HashSet<string>>>>(File.ReadAllText(tempWorldObjects)) ?? [];
        }

        /// <summary>
        /// Exports the master world object file so we don't have to rebuild it constantly
        /// </summary>
        internal static void ExportWorldObjectsFile()
        {
            if (worldObjects != null) Cute_Rando_Core.ExportJsonFile("World_Objects", worldObjects);
        }
    }
}
#endif