using System;
using UnityEngine;
using UnityEngine.Audio;

namespace BlockShooter
{
    public enum MacaronFeedbackEvent { SelectTray, InvalidTray, TrayArrived, CakeLanded, TrayPacked, UnlockSlot, Win, Lose }

    [CreateAssetMenu(menuName = "Macaron Factory/Feedback Config")]
    public sealed class MacaronFeedbackConfig : ScriptableObject
    {
        [Serializable]
        public sealed class Cue
        {
            public MacaronFeedbackEvent trigger;
            public GameObject effectPrefab;
            public AudioClip sound;
            [Range(0, 1)] public float volume = .65f;
            [Range(.5f, 2)] public float pitch = 1;
            [Min(.01f)] public float effectScale = .3f;
            public Vector3 offset = new Vector3(0, .15f, 0);
            [Range(1, 8)] public int poolSize = 2;
            [Min(.01f)] public float cooldown = .06f;
            [Min(.1f)] public float maxDuration = 5;
        }
        public bool effectsEnabled = true;
        public bool soundEnabled = true;
        [Range(0, 1)] public float masterVolume = .75f;
        public AudioMixerGroup mixerGroup;
        public Cue[] cues = Array.Empty<Cue>();
    }
}
