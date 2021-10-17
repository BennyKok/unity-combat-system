using UnityEngine;
using System;
using UnityEngine.UI;

namespace BennyKok.CombatSystem
{
    [Serializable]
    public class CoolDownValue
    {
        public float coolDownTriggerDelay = 0.5f;
        public float maxValue = 1;
        public float actionValue = 0.1f;
        public float recoveryTime = 2f;

        [Header("UI")]
        public bool inverseFill;
        public Image fill;
        public Image secondaryFill;


        [NonSerialized]
        public float value;

        private float lastActionTime;

        public CoolDownValue() { }

        public CoolDownValue(float coolDownTriggerDelay, float maxValue, float actionValue, float recoveryTime)
        {
            this.coolDownTriggerDelay = coolDownTriggerDelay;
            this.maxValue = maxValue;
            this.actionValue = actionValue;
            this.recoveryTime = recoveryTime;
        }

        public void SetValues(float coolDownTriggerDelay, float maxValue, float actionValue, float recoveryTime)
        {
            this.coolDownTriggerDelay = coolDownTriggerDelay;
            this.maxValue = maxValue;
            this.actionValue = actionValue;
            this.recoveryTime = recoveryTime;

            value = 0;
        }

        //Call on update
        public void Tick()
        {
            if ((Time.time - lastActionTime) > coolDownTriggerDelay)
            {
                if (recoveryTime == 0)
                    value = 0;
                else
                    value -= Time.deltaTime / recoveryTime;
                ClampValue();
            }

            var v = value / maxValue;
            if (inverseFill)
                v = 1 - v;

            if (fill)
                fill.fillAmount = v;

            if (secondaryFill && (Time.time - lastActionTime) > 0.2f)
            {
                secondaryFill.fillAmount = Mathf.Lerp(secondaryFill.fillAmount, v, 3f * Time.deltaTime);
            }
        }

        //Calll when an action has done
        public void OnAction()
        {
            lastActionTime = Time.time;
            value += actionValue;
            ClampValue();
        }

        //Call to check if can action
        public bool CanAction() => (value + actionValue) <= maxValue;

        //Call to check threshold reached
        public bool ThresholdReached() => value == maxValue;

        private void ClampValue()
        {
            value = Mathf.Clamp(value, 0, maxValue);
        }

        public void Reset()
        {
            lastActionTime = Time.time;
            value = 0;
        }

    }
}