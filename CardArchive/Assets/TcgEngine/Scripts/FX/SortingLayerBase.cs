using System.Collections.Generic;
using TcgEngine.Client;
using UnityEngine;

namespace TcgEngine.FX
{
    /// <summary>
    /// Raises every renderer and canvas under this object to at least a base sorting layer.
    /// An element already on a higher layer keeps it, and orders are never changed, so the FX order bands
    /// (FXTool) still apply when board slots and cards are highlighted on another layer (BSlotIndicatorType).
    /// </summary>
    public class SortingLayerBase : MonoBehaviour
    {
        private readonly Dictionary<Object, int> original_layers = new Dictionary<Object, int>();
        private int base_layer_id = 0;

        public int BaseLayerID { get { return base_layer_id; } }

        public static SortingLayerBase Get(GameObject obj)
        {
            SortingLayerBase sorting = obj.GetComponent<SortingLayerBase>();
            return sorting != null ? sorting : obj.AddComponent<SortingLayerBase>();
        }

        public void Apply(string layer_name)
        {
            Apply(SortingLayer.NameToID(layer_name));
        }

        public void Apply(int layer_id)
        {
            base_layer_id = layer_id;
            int base_value = SortingLayer.GetLayerValueFromID(layer_id);

            //Children are read every time: FX attached after the first call follow too
            foreach (Renderer render in GetComponentsInChildren<Renderer>(true))
                render.sortingLayerID = Raise(render, render.sortingLayerID, base_value);
            foreach (Canvas canvas in GetComponentsInChildren<Canvas>(true))
                canvas.sortingLayerID = Raise(canvas, canvas.sortingLayerID, base_value);

            if (original_layers.Count > 128)
                PruneDestroyed();
        }

        //The layer an element had before it was first raised is its own layer
        private int Raise(Object element, int current, int base_value)
        {
            if (!original_layers.TryGetValue(element, out int original))
                original_layers[element] = original = current;
            return SortingLayer.GetLayerValueFromID(original) >= base_value ? original : base_layer_id;
        }

        private void PruneDestroyed()
        {
            List<Object> destroyed = new List<Object>();
            foreach (Object element in original_layers.Keys)
            {
                if (element == null)
                    destroyed.Add(element);
            }
            foreach (Object element in destroyed)
                original_layers.Remove(element);
        }

        //Base layer of the board slot under a world position (Default outside the board)
        public static int GetBoardLayerAt(Vector3 wpos)
        {
            foreach (BoardSlot slot in BoardSlot.GetAll())
            {
                if (slot.IsInsidePlanar(wpos))
                {
                    SortingLayerBase sorting = slot.GetComponent<SortingLayerBase>();
                    return sorting != null ? sorting.BaseLayerID : 0;
                }
            }
            return 0;
        }

        public static int GetHigherLayer(int layer_a, int layer_b)
        {
            return SortingLayer.GetLayerValueFromID(layer_a) >= SortingLayer.GetLayerValueFromID(layer_b) ? layer_a : layer_b;
        }
    }
}
