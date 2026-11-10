// WebViewPanel.cs
// Free web view for Unity. Put it on:
//   * a UI RawImage (Canvas, screen or world space), or
//   * a 3D object with a MeshRenderer + MeshCollider (e.g. a Quad on a control cabinet).
// It renders a web page (e.g. the WinCC Unified runtime) into a texture and
// forwards mouse, scroll and keyboard input back to the page.
//
// Requirements: an EventSystem in the scene; for 3D objects, a PhysicsRaycaster
// on the camera. Chrome, Edge or Chromium installed on the machine.

using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace CdpWebView
{
    [DisallowMultipleComponent]
    public class WebViewPanel : MonoBehaviour,
        IPointerDownHandler, IPointerUpHandler, IPointerMoveHandler, IDragHandler,
        IScrollHandler, ISelectHandler, IDeselectHandler
    {
        [Header("Page")]
        [Tooltip("e.g. https://192.168.0.10/WebRH for WinCC Unified")]
        public string url = "https://www.example.com";
        [Tooltip("Browser viewport in CSS pixels. Match your HMI screen resolution.")]
        public int width = 1280;
        public int height = 800;

        [Header("Browser")]
        [Tooltip("Leave empty to auto-detect Chrome / Edge / Chromium.")]
        public string browserPath = "";
        [Tooltip("0 = pick a free port automatically.")]
        public int debugPort = 0;
        [Tooltip("WinCC Unified uses a self-signed certificate. Only enable on a trusted plant/lab network.")]
        public bool ignoreCertificateErrors = true;
        [Range(10, 100)] public int jpegQuality = 80;

        [Header("Input")]
        [Tooltip("Multiplier from Unity scroll delta to browser pixels. Lower it if scrolling is too fast.")]
        public float scrollMultiplier = 100f;

        /// Raised when the page calls window.unity("some string").
        public event Action<string> MessageReceived;
        /// Raised once the browser is up and the first page load was requested.
        public event Action Ready;

        public Texture2D Texture => _tex;
        public bool IsReady { get; private set; }

        BrowserProcess _browser;
        CdpClient _cdp;
        Texture2D _tex;
        RawImage _rawImage;
        Renderer _renderer;
        Collider _collider;
        bool _focused;
        Vector2 _lastPos = new Vector2(-1, -1);
        int _buttonsDown;

        // ---------- lifecycle ----------

        void Awake()
        {
            _rawImage = GetComponent<RawImage>();
            _renderer = GetComponent<Renderer>();
            _collider = GetComponent<Collider>();

            _tex = new Texture2D(2, 2, TextureFormat.RGB24, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = "WebViewTexture"
            };
            if (_rawImage) _rawImage.texture = _tex;
            else if (_renderer) _renderer.material.mainTexture = _tex;
            else Debug.LogWarning("[WebView] No RawImage or Renderer found; use the Texture property yourself.", this);
        }

        async void Start()
        {
            try { await StartBrowser(); }
            catch (Exception e) { Debug.LogError("[WebView] Start failed: " + e.Message, this); }
        }

        async Task StartBrowser()
        {
            int port = debugPort > 0 ? debugPort : FreePort();
            string exe = BrowserProcess.FindBrowser(browserPath);

            _browser = new BrowserProcess();
            string wsUrl = await _browser.StartAsync(exe, port, width, height, ignoreCertificateErrors);

            _cdp = new CdpClient();
            await _cdp.ConnectAsync(wsUrl);

            await _cdp.Send("Page.enable");
            await _cdp.Send("Runtime.enable");
            await _cdp.Send("Runtime.addBinding", new { name = "unity" });
            await _cdp.Send("Emulation.setDeviceMetricsOverride",
                new { width, height, deviceScaleFactor = 1, mobile = false });
            if (ignoreCertificateErrors)
                await _cdp.Send("Security.setIgnoreCertificateErrors", new { ignore = true });
            await _cdp.Send("Page.startScreencast",
                new { format = "jpeg", quality = jpegQuality, maxWidth = width, maxHeight = height, everyNthFrame = 1 });

            IsReady = true;
            Navigate(url);
            Ready?.Invoke();
        }

        void Update()
        {
            if (_cdp == null) return;
            _cdp.DrainEvents(HandleEvent);
            if (_focused && IsReady) PollKeyboard();
        }

        void OnDestroy() => Shutdown();
        void OnApplicationQuit() => Shutdown();

        void Shutdown()
        {
            IsReady = false;
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null) Keyboard.current.onTextInput -= OnTextInput;
#endif
            _cdp?.Dispose(); _cdp = null;
            _browser?.Dispose(); _browser = null;
        }

        // ---------- public API ----------

        public void Navigate(string newUrl)
        {
            url = newUrl;
            if (IsReady) _cdp.Post("Page.navigate", new { url = newUrl });
        }

        public void Reload() { if (IsReady) _cdp.Post("Page.reload"); }

        /// Run JavaScript in the page and get the result back as JSON-ish text.
        public async Task<string> ExecuteJavaScript(string js)
        {
            if (!IsReady) return null;
            var r = await _cdp.Send("Runtime.evaluate", new { expression = js, returnByValue = true, awaitPromise = true });
            return r["result"]?["value"]?.ToString();
        }

        // ---------- CDP events ----------

        void HandleEvent(CdpClient.CdpEvent ev)
        {
            switch (ev.Method)
            {
                case "Page.screencastFrame":
                    var bytes = Convert.FromBase64String((string)ev.Params["data"]);
                    _tex.LoadImage(bytes, false);
                    _cdp.Post("Page.screencastFrameAck", new { sessionId = (int)ev.Params["sessionId"] });
                    break;

                case "Runtime.bindingCalled":
                    if ((string)ev.Params["name"] == "unity")
                        MessageReceived?.Invoke((string)ev.Params["payload"]);
                    break;

                case "__closed":
                    Debug.LogWarning("[WebView] Connection closed: " + ev.Params["reason"], this);
                    IsReady = false;
                    break;
            }
        }

        // ---------- pointer input ----------

        public void OnPointerDown(PointerEventData e)
        {
            if (EventSystem.current) EventSystem.current.SetSelectedGameObject(gameObject);
            if (!TryGetPagePoint(e, out var p)) return;
            _buttonsDown |= ButtonMask(e.button);
            SendMouse("mousePressed", p, e.button, 1);
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (!TryGetPagePoint(e, out var p)) p = _lastPos;
            _buttonsDown &= ~ButtonMask(e.button);
            SendMouse("mouseReleased", p, e.button, 1);
        }

        public void OnPointerMove(PointerEventData e) => Move(e);
        public void OnDrag(PointerEventData e) => Move(e);

        void Move(PointerEventData e)
        {
            if (!TryGetPagePoint(e, out var p) || p == _lastPos) return;
            _lastPos = p;
            if (!IsReady) return;
            _cdp.Post("Input.dispatchMouseEvent", new
            {
                type = "mouseMoved", x = p.x, y = p.y,
                button = _buttonsDown != 0 ? "left" : "none", buttons = _buttonsDown
            });
        }

        public void OnScroll(PointerEventData e)
        {
            if (!IsReady || !TryGetPagePoint(e, out var p)) return;
            _cdp.Post("Input.dispatchMouseEvent", new
            {
                type = "mouseWheel", x = p.x, y = p.y,
                deltaX = e.scrollDelta.x * scrollMultiplier,
                deltaY = -e.scrollDelta.y * scrollMultiplier
            });
        }

        void SendMouse(string type, Vector2 p, PointerEventData.InputButton b, int clicks)
        {
            if (!IsReady) return;
            _lastPos = p;
            _cdp.Post("Input.dispatchMouseEvent", new
            {
                type, x = p.x, y = p.y, button = ButtonName(b), buttons = _buttonsDown, clickCount = clicks
            });
        }

        static string ButtonName(PointerEventData.InputButton b) =>
            b == PointerEventData.InputButton.Right ? "right" :
            b == PointerEventData.InputButton.Middle ? "middle" : "left";

        static int ButtonMask(PointerEventData.InputButton b) =>
            b == PointerEventData.InputButton.Right ? 2 :
            b == PointerEventData.InputButton.Middle ? 4 : 1;

        /// Convert a pointer position to browser CSS pixels (origin top-left).
        bool TryGetPagePoint(PointerEventData e, out Vector2 page)
        {
            page = default;
            Vector2 uv;
            Camera cam = e.pressEventCamera ? e.pressEventCamera : e.enterEventCamera;

            if (transform is RectTransform rt)
            {
                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, e.position, cam, out var local))
                    return false;
                var r = rt.rect;
                uv = new Vector2((local.x - r.xMin) / r.width, (local.y - r.yMin) / r.height);
                if (_rawImage)
                {
                    var u = _rawImage.uvRect;
                    uv = new Vector2(u.x + uv.x * u.width, u.y + uv.y * u.height);
                }
            }
            else
            {
                if (!cam) cam = Camera.main;
                if (!cam || !_collider) return false;
                if (!_collider.Raycast(cam.ScreenPointToRay(e.position), out var hit, 10000f)) return false;
                uv = hit.textureCoord; // needs a MeshCollider to be meaningful
            }

            if (uv.x < 0 || uv.x > 1 || uv.y < 0 || uv.y > 1) return false;
            page = new Vector2(Mathf.Round(uv.x * width), Mathf.Round((1f - uv.y) * height));
            return true;
        }

        // ---------- keyboard ----------

        public void OnSelect(BaseEventData e)
        {
            _focused = true;
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                Keyboard.current.onTextInput -= OnTextInput;
                Keyboard.current.onTextInput += OnTextInput;
            }
