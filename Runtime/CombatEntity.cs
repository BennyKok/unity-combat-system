using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Playables;

using UnityEngine.UI;
using UnityEngine.Events;

#if HAS_DAMAGE_DISPLAY
using BennyKok.DamageDisplay;
#endif
using UnityEngine.EventSystems;

#if UNITY_EDITOR
using UnityEditor;
#endif

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.UI;
#endif

namespace BennyKok.CombatSystem
{
    // [RequireComponent(typeof(PlayableDirector))]
    public class CombatEntity : MonoBehaviour
    {
        [Foldout("Debug")] public bool debug;
        [Foldout("Debug")] public bool debugTimline;

        [Foldout("Skill")] public SkillSet skillSet;
        [Foldout("Skill")] public float stopTimelineThershold = 0.2f;

        [Foldout("Stats")] public bool invincible;
        [Foldout("Stats")] public float hp;
        [Foldout("Stats")] public float baseAttack;
        [Foldout("Stats")] public CoolDownValue comboAddition = new CoolDownValue(0.5f, 1f, 0.1f, 0.6f);

        [Foldout("Stats UI")] public TMPro.TextMeshProUGUI hpText;
        [Foldout("Stats UI")] public Transform hpFill;
        [Foldout("Stats UI")] public Image imageFill;
        [Foldout("Stats UI")] public Color imageColor;
        [Foldout("Stats UI")] public Image imageSecondaryFill;
        [Foldout("Stats UI")] public Color imageSecondaryColor;
        [Foldout("Stats UI")] public bool displayHpBar;
        [Foldout("Stats UI")] public bool clampHealth = true;
        [Foldout("Stats UI")] public Vector3 hpBarDisplayOffset;

#if HAS_DAMAGE_DISPLAY
        [Foldout("Stats UI")] public bool damageDisplay;
        [Foldout("Stats UI")] public Vector3 damageDisplayOffset;
#endif

        [Foldout("Destory")] public bool dontDestory;
        [Foldout("Destory")] public float destroyDelay;
        [Foldout("Destory")] public GameObject destoryTarget;

        [Foldout("Events")] public UnityEvent onDamageEvent;
        [Foldout("Events")] public UnityEvent onAddHealthEvent;
        [Foldout("Events")] public UnityEvent onDestroyEvent;

        public event Action<float, Transform> onDamage;

        [Foldout("Target")] public Transform targetPoint;

        [NonSerialized] public float fullHp;

        public bool IsAlive => hp > 0;

        public void SetMaxHp(float newHp, bool setFull = false)
        {
            fullHp = newHp;
            if (setFull)
                hp = newHp;
        }

        private Skill currentSkill;
        private Skill previousSkill;
        private float skillActivateTime;
        private float lastInputTime;
        private int currentInputComboStage = -1;
        private int currentComboStage = -1;

        private PlayableDirector _playableDirector;
        private PlayableDirector playableDirector
        {
            get
            {
                if (!_playableDirector)
                    _playableDirector = GetComponent<PlayableDirector>();

                return _playableDirector;
            }
        }

        private PlayableDirector blendPlayableDirector;

        [NonSerialized] private List<InputChain> inputBuffer = new List<InputChain>();
        [NonSerialized] private Queue<Skill> skillQueue = new Queue<Skill>();

        protected virtual void Awake()
        {
            if (skillSet)
                skillSet.Setup();

            fullHp = hp;
        }

        protected virtual void Start()
        {
            if (displayHpBar)
                BarDisplaySystem.Instance.DisplayBar(this, hpBarDisplayOffset);
        }

        public struct InputChain
        {
            public float time;
            public CombatInput input;
        }

        public enum CombatInput
        {
            A,
            B,
            C,
            D,
            E,
            F,
            G
        }

        public void OnCombatInput(CombatInput input)
        {
            inputBuffer.Add(new InputChain()
            {
                time = Time.time,
                input = input
            });

            // Debug.Log(inputBuffer.Aggregate("", (x, y) => x.Length == 0 ? y.input.ToString() : x + "," + y.input));
        }

        protected virtual void Update()
        {
            if (debug && !EventSystem.current.IsPointerOverGameObject())
            {
#if ENABLE_INPUT_SYSTEM
                if (Mouse.current.leftButton.wasPressedThisFrame) OnCombatInput(CombatInput.A);
#else
                if (Input.GetMouseButtonDown(0)) OnCombatInput(CombatInput.A);
#endif
            }

            if (hpFill)
            {
                // hpFill.fillAmount = hp / defaultHp;
                var scale = hpFill.localScale;
                scale.x = Mathf.Lerp(scale.x, hp / fullHp, 10 * Time.unscaledDeltaTime);
                hpFill.localScale = scale;
            }
            if (imageFill)
            {
                var fill = Mathf.Lerp(imageFill.fillAmount, hp / fullHp, 10 * Time.unscaledDeltaTime);
                imageFill.fillAmount = fill;
            }
            if (imageSecondaryFill && (Time.unscaledTime - lastDamageTime) > 0.5f)
            {
                imageSecondaryFill.fillAmount = Mathf.Lerp(imageSecondaryFill.fillAmount, hp / fullHp, 3f * Time.unscaledDeltaTime);
            }
            if (hpText)
                hpText.text = $"{hp}/{fullHp}";
            comboAddition.Tick();

            // ClearInvalidInputBuffer();
            UpdateCombo();
            UpdateSkillQueue();
        }

        public void Kill()
        {
            OnDamage(hp);
        }

