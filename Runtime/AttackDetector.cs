#if HAS_TIMELINE_ACTION
using UnityEngine;
using UnityEngine.Events;

using BennyKok.TimelineAction;

namespace BennyKok.CombatSystem
{
    public class AttackDetector : ActionBehaviour
    {

        [Foldout("General")] public DetectType type;
        [Foldout("General")] public LayerMask layerMask;
        [Foldout("General")] public float distance;
        [Foldout("General")] public bool checkEveryUpdate;
        [Foldout("General")] public bool autoDestory;
        [Foldout("General")] public bool autoDestoryOnCollions;

        [Foldout("Damage Source")]
        public CombatEntity self;

        [Foldout("BoxCast")]
        public Vector3 boxSize = Vector3.one;

        [Foldout("Events"), CollapsedEvent]
        public UnityEvent onAttacked;

        private bool triggered;

        private Collider[] overlapResult;

        public enum DetectType
        {
            RayCast, BoxCast, SphereCast, BoxOverlap
        }

        protected override void Awake()
        {
            base.Awake();

            if (!self)
                self = GetComponentInParent<CombatEntity>();

            if ((int)type >= 3)
            {
                overlapResult = new Collider[5];
            }
        }

        private void Update()
        {
            if (checkEveryUpdate && !triggered)
            {
                if (BeginDetection())
                {
                    onAttacked.Invoke();
                    triggered = true;

                    if (autoDestory)
                    {
                        Destroy(this.gameObject);
                    }
                }
            }
        }

        public bool BeginDetection()
        {
            switch (type)
            {
                case DetectType.RayCast:
                    break;

                case DetectType.BoxCast:
                    if (Physics.BoxCast(transform.position, boxSize / 2, transform.forward, out var result, transform.rotation, distance, layerMask, QueryTriggerInteraction.Ignore))
                    {
                        CombatEntity target = null;
                        if (!result.collider.TryGetComponent<CombatEntity>(out target))
                            target = result.collider.GetComponentInParent<CombatEntity>();

                        if (self)
                            self.AttackTo(target, result.point, transform.rotation);
                        return true;
                    }
                    break;

                case DetectType.SphereCast:
                    break;

                case DetectType.BoxOverlap:
                    var hitCount = Physics.OverlapBoxNonAlloc(transform.position, boxSize / 2, overlapResult, transform.rotation, layerMask, QueryTriggerInteraction.Ignore);
                    for (int i = 0; i < hitCount; i++)
                    {
                        var t = overlapResult[i];
                        CombatEntity target = null;
                        if (!t.TryGetComponent<CombatEntity>(out target))
                            target = t.GetComponentInParent<CombatEntity>();

                        if (self)
                            self.AttackTo(target, t.transform.position, transform.rotation);
                    }
                    return hitCount > 0;
            }
            return false;
        }

        public void EndDetection()
        {

        }

        private void OnDrawGizmosSelected()
        {
            switch (type)
            {
                case DetectType.RayCast:
                    Gizmos.DrawLine(transform.position, transform.position + transform.forward * distance);
                    break;

                case DetectType.BoxCast:
                    ExtDebug.DrawBoxCastBox(transform.position, boxSize / 2, transform.forward, transform.rotation, distance, Color.white);
                    break;

                case DetectType.SphereCast:
                    break;

                case DetectType.BoxOverlap:
                    Gizmos.DrawWireCube(transform.position, boxSize);
                    break;
            }
        }

        private void OnCollisionEnter(Collision other)
        {
            if (autoDestoryOnCollions)
            {
                Destroy(this.gameObject);
            }
        }

        protected override void OnRegisterActions()
        {
            RegisterAction(BeginDetection);
            RegisterAction(EndDetection);
        }
    }

#if UNITY_EDITOR
    [UnityEditor.CustomEditor(typeof(AttackDetector))]
    public class AttackDetectorEditor : FoldoutEditor { }
#endif

    // https://answers.unity.com/questions/1156087/how-can-you-visualize-a-boxcast-boxcheck-etc.html
    public static class ExtDebug
    {
        //Draws just the box at where it is currently hitting.
        public static void DrawBoxCastOnHit(Vector3 origin, Vector3 halfExtents, Vector3 direction, Quaternion orientation, float hitInfoDistance, Color color)
        {
            origin = CastCenterOnCollision(origin, direction, hitInfoDistance);
            DrawBox(origin, halfExtents, orientation, color);
        }