#endif
        }

        public void OnDeselect(BaseEventData e)
        {
            _focused = false;
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null) Keyboard.current.onTextInput -= OnTextInput;
#endif
        }

        // (DOM key name, code, Windows virtual key code)
        static readonly (string key, string code, int vk)[] SpecialKeys =
        {
            ("Backspace","Backspace",8), ("Tab","Tab",9), ("Enter","Enter",13), ("Escape","Escape",27),
            ("Delete","Delete",46), ("ArrowLeft","ArrowLeft",37), ("ArrowUp","ArrowUp",38),
            ("ArrowRight","ArrowRight",39), ("ArrowDown","ArrowDown",40), ("Home","Home",36), ("End","End",35),
        };

        void SendKey(int index)
        {
            var k = SpecialKeys[index];
            bool enter = k.vk == 13;
            _cdp.Post("Input.dispatchKeyEvent", new
            {
                type = enter ? "keyDown" : "rawKeyDown",
                key = k.key, code = k.code, windowsVirtualKeyCode = k.vk, nativeVirtualKeyCode = k.vk,
                text = enter ? "\r" : null, unmodifiedText = enter ? "\r" : null
            });
            _cdp.Post("Input.dispatchKeyEvent", new
            {
                type = "keyUp", key = k.key, code = k.code, windowsVirtualKeyCode = k.vk, nativeVirtualKeyCode = k.vk
            });
        }

        void InsertText(string s)
        {
            if (!string.IsNullOrEmpty(s)) _cdp.Post("Input.insertText", new { text = s });
        }

