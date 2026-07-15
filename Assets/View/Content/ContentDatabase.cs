using System;
using System.Collections.Generic;
using UnityEngine;
using Aerow.Sim;

namespace Aerow.View
{
    /// <summary>
    /// The master content list and single source of truth. Lists every item and recipe SO
    /// in the game; <see cref="Build"/> turns them into the sim's catalogues plus the
    /// view-side visual lookup. Create one asset via
    /// <c>Assets ▸ Create ▸ Aerow ▸ Content Database</c>, then either drag SOs into the lists
    /// or use the context menu <b>Collect All Content From Project</b>.
    /// </summary>
    [CreateAssetMenu(menuName = "Aerow/Content Database", fileName = "ContentDatabase")]
    public sealed class ContentDatabase : ScriptableObject
    {
        [Tooltip("Every item in the game. Use the context menu 'Collect All Content From Project' to auto-fill.")]
        public List<ItemDefSO> items = new List<ItemDefSO>();

        /// <summary>
        /// Register all listed SOs into fresh sim catalogues and build the visual lookup.
        /// Registration is sorted by StringId so runtime ids are deterministic regardless of
        /// list order or how the assets were gathered. Items register before recipes so
        /// ingredient references resolve. Throws with a clear message on the first bad asset.
        /// </summary>
        public GameContent Build()
        {
            var itemCat = new ItemCatalogue();

            // --- Items (sorted → deterministic ItemId.Index) ---
            var sortedItems = new List<ItemDefSO>(items);
            sortedItems.Sort((a, b) => string.CompareOrdinal(IdOf(a), IdOf(b)));

            var visuals = new List<ItemDefSO>(sortedItems.Count);
            foreach (ItemDefSO so in sortedItems)
            {
                if (so == null) continue;
                if (string.IsNullOrWhiteSpace(so.stringId))
                    throw new InvalidOperationException($"Item SO '{so.name}' has an empty stringId.");

                ItemId id = itemCat.Register(so.ToDefinition());
                // id.Index increments 0,1,2… in registration order, so this parallel
                // list stays index-aligned with the catalogue.
                visuals.Add(so);
            }


            return new GameContent(itemCat, visuals.ToArray());
        }

        private static string IdOf(ItemDefSO so) => so == null ? "" : so.stringId ?? "";

#if UNITY_EDITOR
        [ContextMenu("Collect All Content From Project")]
        private void CollectAllContent()
        {
            items = FindAllAssets<ItemDefSO>();
            UnityEditor.EditorUtility.SetDirty(this);
            Debug.Log($"[ContentDatabase] Collected {items.Count} item(s).");
        }

        [ContextMenu("Validate (Build In Editor)")]
        private void ValidateBuild()
        {
            try
            {
                GameContent content = Build();
                Debug.Log($"[ContentDatabase] Build OK — {content.Items.Count} item(s), ");
            }
            catch (Exception e)
            {
                Debug.LogError($"[ContentDatabase] Build FAILED: {e.Message}");
            }
        }

        private static List<T> FindAllAssets<T>() where T : UnityEngine.Object
        {
            var result = new List<T>();
            string[] guids = UnityEditor.AssetDatabase.FindAssets($"t:{typeof(T).Name}");
            foreach (string guid in guids)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                T asset = UnityEditor.AssetDatabase.LoadAssetAtPath<T>(path);
                if (asset != null) result.Add(asset);
            }
            return result;
        }
#endif
    }
}
