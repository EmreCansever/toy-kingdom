using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
#if !ENABLE_LEGACY_INPUT_MANAGER && ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using ETouch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using ETouchPhase = UnityEngine.InputSystem.TouchPhase;
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
            int fingers = 0;
            Vector2 f0 = default, f1 = default;

#if ENABLE_LEGACY_INPUT_MANAGER
            int count = Input.touchCount;
            if (count > 0)
            {
                Touch t = Input.GetTouch(0);
                down = t.phase == TouchPhase.Began;
                up = t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled;
                held = !up;
                pos = t.position;
                // sadece gerçekten basılı olan parmakları say
                for (int i = 0; i < count; i++)
                {
                    var ti = Input.GetTouch(i);
                    if (ti.phase == TouchPhase.Ended || ti.phase == TouchPhase.Canceled) continue;
                    if (fingers == 0) f0 = ti.position; else if (fingers == 1) f1 = ti.position;
                    fingers++;
                }
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
            if (!EnhancedTouchSupport.enabled) EnhancedTouchSupport.Enable();
            var active = ETouch.activeTouches;
            if (active.Count > 0)
            {
                var t = active[0];
                down = t.phase == ETouchPhase.Began;
                up = t.phase == ETouchPhase.Ended || t.phase == ETouchPhase.Canceled;
                held = !up;
                pos = t.screenPosition;
                // sadece gerçekten basılı olan, farklı kimlikli parmakları say
                int firstId = -1;
                for (int i = 0; i < active.Count; i++)
                {
                    var ti = active[i];
                    if (ti.phase == ETouchPhase.Ended || ti.phase == ETouchPhase.Canceled) continue;
                    if (fingers == 0) { f0 = ti.screenPosition; firstId = ti.touchId; fingers = 1; }
                    else if (ti.touchId != firstId) { f1 = ti.screenPosition; fingers = 2; break; }
                }
            }
            else if (Mouse.current != null)
            {
                var m = Mouse.current;
                down = m.leftButton.wasPressedThisFrame;
                held = m.leftButton.isPressed;
                up = m.leftButton.wasReleasedThisFrame;
                pos = m.position.ReadValue();
                scroll = m.scroll.ReadValue().y / 120f;
            }
#endif
            Down = down;
            Held = held;
            Up = up;
            Position = pos;
            Scroll = Mathf.Clamp(scroll, -3f, 3f);

            if (fingers >= 2 && (f1 - f0).sqrMagnitude > 1f)
            {
                float dist = Vector2.Distance(f0, f1);
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