#if ENABLE_INPUT_SYSTEM
        static readonly Key[] KeyMap =
        {
            Key.Backspace, Key.Tab, Key.Enter, Key.Escape, Key.Delete, Key.LeftArrow,
            Key.UpArrow, Key.RightArrow, Key.DownArrow, Key.Home, Key.End
        };

        void OnTextInput(char c)
        {
            if (!_focused || !IsReady || char.IsControl(c)) return;
            InsertText(c.ToString());
        }

        void PollKeyboard()
        {
            var kb = Keyboard.current;
            if (kb == null) return;
            for (int i = 0; i < KeyMap.Length; i++)
                if (kb[KeyMap[i]].wasPressedThisFrame) SendKey(i);
            if (kb.numpadEnterKey.wasPressedThisFrame) SendKey(2);
        }
#else
        void PollKeyboard()
        {
            // Legacy Input Manager: Input.inputString holds typed chars, incl. '\b' and '\n'.
            var text = new System.Text.StringBuilder();
            foreach (char c in Input.inputString)
            {
                if (c == '\b') { InsertText(text.ToString()); text.Clear(); SendKey(0); }
                else if (c == '\n' || c == '\r') { InsertText(text.ToString()); text.Clear(); SendKey(2); }
                else if (!char.IsControl(c)) text.Append(c);
            }
            InsertText(text.ToString());

            KeyCode[] map =
            {
                KeyCode.None, KeyCode.Tab, KeyCode.None, KeyCode.Escape, KeyCode.Delete, KeyCode.LeftArrow,
                KeyCode.UpArrow, KeyCode.RightArrow, KeyCode.DownArrow, KeyCode.Home, KeyCode.End
            };
            for (int i = 0; i < map.Length; i++)
                if (map[i] != KeyCode.None && Input.GetKeyDown(map[i])) SendKey(i);
        }
#endif

        static int FreePort()
        {
            var l = new TcpListener(IPAddress.Loopback, 0);
            l.Start();
            int port = ((IPEndPoint)l.LocalEndpoint).Port;
            l.Stop();
            return port;
        }
    }
}
