using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TcgEngine;

namespace TcgEngine.UI
{
    /// <summary>
    /// Grid of cards in the collection panel.
    /// Inherits GridLayoutGroup to auto-fit N columns to the grid width: the cell footprint
    /// is computed from width/columns/spacing, and each native-sized card is fitted by
    /// adjusting only its localScale (so inner content is never stretched/distorted).
    /// Cards fill left-to-right; because the cells fill the full width, the left/right
    /// margins stay equal (= padding) and a partial last row stays left-aligned.
    /// Assumes Constraint = FixedColumnCount and StartCorner = UpperLeft.
    /// </summary>

    public class CardGrid : GridLayoutGroup
    {
        [Tooltip("Auto-fit the columns to the grid width: cell footprint from width/columns, cards fitted via localScale (FixedColumnCount only).")]
        public bool auto_cell_scale = true;

        [Tooltip("Native (design) size of a card, matching the CollectionCard / inner card UI. Cards keep this size and are only scaled to fit the column width.")]
        public Vector2 design_cell_size = new Vector2(420f, 680f);

        private float card_scale = 1f;

        public override void SetLayoutHorizontal()
        {
            FitColumns();
            base.SetLayoutHorizontal();
        }

        public override void SetLayoutVertical()
        {
            base.SetLayoutVertical();
            ApplyScale();
        }

        private void FitColumns()
        {
            card_scale = 1f;

            if (!auto_cell_scale || constraint != Constraint.FixedColumnCount || constraintCount < 1 || design_cell_size.x <= 0f)
                return;

            //Split the available width evenly across the columns so they fill it edge-to-edge.
            //This keeps the left/right margins equal (= padding) and the last full row reaches the right edge.
            float available = rectTransform.rect.width - padding.horizontal - spacing.x * (constraintCount - 1);
            float cell_width = available / constraintCount;
            if (cell_width <= 0f)
                return;

            //Scale the native card to that width; the cell footprint matches the scaled card.
            card_scale = cell_width / design_cell_size.x;
            cellSize = design_cell_size * card_scale;
        }

        private void ApplyScale()
        {
            if (!auto_cell_scale || constraint != Constraint.FixedColumnCount || design_cell_size.x <= 0f)
                return;

            Vector3 scale = new Vector3(card_scale, card_scale, 1f);
            for (int i = 0; i < rectChildren.Count; i++)
            {
                //Keep the native size, fit via scale only (design_size * card_scale == cellSize footprint)
                rectChildren[i].sizeDelta = design_cell_size;
                rectChildren[i].localScale = scale;
            }
        }

        public void GetColumnAndRow(out int rows, out int columns)
        {
            rows = 0;
            columns = 0;

            if (transform.childCount == 0)
                return;

            //Get the first child GameObject of the grid
            RectTransform firstChildObj = transform.GetChild(0).GetComponent<RectTransform>();
            Vector2 firstChildPos = firstChildObj.anchoredPosition;
            bool stopCountingCol = false;

            if (firstChildPos.x == 0 && firstChildPos.y == 0)
                return;

            //Column and row are now 1
            rows = 1;
            columns = 1;

            //Loop through the rest of the child object
            for (int i = 1; i < transform.childCount; i++)
            {
                if (!transform.GetChild(i).gameObject.activeSelf)
                    continue;
                //Get the next child
                RectTransform currentChildObj = transform.GetChild(i).GetComponent<RectTransform>();
                Vector2 currentChildPos = currentChildObj.anchoredPosition;

                //check if column or row
                if (Mathf.Abs(firstChildPos.x - currentChildPos.x) < 0.1f)
                {
                    rows++;
                    stopCountingCol = true;
                }
                else
                {
                    if (!stopCountingCol)
                        columns++;
                }
            }
        }

        public GridLayoutGroup GetGrid()
        {
            return this;
        }

        public RectTransform GetRect()
        {
            return rectTransform;
        }
    }
}
