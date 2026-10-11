using UnityEngine;
using UnityEngine.UI;

namespace CommandQueue.UI
{
    // Lays out [queue][square button]: the button is a fixed square as tall as a one-row queue, pinned to
    // the top right, and the queue takes the rest of the width and grows downwards as it wraps.
    // A built-in HorizontalLayoutGroup can't size one child's width from a height.
    internal class QueueRowLayout : LayoutGroup
    {
        public float spacing = 5f;
        // Height of a one-row queue; also the button's size.
        public float minRowHeight = 50f;

        public override void CalculateLayoutInputHorizontal()
        {
            base.CalculateLayoutInputHorizontal();
            SetLayoutInputForAxis(minRowHeight, minRowHeight, 1, 0);
        }

        public override void CalculateLayoutInputVertical()
        {
            float height = minRowHeight;
            if (rectChildren.Count > 0)
                height = Mathf.Max(height, LayoutUtility.GetPreferredHeight(rectChildren[0]));
            SetLayoutInputForAxis(height, height, 0, 1);
        }

        public override void SetLayoutHorizontal()
        {
            float width = rectTransform.rect.width;
            if (rectChildren.Count > 0)
                SetChildAlongAxis(rectChildren[0], 0, 0, Mathf.Max(0, width - minRowHeight - spacing));
            if (rectChildren.Count > 1)
                SetChildAlongAxis(rectChildren[1], 0, width - minRowHeight, minRowHeight);
        }

        public override void SetLayoutVertical()
        {
            if (rectChildren.Count > 0)
                SetChildAlongAxis(rectChildren[0], 1, 0, rectTransform.rect.height);
            if (rectChildren.Count > 1)
                SetChildAlongAxis(rectChildren[1], 1, 0, minRowHeight);
        }
    }
}
