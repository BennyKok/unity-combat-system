using BennyKok.DamageDisplay;
using UnityEngine;
using UnityEngine.UI;

namespace BennyKok.CombatSystem
{
    [RequireComponent(typeof(CanvasGroup))]
    public class BarView : Poolable
    {
        public GameObject uiParent;
        public TMPro.TextMeshProUGUI label;
        public Image imageFill;
        public Image imageSecondaryFill;

        public bool useCustomColor;

        [System.NonSerialized]
        public CombatEntity target;

        private BarDisplaySystem system;

        private Vector3 worldOffset;

        private Vector3 offset;

        private bool fading;

        private CanvasGroup canvasGroup;

        private Vector3 lastTransfromPosition;

        private void Awake()
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }

        public void OnDisplay(CombatEntity target, Vector3 offset)
        {
            transform.localScale = new Vector3(1, 1, 1);
            target.onDestroyEvent.AddListener(ResetToPoolInstant);
            target.imageFill = imageFill;
            target.imageSecondaryFill = imageSecondaryFill;
            target.hpText = label;

            if (useCustomColor)
            {
                target.imageFill.color = target.imageColor;
                target.imageSecondaryFill.color = target.imageSecondaryColor;
            }

            this.target = target;

            worldOffset = offset;
        }

        private void OnEnable()
        {
            system = BarDisplaySystem.Instance;
        }

        private void Update()
        {
            if (target)
            {
                lastTransfromPosition = target.transform.position;
            }
            else
            {
                if (!fading && used)
                    ResetToPoolInstant();
            }

            var worldPoint = lastTransfromPosition + worldOffset;
            var screenPos = system.targetCamera.WorldToScreenPoint(worldPoint);
            var isBehind = Vector3.Dot(system.targetCamera.transform.forward, worldPoint - system.targetCamera.transform.position) < 0;

            uiParent.gameObject.SetActive(!isBehind);

            transform.position = screenPos;

            if (fading)
            {
                canvasGroup.alpha -= Time.deltaTime / 0.2f;

                if (canvasGroup.alpha <= 0)
                {
                    fading = false;
                    ResetInternal();
                }
            }
        }

        protected override void ResetToPoolInstant()
        {
            fading = true;
        }

        private void ResetInternal()
        {
            base.ResetToPoolInstant();

            if (target)
            {
                target.onDestroyEvent.RemoveListener(ResetToPoolInstant);
                target.imageFill = null;
                target.hpText = null;
            }

            offset = Vector3.zero;
            canvasGroup.alpha = 1;
        }
    }
}