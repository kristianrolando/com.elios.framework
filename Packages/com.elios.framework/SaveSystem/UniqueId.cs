using UnityEngine;

namespace Game.Framework.SaveSystem
{
    /// <summary>
    /// Simple persistent id holder for object-based save keys.
    /// Example usage:
    /// Save.Set(SaveKeyBuilder.ObjectState(uniqueId.Value), myState);
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UniqueId : MonoBehaviour
    {
        [SerializeField] private string value;

        public string Value => value;

        private void Awake()
        {
            EnsureValue();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!Application.isPlaying)
                EnsureValue();
        }

        [ContextMenu("Regenerate Unique ID")]
        private void Regenerate()
        {
            value = System.Guid.NewGuid().ToString("N");
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif

        private void EnsureValue()
        {
            if (!string.IsNullOrWhiteSpace(value))
                return;

            value = System.Guid.NewGuid().ToString("N");
        }
    }
}