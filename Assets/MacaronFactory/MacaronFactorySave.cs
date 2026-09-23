using System;
using System.Collections.Generic;
using System.Linq;
using BlockShooter.SodaConveyor;
using UnityEngine;

namespace BlockShooter
{
    public sealed partial class MacaronFactory
    {
        [Serializable]
        private class SavedTray
        {
            public int slot, filled; // -1: table, -2: shipped
            public bool hidden, rewarded;
        }

        [Serializable]
        private class SavedRun
        {
            public int version = 1, stage, openSlots, remaining, combo;
            public float comboSeconds;
            public string layout;
            public SavedTray[] trays;
            public SodaConveyorTrack.SavedState conveyor;
        }

        [Serializable]
        private struct TrayStamp
        {
            public int color, capacity, layer;
            public Vector3 position, rotation, scale;
        }

        private readonly HashSet<MacaronTray> _rewardedTrays = new();
        private string _saveLayout;
        private float _lastAutosave;
        private bool _saveDirty;
        private string RunSaveKey => "Macaron.Run." + gameObject.scene.path + "." + stageOverride;
        private string StageSaveKey => stageOverride == 0 ? "Macaron.Stage" : RunSaveKey + ".Stage";

        private void InitializeRunSave()
        {
            // Compare authored tray poses and initial supply, never transient Unity instance IDs.
            _saveLayout = SourceStage + ":" + (int)PuzzleStyle + ":" +
                string.Join("|", _trays.Select(t => JsonUtility.ToJson(new TrayStamp {
                    color = (int)t.Color, capacity = t.Capacity, layer = t.Layer,
                    position = t.transform.localPosition, rotation = t.transform.localEulerAngles,
                    scale = t.transform.localScale }))) + JsonUtility.ToJson(Conveyor.CaptureState());
            if (!PlayerPrefs.HasKey(RunSaveKey)) return;
            SavedRun saved;
            try
            {
                saved = JsonUtility.FromJson<SavedRun>(PlayerPrefs.GetString(RunSaveKey));
                if (!IsValidRun(saved)) throw new InvalidOperationException("Save version or level data no longer matches.");
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Cannot resume Macaron save: " + exception.Message);
                PlayerPrefs.DeleteKey(RunSaveKey);
                PlayerPrefs.Save();
                return;
            }
            RestoreRun(saved);
        }

        private SavedRun CaptureRun()
        {
            return new SavedRun {
                stage = Stage, layout = _saveLayout, openSlots = OpenSlots, remaining = _remaining,
                combo = _comboCount, comboSeconds = Mathf.Max(0, _comboUntil - Time.time),
                conveyor = Conveyor.CaptureState(),
                trays = _trays.Select(t => new SavedTray {
                    slot = t.OnTable ? -1 : !t.gameObject.activeSelf ? -2 : Array.IndexOf(_slots, t),
                    // A cake is committed when detached from the conveyor, even during its flight.
                    filled = Mathf.Max(t.Filled, t.pockets.Count(p => p.Find("Macaron") != null)),
                    hidden = t.Hidden, rewarded = _rewardedTrays.Contains(t)
                }).ToArray()
            };
        }

        private bool IsValidRun(SavedRun saved)
        {
            if (saved == null || saved.version != 1 || saved.stage != Stage || saved.layout != _saveLayout ||
                saved.openSlots < 4 || saved.openSlots > 6 || saved.remaining < 0 || saved.combo < 0 ||
                !float.IsFinite(saved.comboSeconds) || saved.comboSeconds < 0 ||
                saved.trays == null || saved.trays.Length != _trays.Count || saved.conveyor == null ||
                saved.conveyor.slots == null || saved.conveyor.branches == null ||
                saved.conveyor.slots.Length != Conveyor.CaptureState().slots.Length ||
                saved.conveyor.branches.Length != Conveyor.Branches.Count) return false;
            var colors = _trays.Select(t => t.Color).Distinct().ToDictionary(c => c, _ => 0);
            var usedSlots = new HashSet<int>();
            int consumed = 0;
            for (int i = 0; i < saved.trays.Length; i++)
            {
                var t = saved.trays[i];
                if (t == null || t.slot < -2 || t.slot >= saved.openSlots || t.filled < 0 || t.filled > _trays[i].Capacity ||
                    (t.slot >= 0 && !usedSlots.Add(t.slot)) || (t.slot == -1 && t.filled != 0) ||
                    (t.slot == -2 && (t.filled != _trays[i].Capacity || !t.rewarded)) ||
                    (t.rewarded && t.filled != _trays[i].Capacity)) return false;
                consumed += t.filled;
                colors[_trays[i].Color] += _trays[i].Capacity - t.filled;
            }
            int supply = 0;
            for (int i = 0; i < saved.conveyor.slots.Length; i++)
            {
                var slot = saved.conveyor.slots[i];
                if (!float.IsFinite(slot.t) || slot.t < 0 || slot.t >= 1 || slot.lanes < 0 || slot.lanes > 15) return false;
                float expectedT = saved.conveyor.slots[0].t + (float)i / saved.conveyor.slots.Length;
                if (Mathf.Abs(Mathf.DeltaAngle(expectedT * 360, slot.t * 360)) > .01f) return false;
                for (int lane = 0; lane < 4; lane++)
                {
                    if ((slot.lanes & (1 << lane)) == 0) continue;
                    if (!colors.ContainsKey(slot.color)) return false;
                    colors[slot.color]--;
                    supply++;
                }
            }
            foreach (var branch in saved.conveyor.branches)
            {
                if (branch?.rows == null || branch.rows.Length > _remaining / 4) return false;
                float previous = 1;
                foreach (var row in branch.rows)
                {
                    if (!float.IsFinite(row.t) || row.t > previous || !colors.ContainsKey(row.color)) return false;
                    previous = row.t;
                    colors[row.color] -= 4;
                    supply += 4;
                }
            }
            return supply == saved.remaining && consumed + supply == _trays.Sum(t => t.Capacity) && colors.Values.All(n => n == 0);
        }

