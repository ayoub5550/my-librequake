using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace LQ {
    /// <summary>Helpers to build uGUI at runtime (no prefabs/scenes needed).</summary>
    public static class UIKit {
        static Font font;
        public static Font Font {
            get {
                if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                if (font == null) font = Font.CreateDynamicFontFromOSFont(new[] { "Arial", "Roboto", "DejaVu Sans" }, 24);
                return font;
            }
        }

        static readonly System.Collections.Generic.Dictionary<string, Sprite> sprites = new System.Collections.Generic.Dictionary<string, Sprite>();

        public static Sprite HudSprite(string name) {
            if (name == null) return null;
            name = name.ToLowerInvariant();
            if (sprites.TryGetValue(name, out var s)) return s;
            var tex = Resources.Load<Texture2D>("hud/" + name);
            if (tex == null) { sprites[name] = null; return null; }
            tex.filterMode = FilterMode.Point;
            s = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
            sprites[name] = s;
            return s;
        }

        public static Canvas CreateCanvas(string name, int sortOrder = 0) {
            var go = new GameObject(name);
            var c = go.AddComponent<Canvas>(); c.renderMode = RenderMode.ScreenSpaceOverlay; c.sortingOrder = sortOrder;
            var sc = go.AddComponent<CanvasScaler>(); sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; sc.referenceResolution = new Vector2(1280, 720); sc.matchWidthOrHeight = 1f;
            go.AddComponent<GraphicRaycaster>();
            if (EventSystem.current == null) {
                var es = new GameObject("EventSystem"); es.AddComponent<EventSystem>(); es.AddComponent<StandaloneInputModule>();
                Object.DontDestroyOnLoad(es);
            }
            return c;
        }

        public static RectTransform Panel(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax, Color? color = null) {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax; rt.offsetMin = offsetMin; rt.offsetMax = offsetMax;
            if (color.HasValue) { var img = go.AddComponent<Image>(); img.color = color.Value; img.raycastTarget = false; }
            return rt;
        }

        /// <summary>Anchored rect: anchor (0..1) with pixel position/size relative to it.</summary>
        public static RectTransform Rect(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size) {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = anchor; rt.pivot = anchor; rt.anchoredPosition = pos; rt.sizeDelta = size;
            return rt;
        }

        public static Image Image(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size, Sprite sprite = null, Color? color = null) {
            var rt = Rect(parent, name, anchor, pos, size);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite; img.color = color ?? Color.white; img.raycastTarget = false; img.preserveAspect = true;
            return img;
        }

        public static Text Text(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size, string text, int fontSize, TextAnchor align = TextAnchor.MiddleCenter, Color? color = null) {
            var rt = Rect(parent, name, anchor, pos, size);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = Font; t.fontSize = fontSize; t.text = text; t.alignment = align; t.color = color ?? new Color(1f, 0.85f, 0.6f);
            t.raycastTarget = false; t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow;
            var sh = rt.gameObject.AddComponent<Shadow>(); sh.effectColor = new Color(0, 0, 0, 0.9f); sh.effectDistance = new Vector2(2, -2);
            return t;
        }

        public static Button Button(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size, string label, System.Action onClick, int fontSize = 28) {
            var rt = Rect(parent, name, anchor, pos, size);
            var img = rt.gameObject.AddComponent<Image>(); img.color = new Color(0.25f, 0.18f, 0.12f, 0.92f);
            var b = rt.gameObject.AddComponent<Button>();
            var colors = b.colors; colors.highlightedColor = new Color(0.45f, 0.3f, 0.15f); colors.pressedColor = new Color(0.7f, 0.5f, 0.2f); b.colors = colors;
            b.onClick.AddListener(() => { SoundBank.Play2D("misc/menu2.wav", 0.6f); onClick?.Invoke(); });
            var t = Text(rt, "label", new Vector2(0.5f, 0.5f), Vector2.zero, size, label, fontSize);
            t.rectTransform.anchorMin = Vector2.zero; t.rectTransform.anchorMax = Vector2.one; t.rectTransform.offsetMin = Vector2.zero; t.rectTransform.offsetMax = Vector2.zero;
            // frame
            var outline = rt.gameObject.AddComponent<Outline>(); outline.effectColor = new Color(0.8f, 0.6f, 0.3f, 0.8f); outline.effectDistance = new Vector2(2, 2);
            return b;
        }

        public static Slider Slider(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size, float min, float max, float value, System.Action<float> onChange) {
            var rt = Rect(parent, name, anchor, pos, size);
            var bg = rt.gameObject.AddComponent<Image>(); bg.color = new Color(0.1f, 0.1f, 0.1f, 0.8f);
            var s = rt.gameObject.AddComponent<Slider>();
            var fillArea = Panel(rt, "fill", Vector2.zero, Vector2.one, new Vector2(4, 4), new Vector2(-4, -4));
            var fill = Panel(fillArea, "fillimg", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0.8f, 0.55f, 0.2f));
            s.fillRect = fill;
            var handleArea = Panel(rt, "handlearea", Vector2.zero, Vector2.one, new Vector2(10, 0), new Vector2(-10, 0));
            var handle = Panel(handleArea, "handle", new Vector2(0, 0), new Vector2(0, 1), new Vector2(-12, 0), new Vector2(12, 0), new Color(1f, 0.9f, 0.7f));
            s.handleRect = handle; s.targetGraphic = handle.GetComponent<Image>();
            s.minValue = min; s.maxValue = max; s.value = value;
            s.onValueChanged.AddListener(v => onChange(v));
            return s;
        }

        public static Toggle Toggle(Transform parent, string name, Vector2 anchor, Vector2 pos, string label, bool value, System.Action<bool> onChange) {
            var rt = Rect(parent, name, anchor, pos, new Vector2(400, 44));
            var t = rt.gameObject.AddComponent<Toggle>();
            var box = Image(rt, "box", new Vector2(0, 0.5f), new Vector2(0, 0), new Vector2(36, 36), null, new Color(0.15f, 0.12f, 0.1f));
            box.raycastTarget = true;
            var check = Image(box.transform, "check", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(22, 22), null, new Color(0.9f, 0.65f, 0.25f));
            t.graphic = check; t.targetGraphic = box; t.isOn = value;
            t.onValueChanged.AddListener(v => { SoundBank.Play2D("misc/menu3.wav", 0.5f); onChange(v); });
            Text(rt, "label", new Vector2(0, 0.5f), new Vector2(50, 0), new Vector2(340, 44), label, 26, TextAnchor.MiddleLeft);
            return t;
        }
    }
}
