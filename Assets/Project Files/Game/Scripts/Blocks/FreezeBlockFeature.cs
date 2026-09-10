using UnityEngine;
using TMPro;

namespace BlockShooter
{
    /// <summary>
    /// A modular feature component for ShooterBlock. Handles toggling between the
    /// freeze visual ("Freeze") and standard visual ("Base") on the block prefab,
    /// and tracking the count required to unfreeze the block.
    /// </summary>
    [RequireComponent(typeof(ShooterBlock))]
    public class FreezeBlockFeature : MonoBehaviour
    {
        [Header("Modular Visual Groups")]
        [Tooltip("The normal block visual group GameObject (Base)")]
        public GameObject baseVisual;

        [Tooltip("The freeze visual group GameObject (Freeze)")]
        public GameObject freezeVisual;

        [Tooltip("Text component displaying the remaining freeze count")]
        public TextMeshPro freezeCountText;

        [Tooltip("Optional particle system to play when unfrozen (revealed)")]
        public ParticleSystem revealParticle;

        [Header("State")]
        public bool isFrozen = true;
        public int freezeCount = 3;

        [Header("Freeze Animator")]
        [Tooltip("Animator for the freeze visual (e.g. Freeze object)")]
        public Animator animator;

        private ShooterBlock _block;

        public void PlayShake()
        {
            if (animator != null)
            {
                animator.SetTrigger("ShooterShake");
            }
        }

        private void Awake()
        {
            // Handle duplicate components at runtime just in case legacy levels have them
            var features = GetComponents<FreezeBlockFeature>();
            if (features.Length > 1 && features[0] != this)
            {
                var primary = features[0];
                if (isFrozen)
                {
                    primary.isFrozen = true;
                    primary.freezeCount = freezeCount;
                }
                Destroy(this);
                return;
            }

            _block = GetComponent<ShooterBlock>();

            // Auto-find children by name if not assigned in the inspector
            if (baseVisual == null)
            {
                var t = transform.Find("Base");
                if (t != null) baseVisual = t.gameObject;
            }
            if (freezeVisual == null)
            {
                var t = transform.Find("Freeze");
                if (t != null) freezeVisual = t.gameObject;
            }
            if (freezeCountText == null && freezeVisual != null)
            {
                freezeCountText = freezeVisual.GetComponentInChildren<TextMeshPro>(true);
            }

            // Sync visual states on start based on freeze state
            SyncVisualStates();
        }

        private void Start()
        {
            if (_block == null) return; // duplicate component safety exit
            // Make sure the text is updated at the beginning of the level
            UpdateTextUI();
        }

        public void SyncVisualStates()
        {
            if (freezeVisual != null) freezeVisual.SetActive(isFrozen);
            
            bool isMystery = _block != null && _block.isMystery;
            bool shouldShowBase = !isFrozen && !isMystery;
            if (baseVisual != null) baseVisual.SetActive(shouldShowBase);
            
            UpdateTextUI();
        }

        public void SyncVisualsEditor()
        {
            // Auto-find children by name for editor view setup
            if (baseVisual == null)
            {
                var t = transform.Find("Base");
                if (t != null) baseVisual = t.gameObject;
            }
            if (freezeVisual == null)
            {
                var t = transform.Find("Freeze");
                if (t != null) freezeVisual = t.gameObject;
            }
            if (freezeCountText == null && freezeVisual != null)
            {
                freezeCountText = freezeVisual.GetComponentInChildren<TextMeshPro>(true);
            }

            if (freezeVisual != null) freezeVisual.SetActive(isFrozen);
            
            bool isMystery = _block != null && _block.isMystery;
            bool shouldShowBase = !isFrozen && !isMystery;
            if (baseVisual != null) baseVisual.SetActive(shouldShowBase);
            
            UpdateTextUI();
        }

        public void DecrementCount()
        {
            if (!isFrozen) return;

            freezeCount--;
            UpdateTextUI();

            if (freezeCount <= 0)
            {
                Unfreeze();
            }
        }

        private void Unfreeze()
        {
            isFrozen = false;

            if (freezeVisual != null) freezeVisual.SetActive(false);
            
            bool isMystery = _block != null && _block.isMystery;
            bool shouldShowBase = !isMystery;
            if (baseVisual != null) baseVisual.SetActive(shouldShowBase);

            if (_block != null)
            {
                _block.RevealFromFeature();
            }

            if (revealParticle != null)
            {
                ParticleSystem psToPlay = revealParticle;
                
                // Check if it's a project asset prefab (not currently in the scene hierarchy)
                bool isPrefabAsset = !revealParticle.gameObject.scene.IsValid();
                
                if (isPrefabAsset)
                {
                    // Instantiate particle at runtime to play it safely
                    Vector3 spawnPos = transform.position + new Vector3(0f, 0.4f, 0f);
                    psToPlay = Instantiate(revealParticle, spawnPos, Quaternion.identity);
                    Destroy(psToPlay.gameObject, psToPlay.main.duration + psToPlay.main.startLifetime.constantMax);
                }
                else
                {
                    // If it is child of Freeze, unparent it to root so it isn't deactivated when Freeze turns off
                    if (freezeVisual != null && psToPlay.transform.IsChildOf(freezeVisual.transform))
                    {
                        psToPlay.transform.SetParent(transform, true);
                    }
                    
                    psToPlay.gameObject.SetActive(true);
                    Vector3 localPos = psToPlay.transform.localPosition;
                    localPos.y = 0.4f;
                    psToPlay.transform.localPosition = localPos;
                }
                
                psToPlay.Play();
            }

            // Refresh accessibility in grid so this block (and blocks behind it) can be pathfound
            if (ShooterGrid.Instance != null)
            {
                ShooterGrid.Instance.RefreshAllAccessibility();
            }
        }

        private void UpdateTextUI()
        {
            if (freezeCountText != null)
            {
                freezeCountText.text = freezeCount.ToString();
            }
        }
    }
}
