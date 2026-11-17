using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace KPL.Game
{
    /// <summary>Virtual stick: value in the unit circle, springs back on release.</summary>
    public sealed class Joystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        public RectTransform Knob;
        public float Radius = 80;
        public Vector2 Value { get; private set; }
        public bool Held { get; private set; }
        public Action<Vector2> Changed;

        public void OnPointerDown(PointerEventData e)
        {
            Held = true;
            OnDrag(e);
        }

        public void OnDrag(PointerEventData e)
        {
            var rt = (RectTransform)transform;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, e.position, e.pressEventCamera, out var local);
            var v = Vector2.ClampMagnitude(local / Radius, 1);
            Value = v.magnitude < 0.22f ? Vector2.zero : v;
            Knob.anchoredPosition = v * Radius;
            Changed?.Invoke(Value);
        }

        public void OnPointerUp(PointerEventData e)
        {
            Held = false;
            Knob.anchoredPosition = Vector2.zero;
        }

        public void ResetStick()
        {
            Value = Vector2.zero;
            if (Knob != null) Knob.anchoredPosition = Vector2.zero;
        }
    }

    /// <summary>Fires on touch-down (not on release) so batting timing is exact.</summary>
    public sealed class TouchButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        public Action Down;
        float _s = 1, _target = 1;

        public void OnPointerDown(PointerEventData e)
        {
            _target = 0.9f;
            Down?.Invoke();
        }

        public void OnPointerUp(PointerEventData e) => _target = 1;

        void Update()
        {
            _s = Mathf.Lerp(_s, _target, 1 - Mathf.Exp(-Time.unscaledDeltaTime * 25));
            transform.localScale = new Vector3(_s, _s, 1);
        }
    }

    /// <summary>Swipe surface: direction and length of a swipe, or a tap.</summary>
    public sealed class SwipePad : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        Vector2 _start;
        float _t0;
        public Action<Vector2, float> Swiped;
        public Action Tapped;

        public void OnPointerDown(PointerEventData e)
        {
            _start = e.position;
            _t0 = Time.unscaledTime;
        }

        public void OnPointerUp(PointerEventData e)
        {
            var d = e.position - _start;
            float dt = Mathf.Max(0.01f, Time.unscaledTime - _t0);
            float scale = Screen.height / 720f;
            if (d.magnitude < 40 * scale) Tapped?.Invoke();
            else Swiped?.Invoke(d / scale, d.magnitude / scale / dt);
        }
    }

    /// <summary>Full-screen tap catcher.</summary>
    public sealed class TapCatcher : MonoBehaviour, IPointerDownHandler
    {
        public Action Tapped;
        public void OnPointerDown(PointerEventData e) => Tapped?.Invoke();
    }

    public static class Pointer
    {
        public static bool OverUi()
        {
            var es = EventSystem.current;
            if (es == null) return false;
            if (es.IsPointerOverGameObject()) return true;
            for (int i = 0; i < Input.touchCount; i++)
            {
                if (es.IsPointerOverGameObject(Input.GetTouch(i).fingerId)) return true;
            }
            return false;
        }
    }
}
