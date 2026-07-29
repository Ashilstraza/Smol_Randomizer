#if TESTING
using System;
using System.Collections.Generic;

namespace Smol_Randomizer.Handlers;

internal class MultiObjectHolder
{
    private int _count;

    public MultiObjectHolder(SavedItem item, HashSet<Location> locations, int count)
    {
        Count = count;
        Item = item;
        Locations = locations;
    }

    public SavedItem Item { get; }
    public HashSet<Location> Locations { get; private set; }
    public HashSet<Location> OldLocations { get; private set; }
    private HashSet<string> holders = [];
    public HashSet<string> Holders
    {
        get
        {
            if (holders.Count != Locations.Count)
            {
                RefreshHolders();
            }

            return holders;
        }
    }
    public int Count
    {
        get => _count;
        set
        {
            if (value < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "count is less than 1");
            }

            _count = value;
        }
    }

    public bool IsDuplicate(string scene, string holderName)
    {
        foreach (Location location in Locations)
        {
            if (location.Scene == scene && location.HolderName == holderName)
            {
                return true;
            }
        }

        return false;
    }

    public void SetNewLocations(HashSet<Location> newLocations)
    {
        OldLocations = Locations;
        Locations = newLocations;

    }

    private void RefreshHolders()
    {
        holders = [];

        foreach (Location location in Locations)
        {
            holders.Add(location.HolderName);
        }
    }
}

internal class Location(string scene, string holderType, string holderName)
{
    public string Scene { get; } = scene;
    public string HolderType { get; } = holderType;
    public string HolderName { get; } = holderName;
}
#endif