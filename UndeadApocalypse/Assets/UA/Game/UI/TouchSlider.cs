using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace UA.Game
{
    /// <summary>
    /// Slider for touch screens: the whole bar takes touches (tap to jump, drag to slide), not only the small handle.
    /// Inside a scrolling list, a mostly vertical swipe that starts on the slider scrolls the list instead and leaves the
    /// value where it was.
    /// </summary>
    public sealed class TouchSlider : Slider, IBeginDragHandler, IEndDragHandler
    {
        ScrollRect _scroll;
        bool _scrolling, _looked;
        float _downValue;

        ScrollRect Scroll
        {
            get
            {
                if (!_looked) { _scroll = GetComponentInParent<ScrollRect>(); _looked = true; }
                return _scroll;
            }
        }

        public override void OnPointerDown(PointerEventData e)
        {
            _downValue = value;
            _scrolling = false;
            base.OnPointerDown(e);
        }

        /// <summary>In a scrolling list keep the drag threshold, so the swipe direction is known before anything moves.</summary>
        public override void OnInitializePotentialDrag(PointerEventData e)
        {
            if (Scroll == null) base.OnInitializePotentialDrag(e);
        }

        public void OnBeginDrag(PointerEventData e)
        {
            var d = e.position - e.pressPosition;
            if (Scroll != null && Mathf.Abs(d.y) > Mathf.Abs(d.x))
            {
                _scrolling = true;
                value = _downValue;
                Scroll.OnBeginDrag(e);
            }
        }

        public override void OnDrag(PointerEventData e)
        {
            if (_scrolling) Scroll.OnDrag(e);
            else base.OnDrag(e);
        }

        public void OnEndDrag(PointerEventData e)
        {
            if (!_scrolling) return;
            _scrolling = false;
            Scroll.OnEndDrag(e);
        }
    }
}