        //Draws the full box from start of cast to its end distance. Can also pass in hitInfoDistance instead of full distance
        public static void DrawBoxCastBox(Vector3 origin, Vector3 halfExtents, Vector3 direction, Quaternion orientation, float distance, Color color)
        {
            direction.Normalize();
            Box bottomBox = new Box(origin, halfExtents, orientation);
            Box topBox = new Box(origin + (direction * distance), halfExtents, orientation);

            Debug.DrawLine(bottomBox.backBottomLeft, topBox.backBottomLeft, color);
            Debug.DrawLine(bottomBox.backBottomRight, topBox.backBottomRight, color);
            Debug.DrawLine(bottomBox.backTopLeft, topBox.backTopLeft, color);
            Debug.DrawLine(bottomBox.backTopRight, topBox.backTopRight, color);
            Debug.DrawLine(bottomBox.frontTopLeft, topBox.frontTopLeft, color);
            Debug.DrawLine(bottomBox.frontTopRight, topBox.frontTopRight, color);
            Debug.DrawLine(bottomBox.frontBottomLeft, topBox.frontBottomLeft, color);
            Debug.DrawLine(bottomBox.frontBottomRight, topBox.frontBottomRight, color);

            DrawBox(bottomBox, color);
            DrawBox(topBox, color);
        }

        public static void DrawBox(Vector3 origin, Vector3 halfExtents, Quaternion orientation, Color color)
        {
            DrawBox(new Box(origin, halfExtents, orientation), color);
        }
        public static void DrawBox(Box box, Color color)
        {
            Debug.DrawLine(box.frontTopLeft, box.frontTopRight, color);
            Debug.DrawLine(box.frontTopRight, box.frontBottomRight, color);
            Debug.DrawLine(box.frontBottomRight, box.frontBottomLeft, color);
            Debug.DrawLine(box.frontBottomLeft, box.frontTopLeft, color);

            Debug.DrawLine(box.backTopLeft, box.backTopRight, color);
            Debug.DrawLine(box.backTopRight, box.backBottomRight, color);
            Debug.DrawLine(box.backBottomRight, box.backBottomLeft, color);
            Debug.DrawLine(box.backBottomLeft, box.backTopLeft, color);

            Debug.DrawLine(box.frontTopLeft, box.backTopLeft, color);
            Debug.DrawLine(box.frontTopRight, box.backTopRight, color);
            Debug.DrawLine(box.frontBottomRight, box.backBottomRight, color);
            Debug.DrawLine(box.frontBottomLeft, box.backBottomLeft, color);
        }

        public struct Box
        {
            public Vector3 localFrontTopLeft { get; private set; }
            public Vector3 localFrontTopRight { get; private set; }
            public Vector3 localFrontBottomLeft { get; private set; }
            public Vector3 localFrontBottomRight { get; private set; }
            public Vector3 localBackTopLeft { get { return -localFrontBottomRight; } }
            public Vector3 localBackTopRight { get { return -localFrontBottomLeft; } }
            public Vector3 localBackBottomLeft { get { return -localFrontTopRight; } }
            public Vector3 localBackBottomRight { get { return -localFrontTopLeft; } }

            public Vector3 frontTopLeft { get { return localFrontTopLeft + origin; } }
            public Vector3 frontTopRight { get { return localFrontTopRight + origin; } }
            public Vector3 frontBottomLeft { get { return localFrontBottomLeft + origin; } }
            public Vector3 frontBottomRight { get { return localFrontBottomRight + origin; } }
            public Vector3 backTopLeft { get { return localBackTopLeft + origin; } }
            public Vector3 backTopRight { get { return localBackTopRight + origin; } }
            public Vector3 backBottomLeft { get { return localBackBottomLeft + origin; } }
            public Vector3 backBottomRight { get { return localBackBottomRight + origin; } }

            public Vector3 origin { get; private set; }

            public Box(Vector3 origin, Vector3 halfExtents, Quaternion orientation) : this(origin, halfExtents)
            {
                Rotate(orientation);
            }
            public Box(Vector3 origin, Vector3 halfExtents)
            {
                this.localFrontTopLeft = new Vector3(-halfExtents.x, halfExtents.y, -halfExtents.z);
                this.localFrontTopRight = new Vector3(halfExtents.x, halfExtents.y, -halfExtents.z);
                this.localFrontBottomLeft = new Vector3(-halfExtents.x, -halfExtents.y, -halfExtents.z);
                this.localFrontBottomRight = new Vector3(halfExtents.x, -halfExtents.y, -halfExtents.z);

                this.origin = origin;
            }


            public void Rotate(Quaternion orientation)
            {
                localFrontTopLeft = RotatePointAroundPivot(localFrontTopLeft, Vector3.zero, orientation);
                localFrontTopRight = RotatePointAroundPivot(localFrontTopRight, Vector3.zero, orientation);
                localFrontBottomLeft = RotatePointAroundPivot(localFrontBottomLeft, Vector3.zero, orientation);
                localFrontBottomRight = RotatePointAroundPivot(localFrontBottomRight, Vector3.zero, orientation);
            }
        }

        //This should work for all cast types
        static Vector3 CastCenterOnCollision(Vector3 origin, Vector3 direction, float hitInfoDistance)
        {
            return origin + (direction.normalized * hitInfoDistance);
        }

        static Vector3 RotatePointAroundPivot(Vector3 point, Vector3 pivot, Quaternion rotation)
        {
            Vector3 direction = point - pivot;
            return pivot + rotation * direction;
        }
    }
}

#endif