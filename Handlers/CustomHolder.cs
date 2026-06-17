#if DEBUG
using System;
using UnityEngine;

namespace Cute_Randomizer.Handlers
{
    internal class CustomHolder : SavedItem
    {
        private GameObject _heldGameObject;
        public GameObject GameObject
        { get => _heldGameObject; set => _heldGameObject = _heldGameObject != null ? _heldGameObject : value;
        }

        public override bool CanGetMore()
        {
            return true;
        }

        public override void Get(bool showPopup = true)
        {
            if (showPopup)
            {
                CollectableUIMsg.Spawn(new UIMsgDisplay
                {
                    Name = "Test",
                    Icon = null,
                    IconScale = 1,
                    RepresentingObject = this
                }, Color.white);
                CollectableItemHeroReaction.DoReaction(); // May not need?
            }
        }

        internal CustomHolder SetGameObject(GameObject obj)
        {
            if (_heldGameObject != null) throw new ArgumentException("GameObject was already set. Create a new CustomHolder instead.");
            GameObject = obj;
            return this;
        }
    }
}
#endif