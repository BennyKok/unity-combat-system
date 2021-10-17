using System.Collections.Generic;
using BennyKok.DamageDisplay;
using UnityEngine;

namespace BennyKok.CombatSystem
{
    public class BarDisplaySystem : PoolSystem<BarDisplaySystem, BarView>
    {
        [Header("System")]
        public Camera targetCamera;

        protected override void Awake()
        {
            base.Awake();

            if (!targetCamera)
                targetCamera = Camera.main;
        }

        public void DisplayBar(CombatEntity target, Vector3 offset)
        {
            var view = GetAvailableItem();
            view.OnDisplay(target, offset);
            // view.ResetToPool(viewLifetime);
        }
    }
}