using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
#if !ENABLE_LEGACY_INPUT_MANAGER && ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace ToyKingdom
{
    /// <summary>
    /// Dokunma ve fareyi tek yerden okur: basma, sürükleme, bırakma, iki parmak yakınlaştırma, tekerlek.
    /// Hem eski Input Manager hem yeni Input System ile çalışır. Kare başına bir kez günceller.
    /// </summary>
    public static class PointerInput
    {
        public static bool Down { get; private set; }
        public static bool Held { get; private set; }
        public static bool Up { get; private set; }
        public static Vector2 Position { get; private set; }
        /// <summary>İki parmak arası mesafenin bu karedeki değişimi (piksel). Yakınlaştırma için.</summary>
        public static float PinchDelta { get; private set; }
        public static bool Pinching { get; private set; }
        public static float Scroll { get; private set; }
        /// <summary>Basış UI üstünde başladıysa true (bırakılana kadar).</summary>
        public static bool PressStartedOverUI { get; private set; }

        static int lastFrame = -1;
        static float lastPinchDistance;
        static readonly List<RaycastResult> hits = new List<RaycastResult>();

        public static void Refresh()
        {
            if (lastFrame == Time.frameCount) return;
            lastFrame = Time.frameCount;

            bool down = false, held = false, up = false;
            Vector2 pos = Position;
            float scroll = 0f;
            int touchCount = 0;
            Vector2 t0 = default, t1 = default;

#if ENABLE_LEGACY_INPUT_MANAGER
            touchCount = Input.touchCount;
            if (touchCount > 0)
            {
                Touch t = Input.GetTouch(0);
                down = t.phase == TouchPhase.Began;
                up = t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled;
                held = !up;
                pos = t.position;
                t0 = t.position;
                if (touchCount > 1) t1 = Input.GetTouch(1).position;
            }
            else
            {
                down = Input.GetMouseButtonDown(0);
                held = Input.GetMouseButton(0);
                up = Input.GetMouseButtonUp(0);
                pos = Input.mousePosition;
                scroll = Input.mouseScrollDelta.y;
            }
#elif ENABLE_INPUT_SYSTEM
            var ts = Touchscreen.current;
            bool usedTouch = false;
            if (ts != null)
            {
                var pressed = new List<Vector2>(2);
                foreach (var touch in ts.touches)
                    if (touch.press.isPressed) pressed.Add(touch.position.ReadValue());
                touchCount = pressed.Count;

                var primary = ts.primaryTouch;
                if (primary.press.isPressed || primary.press.wasReleasedThisFrame || primary.press.wasPressedThisFrame)
                {
                    usedTouch = true;
                    down = primary.press.wasPressedThisFrame;
                    up = primary.press.wasReleasedThisFrame;
                    held = primary.press.isPressed;
                    pos = primary.position.ReadValue();
                    if (pressed.Count > 0) t0 = pressed[0];
                    if (pressed.Count > 1) t1 = pressed[1];
                }
            }
            if (!usedTouch && Mouse.current != null)
            {
                var m = Mouse.current;
                down = m.leftButton.wasPressedThisFrame;
                held = m.leftButton.isPressed;
                up = m.leftButton.wasReleasedThisFrame;
                pos = m.position.ReadValue();
                scroll = m.scroll.ReadValue().y / 120f;
                touchCount = 0;
            }
#endif
            Down = down;
            Held = held;
            Up = up;
            Position = pos;
            Scroll = scroll;

            if (touchCount >= 2)
            {
                float dist = Vector2.Distance(t0, t1);
                PinchDelta = Pinching ? dist - lastPinchDistance : 0f;
                lastPinchDistance = dist;
                Pinching = true;
            }
            else
            {
                Pinching = false;
                PinchDelta = 0f;
            }

            if (Down) PressStartedOverUI = IsOverUI(pos);
        }

        public static bool IsOverUI(Vector2 screenPos)
        {
            var es = EventSystem.current;
            if (es == null) return false;
            var data = new PointerEventData(es) { position = screenPos };
            hits.Clear();
            es.RaycastAll(data, hits);
            return hits.Count > 0;
        }

        public static Vector3 ToWorld(Camera cam, Vector2 screen)
        {
            if (cam == null) cam = Camera.main;
            Vector3 w = cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -cam.transform.position.z));
            w.z = 0f;
            return w;
        }
    }
}
