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
        /// <summary>true dönerse bu basış kamerayı kaydırmaz (ör. savaşta rota çizimi başlıyor).</summary>
        public System.Func<Vector2, bool> blockPress;
        /// <summary>İzometrik harita için: kamera merkezi bu eşkenar dörtgenin (karo alanı) içinde kalır, boş gökyüzü görünmez.</summary>
        public bool useDiamond;
        public Vector2 diamondCenter;
        public float diamondHalfW, diamondHalfH;

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

        float zoomTarget = -1f;

        /// <summary>Yakınlaştır (eksi) / uzaklaştır (artı): ekrandaki düğmeler için, yumuşak geçişle.</summary>
        public void ZoomBy(float delta)
        {
            if (Cam == null) return;
            float from = zoomTarget > 0f ? zoomTarget : Cam.orthographicSize;
            zoomTarget = Mathf.Clamp(from + delta, minSize, maxSize);
        }

        void Update()
        {
            tapReady = false;
            if (zoomTarget > 0f && Cam != null)
            {
                Cam.orthographicSize = Mathf.Lerp(Cam.orthographicSize, zoomTarget, Time.unscaledDeltaTime * 10f);
                if (Mathf.Abs(Cam.orthographicSize - zoomTarget) < 0.01f) { Cam.orthographicSize = zoomTarget; zoomTarget = -1f; }
                Clamp();
            }
            PointerInput.Refresh();
            if (!inputEnabled) { dragging = false; pressValid = false; return; }

            float zoom = 0f;
            if (PointerInput.Pinching) zoom = -PointerInput.PinchDelta * 0.01f;
            if (Mathf.Abs(PointerInput.Scroll) > 0.01f) zoom = -PointerInput.Scroll * 0.5f;
            if (zoom != 0f)
            {
                zoomTarget = -1f;
                Cam.orthographicSize = Mathf.Clamp(Cam.orthographicSize + zoom, minSize, maxSize);
                dragging = true; // yakınlaştırırken dokunuş sayma
            }

            if (PointerInput.Down)
            {
                pressValid = !PointerInput.PressStartedOverUI && (blockPress == null || !blockPress(PointerInput.Position));
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

        /// <summary>Kamerayı dünya biriminde kaydırır (sınırlar içinde).</summary>
        public void Pan(Vector3 delta)
        {
            transform.position += new Vector3(delta.x, delta.y, 0f);
            focusTarget = null;
            Clamp();
        }

        void Clamp()
        {
            if (Cam == null) return;
            float h = Cam.orthographicSize;
            float w = h * Cam.aspect;
            Vector3 p = transform.position;
            if (useDiamond && diamondHalfW > 0f && diamondHalfH > 0f)
            {
                float dx = p.x - diamondCenter.x, dy = p.y - diamondCenter.y;
                float k = Mathf.Abs(dx) / diamondHalfW + Mathf.Abs(dy) / diamondHalfH;
                if (k > 1f) { p.x = diamondCenter.x + dx / k; p.y = diamondCenter.y + dy / k; }
            }
            p.x = bounds.width > 2f * w ? Mathf.Clamp(p.x, bounds.xMin + w, bounds.xMax - w) : bounds.center.x;
            p.y = bounds.height > 2f * h ? Mathf.Clamp(p.y, bounds.yMin + h, bounds.yMax - h) : bounds.center.y;
            transform.position = p;
        }
    }
}
