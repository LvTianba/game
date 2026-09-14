using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BorderValley.UI.World
{
    public sealed class VirtualJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [SerializeField] private float radius = 90f;
        private RectTransform background;
        private RectTransform handle;
        private Image handleImage;

        public Vector2 Value { get; private set; }

        private void Awake()
        {
            EnsureBuilt();
        }

        private void OnDisable()
        {
            SetValue(Vector2.zero);
        }

        public void SetValue(Vector2 value)
        {
            EnsureBuilt();
            var normalized = value.sqrMagnitude > 1f ? value.normalized : value;
            normalized.x = Mathf.Clamp(normalized.x, -1f, 1f);
            normalized.y = Mathf.Clamp(normalized.y, -1f, 1f);
            Value = normalized;
            if (handle != null)
                handle.anchoredPosition = normalized * radius;
        }

        public void OnPointerDown(PointerEventData eventData) => UpdateFromPointer(eventData);

        public void OnDrag(PointerEventData eventData) => UpdateFromPointer(eventData);

        public void OnPointerUp(PointerEventData eventData) => SetValue(Vector2.zero);

        private void UpdateFromPointer(PointerEventData eventData)
        {
            EnsureBuilt();
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    background,
                    eventData.position,
                    eventData.pressEventCamera,
                    out var localPosition))
            {
                SetValue(Vector2.zero);
                return;
            }

            var normalized = localPosition / Mathf.Max(1f, radius);
            SetValue(normalized);
        }

        private void EnsureBuilt()
        {
            if (background == null)
                background = transform as RectTransform;

            if (background == null)
                return;

            var rootImage = GetComponent<Image>();
            if (rootImage == null)
                rootImage = gameObject.AddComponent<Image>();
            rootImage.color = new Color(0.12f, 0.16f, 0.22f, 0.82f);
            rootImage.raycastTarget = true;

            if (handle != null)
                return;

            var handleObject = new GameObject("Handle", typeof(RectTransform), typeof(Image));
            handleObject.transform.SetParent(transform, false);
            handle = handleObject.GetComponent<RectTransform>();
            handle.anchorMin = new Vector2(0.5f, 0.5f);
            handle.anchorMax = new Vector2(0.5f, 0.5f);
            handle.sizeDelta = new Vector2(72f, 72f);
            handleImage = handleObject.GetComponent<Image>();
            handleImage.color = new Color(0.38f, 0.62f, 0.86f, 0.95f);
            handleImage.raycastTarget = false;
        }
    }
}
