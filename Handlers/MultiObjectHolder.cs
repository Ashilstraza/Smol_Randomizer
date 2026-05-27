using System;
using System.Collections.Generic;

namespace Cute_Randomizer.Handlers
{
    internal class MultiObjectHolder
    {
        private readonly SavedItem _item;
        private HashSet<Location> _locations;
        private HashSet<Location> oldLocations;
        private int _count;

        public MultiObjectHolder(SavedItem item, HashSet<Location> locations, int count)
        {
            Count = count;
            _item = item;
            _locations = locations;
        }

        public SavedItem Item => _item;
        public HashSet<Location> Locations => _locations;
        public HashSet<Location> OldLocations => oldLocations;
        private HashSet<string> holders = [];
        public HashSet<string> Holders
        {
            get
            {
                if (holders.Count != _locations.Count)
                {
                    RefreshHolders();
                }

                return holders;
            }
        }
        public int Count
        {
            get
            {
                return _count;
            }
            set
            {
                if (value < 1) throw new ArgumentOutOfRangeException(nameof(value), "count is less than 1");
                _count = value;
            }
        }

        public bool IsDuplicate(string scene, string holderName)
        {
            foreach (var location in _locations)
            {
                if (location.Scene == scene && location.HolderName == holderName) return true;
            }
            return false;
        }

        public void SetNewLocations(HashSet<Location> newLocations)
        {
            oldLocations = _locations;
            _locations = newLocations;

        }

        private void RefreshHolders()
        {
            holders = [];

            foreach (var location in _locations)
            {
                holders.Add(location.HolderName);
            }
        }
    }

    internal class Location(string scene, string holderType, string holderName)
    {
        private readonly string _scene = scene;
        private readonly string _holderType = holderType;
        private readonly string _holderName = holderName;

        public string Scene => _scene;
        public string HolderType => _holderType;
        public string HolderName => _holderName;
    }
}
