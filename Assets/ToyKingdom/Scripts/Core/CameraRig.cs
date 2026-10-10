using UnityEngine;

namespace ToyKingdom
{
    /// <summary>
    /// Harita kamerası: parmakla sürükleyerek kaydırma, iki parmakla / tekerlekle yakınlaştırma.
    /// Kısa dokunuşları "tap" olarak bildirir.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class CameraRig : MonoBehaviour
    {
        public Rect bounds = new Rect(-10f, -2f, 20f, 16f);
        public float minSize = 2.5f;
        public float maxSize = 7f;
        [Tooltip("Bu kadar pikselden az kayan dokunuş 'tap' sayılır")]
        public float tapThreshold = 18f;
        public bool inputEnabled = true;

        public Camera Cam { get; private set; }

        Vector2 pressPos;
        Vector2 lastPos;
        bool dragging;
        bool pressValid;
        bool tapReady;
        Vector3 tapWorld;
        Vector3 velocity;
        Vector3? focusTarget;

        void Awake()
        {
            Cam = GetComponent<Camera>();
            Clamp();
        }

        /// <summary>Bu karede bir dokunuş olduysa dünyadaki noktasını verir.</summary>
        public bool TryConsumeTap(out Vector3 world)
        {
            world = tapWorld;
            if (!tapReady) return false;
            tapReady = false;
            return true;
        }

        public void FocusOn(Vector3 world, bool instant = false)
        {
            if (instant)
            {
                transform.position = new Vector3(world.x, world.y, transform.position.z);
                Clamp();
                focusTarget = null;
            }
            else focusTarget = world;
        }

        void Update()
        {
            tapReady = false;
            PointerInput.Refresh();
            if (!inputEnabled) { dragging = false; pressValid = false; return; }

            float zoom = 0f;
            if (PointerInput.Pinching) zoom = -PointerInput.PinchDelta * 0.01f;
            if (Mathf.Abs(PointerInput.Scroll) > 0.01f) zoom = -PointerInput.Scroll * 0.5f;
            if (zoom != 0f)
            {
                Cam.orthographicSize = Mathf.Clamp(Cam.orthographicSize + zoom, minSize, maxSize);
                dragging = true; // yakınlaştırırken dokunuş sayma
            }

            if (PointerInput.Down)
            {
                pressValid = !PointerInput.PressStartedOverUI;
                pressPos = lastPos = PointerInput.Position;
                dragging = false;
                velocity = Vector3.zero;
            }

            if (pressValid && PointerInput.Held && !PointerInput.Pinching)
            {
                Vector2 pos = PointerInput.Position;
                if (!dragging && (pos - pressPos).magnitude > tapThreshold) dragging = true;
                if (dragging)
                {
                    Vector3 a = PointerInput.ToWorld(Cam, lastPos);
                    Vector3 b = PointerInput.ToWorld(Cam, pos);
                    Vector3 delta = a - b;
                    transform.position += delta;
                    velocity = delta / Mathf.Max(Time.deltaTime, 0.001f);
                    focusTarget = null;
                }
                lastPos = pos;
            }
            else if (!PointerInput.Held)
            {
                transform.position += velocity * Time.deltaTime;
                velocity = Vector3.Lerp(velocity, Vector3.zero, Time.deltaTime * 6f);
            }

            if (PointerInput.Up && pressValid)
            {
                if (!dragging)
                {
                    tapReady = true;
                    tapWorld = PointerInput.ToWorld(Cam, PointerInput.Position);
                }
                pressValid = false;
                dragging = false;
            }

            if (focusTarget.HasValue)
            {
                Vector3 t = new Vector3(focusTarget.Value.x, focusTarget.Value.y, transform.position.z);
                transform.position = Vector3.Lerp(transform.position, t, Time.deltaTime * 4f);
                if ((transform.position - t).sqrMagnitude < 0.0004f) focusTarget = null;
            }

            Clamp();
        }

        void Clamp()
        {
            if (Cam == null) return;
            float h = Cam.orthographicSize;
            float w = h * Cam.aspect;
            Vector3 p = transform.position;
            p.x = bounds.width > 2f * w ? Mathf.Clamp(p.x, bounds.xMin + w, bounds.xMax - w) : bounds.center.x;
            p.y = bounds.height > 2f * h ? Mathf.Clamp(p.y, bounds.yMin + h, bounds.yMax - h) : bounds.center.y;
            transform.position = p;
        }
    }
}
