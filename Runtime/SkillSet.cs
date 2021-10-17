using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace BennyKok.CombatSystem
{
    [System.Serializable]
    public class SkillSet : MonoBehaviour
    {
        [NonSerialized] public Skill[] skills;

        public void Setup()
        {
            skills = GetComponentsInChildren<Skill>();
            foreach (var skillEntry in skills)
            {
                skillEntry.Setup();
            }
        }
    }
}