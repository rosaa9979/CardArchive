using UnityEngine;

namespace TcgEngine.FX
{
    /// <summary>
    /// World FX spawned on the board follow the sorting layer of the board slot under them,
    /// so a highlighted slot or card never covers its own FX. Added by FXTool to board-position FX.
    /// </summary>
    [DefaultExecutionOrder(200)]
    public class SortingLayerFollow : MonoBehaviour
    {
        private SortingLayerBase sorting;

        void Awake()
        {
            sorting = SortingLayerBase.Get(gameObject);
        }

        //Slot layers are set during Update (BSlotIndicatorUI), so they are final here
        void LateUpdate()
        {
            sorting.Apply(SortingLayerBase.GetBoardLayerAt(transform.position));
        }
    }
}