        public void OnAddHealth(float pt)
        {
            hp += pt;
            if (clampHealth) hp = Mathf.Clamp(hp, 0, fullHp);
            onAddHealthEvent.Invoke();
        }

        public void ResetHealth()
        {
            hp = fullHp;
            onAddHealthEvent.Invoke();
        }

        public bool OnDamage(float pt)
        {
            return OnDamage(pt, null);
        }

        public bool OnDamage(float pt, Transform from)
        {
            return OnDamage(pt, from, 1);
        }

        public virtual bool AttackTo(CombatEntity otherEntity)
        {
            return AttackTo(otherEntity, transform.position, Quaternion.LookRotation(transform.forward));
        }

        public virtual bool AttackTo(CombatEntity otherEntity, Vector3 position, Quaternion forward)
        {
            var atk = baseAttack + baseAttack * comboAddition.value;
            comboAddition.OnAction();

            return otherEntity.OnDamage(atk, transform, 0.5f);
        }

        private void UpdateCombo()
        {
            if (inputBuffer.Count > 0)
            {
                foreach (var skillEntry in skillSet.skills)
                {
                    if (inputBuffer.Count > currentInputComboStage && CompareInput(inputBuffer, skillEntry.input))
                    {
                        currentInputComboStage = inputBuffer.Count;
                        lastInputTime = Time.time;
                        // Enqueue the skill here, after the current combo is ended
                        skillQueue.Enqueue(skillEntry);
                        break;
                    }
                }
            }
        }

        public void UpdateSkillQueue()
        {
            if (previousSkill != null && Time.time - skillActivateTime >= stopTimelineThershold)
            {
                // Debug.Log("Stop Skill");
                previousSkill.Stop();
                previousSkill = null;
            }
            if (skillQueue.Count > 0)
            {
                // If current skill is to end / or later fall into the recovery range
                if (currentSkill != null && Time.time - skillActivateTime > currentSkill.GetRecoveryTime())
                {
                    // currentSkill.skill.Pause();
                    if (previousSkill != null)
                        previousSkill.Stop();
                    previousSkill = currentSkill;

                    currentSkill = null;
                }

                // Get a new skill
                if (currentSkill == null)
                {
                    currentSkill = skillQueue.Dequeue();
                    currentComboStage = skillQueue.Count;

                    // Play the new skill
                    skillActivateTime = Time.time;

                    currentSkill.skill.time = 0;
                    currentSkill.skill.Play();

#if UNITY_EDITOR
                    if (debugTimline)
                        Selection.activeGameObject = currentSkill.skill.gameObject;
#endif

                    // playableDirector.Play(currentSkill.skill.timeline, DirectorWrapMode.None);

                    Debug.Log($"Trigger Combo: {currentSkill.skill.name}");
                }
            }

            if (currentComboStage == 0 && currentSkill != null && Time.time - skillActivateTime > currentSkill.skill.duration)
            {
                // Current skill has ended and no upcoming skill
                CleanUpCombat();
            }
        }

        public bool CompareInput(List<InputChain> buffer, List<CombatInput> target)
        {
            if (buffer.Count == target.Count)
            {
                for (int i = 0; i < buffer.Count; i++)
                {
                    if (buffer[i].input != target[i]) return false;
                }
                return true;
            }
            return false;
        }

        protected virtual void CleanUpCombat()
        {
            // currentSkill?.Stop();
            currentSkill = null;
            skillActivateTime = -1;
            currentInputComboStage = -1;
            currentComboStage = -1;
            ClearInputBuffer();
        }

        public void StopAttack()
        {
            if (currentSkill != null)
                currentSkill.Stop();

            CleanUpCombat();
        }

        public void ClearInputBuffer()
        {
            inputBuffer.Clear();
            // Debug.Log(inputBuffer.Aggregate("", (x, y) => x.Length == 0 ? y.input.ToString() : x + "," + y.input));
        }

        public void ClearInvalidInputBuffer()
        {
            for (int i = inputBuffer.Count - 1; i >= 0; i--)
            {
                var inputBuf = inputBuffer[i];
                var timeDiff = Time.time - inputBuf.time > 0.5f;

                if (timeDiff)
                {
                    inputBuffer.RemoveAt(i);
                    Debug.Log(inputBuffer.Aggregate("", (x, y) => x.Length == 0 ? y.input.ToString() : x + "," + y.input));
                }
            }
        }

        public virtual void ContinueDestory()
        {
            if (destoryTarget)
                Destroy(destoryTarget, destroyDelay);
            else
                Destroy(gameObject, destroyDelay);
        }

        private float lastDamageTime;

        public virtual bool OnDamage(float pt, Transform from, float impact, float impactTime = 0.2f)
        {
            if (invincible) return false;
            if (hp <= 0) return false;

            hp -= pt;
            lastDamageTime = Time.unscaledTime;

            if (clampHealth) hp = Mathf.Clamp(hp, 0, fullHp);

            onDamageEvent.Invoke();
            onDamage?.Invoke(pt, from);
#if HAS_DAMAGE_DISPLAY
            if (damageDisplay)
                DamageDisplaySystem.Instance.DisplayDamage(transform, damageDisplayOffset, pt);
#endif
            if (hp <= 0)
            {
                onDestroyEvent.Invoke();
                if (!dontDestory)
                {
                    ContinueDestory();
                }
            }

            return true;
        }
    }

#if UNITY_EDITOR
    [CustomEditor(typeof(CombatEntity), true)]
    public class CombatEntityEditor : FoldoutEditor { }
#endif
}