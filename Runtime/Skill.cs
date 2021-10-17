using System;
using UnityEngine;
using UnityEngine.Timeline;
using UnityEngine.Playables;
using System.Collections.Generic;
using UnityEngine.Events;

#if HAS_TIMELINE_ACTION
using BennyKok.TimelineAction;
#endif

namespace BennyKok.CombatSystem
{
    public class Skill : MonoBehaviour
    {
        public List<CombatEntity.CombatInput> input;
        public UnityEvent onSkillInterrupt;
        [NonSerialized] public PlayableDirector skill;

#if HAS_TIMELINE_ACTION
        [NonSerialized] public ActionMarker recoveryMarker;
#endif

        public void Setup()
        {
            skill = GetComponent<PlayableDirector>();

#if HAS_TIMELINE_ACTION
            foreach (var marker in (skill.playableAsset as TimelineAsset).markerTrack.GetMarkers())
            {
                if (marker is ActionMarker actionMarker)
                {
                    if (actionMarker.name == "Recovery")
                        recoveryMarker = actionMarker;
                }
            }
#endif
        }

        public double GetRecoveryTime()
        {
#if HAS_TIMELINE_ACTION
            return recoveryMarker.time;
#else
            return 0.0;
#endif
        }

        public void Stop()
        {
            onSkillInterrupt.Invoke();
            skill.Stop();
        }
    }
}