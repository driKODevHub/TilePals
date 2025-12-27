using UnityEngine;

namespace TilePals.Grid
{
    /// <summary>
    /// Component to be placed inside environment prefabs to mark where the puzzle grid should be anchored.
    /// </summary>
    public class GridAnchor : MonoBehaviour
    {
        [Tooltip("The transform that serves as the (0,0,0) origin of the puzzle grid.")]
        [SerializeField] private Transform anchorPoint;

        public Transform AnchorPoint => anchorPoint != null ? anchorPoint : transform;

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.cyan;
            Vector3 pos = AnchorPoint.position;
            Gizmos.DrawWireCube(pos + new Vector3(0.5f, 0, 0.5f), new Vector3(1, 0.1f, 1));
            Gizmos.DrawRay(pos, Vector3.up * 2f);
        }
    }
}