        private void RestoreRun(SavedRun saved)
        {
            Conveyor.RestoreState(saved.conveyor);
            OpenSlots = saved.openSlots;
            _remaining = saved.remaining;
            _comboCount = saved.combo;
            _comboUntil = Time.time + Mathf.Min(comboWindow, saved.comboSeconds);
            if (_comboText != null && saved.comboSeconds > 0 && saved.combo > 0)
                _comboText.text = saved.combo < 2 ? "PACKED!" : $"COMBO x{saved.combo}";
            for (int i = 0; i < saved.trays.Length; i++)
            {
                var state = saved.trays[i];
                var tray = _trays[i];
                if (state.rewarded) _rewardedTrays.Add(tray);
                if (state.slot != -1)
                {
                    tray.LeaveTable();
                    tray.ReleaseTableBlockIfClear(true);
                    if (state.slot == -2) { _shipped++; tray.gameObject.SetActive(false); }
                    else
                    {
                        _slots[state.slot] = tray;
                        tray.transform.SetPositionAndRotation(SlotPosition(state.slot), _layout.waitingSlots[state.slot].rotation);
                        tray.transform.localScale = Vector3.one * Mathf.Max(.01f, trayWaitingScale);
                        for (int pocket = 0; pocket < state.filled; pocket++)
                        {
                            var visual = Instantiate(MacaronPrefab(tray.Color), tray.GetPocket(pocket)).transform;
                            visual.name = "Macaron";
                            visual.localPosition = Vector3.zero;
                            visual.localRotation = Quaternion.identity;
                            visual.localScale = Vector3.one;
                            ApplyMacaronColor(visual.GetComponent<Renderer>(), tray.Color);
                            MacaronLevelVisualPolish.MarkForOutline(visual);
                            foreach (var collider in visual.GetComponentsInChildren<Collider>()) collider.enabled = false;
                        }
                    }
                }
                tray.RestoreProgress(state.filled, state.hidden);
            }
        }

        private void ResumePacking()
        {
            foreach (var tray in _slots)
                if (tray != null && tray.Filled == tray.Capacity) StartCoroutine(Ship(tray));
            CheckCompletion();
        }

        private void SaveRun()
        {
            if (!_ready || _saveLayout == null || GameManager.Instance == null ||
                (GameManager.Instance.State != GameState.Playing && GameManager.Instance.State != GameState.Paused)) return;
            try
            {
                // Reuse the game's native PlayerPrefs storage; coins and this checkpoint flush together.
                PlayerPrefs.SetString(RunSaveKey, JsonUtility.ToJson(CaptureRun()));
                PlayerPrefs.Save();
                _saveDirty = false;
                _lastAutosave = Time.realtimeSinceStartup;
            }
            catch (Exception exception) { Debug.LogWarning("Cannot save Macaron progress: " + exception.Message); }
        }

        private void DiscardRun()
        {
            PlayerPrefs.DeleteKey(RunSaveKey);
            PlayerPrefs.Save();
        }

        private void OnApplicationPause(bool paused) { if (paused) SaveRun(); }
        private void OnApplicationFocus(bool focused) { if (!focused) SaveRun(); }
        private void OnApplicationQuit() => SaveRun();

#if UNITY_EDITOR
        [UnityEditor.MenuItem("Macaron Factory/Checks/Save progress (Play Mode)")]
        public static void CheckRunSave()
        {
            var factory = FindFirstObjectByType<MacaronFactory>();
            if (!Application.isPlaying || factory == null || !factory._ready)
                throw new InvalidOperationException("Run this check while playing Macaron Factory.");
            var saved = factory.CaptureRun();
            var json = JsonUtility.ToJson(saved);
            if (!factory.IsValidRun(JsonUtility.FromJson<SavedRun>(json)))
                throw new Exception("Checkpoint lost cakes, tray state or conveyor rows.");
            saved.remaining++;
            if (factory.IsValidRun(saved)) throw new Exception("Accepted incorrect remaining cake count.");
            saved = JsonUtility.FromJson<SavedRun>(json);
            saved.trays[0].filled = factory._trays[0].Capacity + 1;
            if (factory.IsValidRun(saved)) throw new Exception("Accepted overflowing tray.");
            saved = JsonUtility.FromJson<SavedRun>(json);
            saved.trays[0].slot = saved.trays[1].slot = 0;
            if (factory.IsValidRun(saved)) throw new Exception("Accepted duplicate waiting slots.");
            saved = JsonUtility.FromJson<SavedRun>(json);
            saved.version++;
            if (factory.IsValidRun(saved)) throw new Exception("Accepted incompatible save version.");
            Debug.Log("PASS: Save JSON round trip, cake conservation by color, tray bounds, duplicate slots and version validation.");
        }
#endif
    }
}
