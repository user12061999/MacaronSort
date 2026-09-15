using System.Collections.Generic;
using UnityEngine;

namespace BlockShooter
{
    public sealed class MacaronFeedbackPlayer : MonoBehaviour
    {
        public MacaronFeedbackConfig config;
        private sealed class Effect
        {
            public GameObject root;
            public ParticleSystem[] particles;
            public MacaronFeedbackConfig.Cue cue;
            public bool active;
            public float until;
        }
        private readonly List<Effect> _effects = new();
        private readonly Dictionary<MacaronFeedbackEvent, MacaronFeedbackConfig.Cue> _cues = new();
        private readonly Dictionary<MacaronFeedbackEvent, float> _last = new();
        private readonly AudioSource[] _voices = new AudioSource[8];

        private void Start()
        {
            if (config == null) return;
            for (int i = 0; i < _voices.Length; i++)
            {
                var voice = gameObject.AddComponent<AudioSource>();
                voice.playOnAwake = false;
                voice.spatialBlend = 0;
                voice.outputAudioMixerGroup = config.mixerGroup;
                _voices[i] = voice;
            }
            foreach (var cue in config.cues)
            {
                if (cue == null || _cues.ContainsKey(cue.trigger)) continue;
                _cues.Add(cue.trigger, cue);
                if (cue.effectPrefab == null) continue;
                for (int i = 0; i < Mathf.Clamp(cue.poolSize, 1, 8); i++)
                {
                    var go = Instantiate(cue.effectPrefab, transform);
                    var particles = go.GetComponentsInChildren<ParticleSystem>(true);
                    foreach (var particle in particles)
                    {
                        var main = particle.main;
                        main.loop = false;
                        main.playOnAwake = false;
                        main.stopAction = ParticleSystemStopAction.None;
                        particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    }
                    go.SetActive(false);
                    _effects.Add(new Effect { root = go, particles = particles, cue = cue });
                }
            }
        }

        public void Play(MacaronFeedbackEvent trigger, Vector3 position)
        {
            if (config == null || !_cues.TryGetValue(trigger, out var cue)) return;
            if (_last.TryGetValue(trigger, out var last) && Time.unscaledTime - last < Mathf.Max(.01f, cue.cooldown)) return;
            _last[trigger] = Time.unscaledTime;
            if (config.soundEnabled && cue.sound != null)
                foreach (var voice in _voices)
                    if (voice != null && !voice.isPlaying)
                    {
                        voice.pitch = Mathf.Clamp(cue.pitch, .5f, 2);
                        voice.volume = Mathf.Clamp01(config.masterVolume * cue.volume);
                        voice.clip = cue.sound;
                        voice.Play();
                        break;
                    }
            if (!config.effectsEnabled) return;
            foreach (var effect in _effects)
                if (effect.cue == cue && !effect.active)
                {
                    effect.root.transform.SetPositionAndRotation(position + cue.offset, Quaternion.identity);
                    effect.root.transform.localScale = cue.effectPrefab.transform.localScale * Mathf.Max(.01f, cue.effectScale);
                    effect.root.SetActive(true);
                    foreach (var particle in effect.particles) particle.Play(false);
                    effect.active = true;
                    effect.until = Time.time + Mathf.Max(.1f, cue.maxDuration);
                    break;
                }
        }

        public bool IsEffectPlaying(MacaronFeedbackEvent trigger)
        {
            foreach (var effect in _effects)
                if (effect.active && effect.cue.trigger == trigger) return true;
            return false;
        }

        private void Update()
        {
            foreach (var effect in _effects)
            {
                if (!effect.active) continue;
                bool alive = false;
                foreach (var particle in effect.particles) if (particle.IsAlive(false)) { alive = true; break; }
                if (alive && Time.time < effect.until) continue;
                foreach (var particle in effect.particles) particle.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                effect.root.SetActive(false);
                effect.active = false;
            }
        }
    }
}
