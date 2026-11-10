# CDP WebView – free web view for Unity

Shows a live web page, such as the **WinCC Unified** HMI runtime, inside Unity, on a UI panel or on a 3D object. You can click, scroll and type on it.

How it works: the plugin starts a hidden (headless) Chrome, Edge or Chromium, streams the page's frames as JPEG into a `Texture2D` over the Chrome DevTools Protocol, and sends your mouse and keyboard input back to it.

## Install
1. Copy the `com.HMIViewport.cdpwebview` folder into your project's `Packages/` folder.
   Unity pulls in the `Newtonsoft Json` dependency automatically.
2. Make sure Chrome, Edge or Chromium is installed on the PC that runs the app.
   On Linux: `/usr/bin/chromium` or `/usr/bin/google-chrome`.

## Use on a UI panel
1. Create a **Canvas → Raw Image**, and size it to the same aspect ratio as your HMI (e.g. 1280×800).
2. Add the **WebViewPanel** component.
3. Set **Url** to your HMI, e.g. `https://192.168.0.10/WebRH`, and set **Width/Height** to the HMI resolution.
4. Press Play. Make sure an **EventSystem** exists in the scene.

## Use on a 3D object (e.g. a screen on a control cabinet)
1. Create a **Quad**. Replace its collider with a **MeshCollider**, which is needed to get the UV of the click point.
2. Give it an **Unlit/Texture** material, so the screen isn't darkened by scene lighting.
3. Add the **WebViewPanel** component.
4. Add a **PhysicsRaycaster** to your camera, and make sure there's an EventSystem.

## Scripting
```csharp
var web = GetComponent<CdpWebView.WebViewPanel>();
web.Navigate("https://192.168.0.10/WebRH");
web.Reload();
string title = await web.ExecuteJavaScript("document.title");

// Page → Unity messages: in the page's JavaScript call  window.unity("text")
web.MessageReceived += msg => Debug.Log("From page: " + msg);
```

## Notes / limits
- **Certificate:** WinCC Unified uses a self-signed HTTPS certificate. `Ignore Certificate Errors` is on by default.
  Only use that on a trusted lab or plant network.
- **Frame updates:** a new frame arrives only when the page changes, so a static HMI costs almost nothing.
  Expect roughly 15–30 fps when values are animating.
- **No audio, no file pickers, no popups:** popups and new windows open in hidden tabs you can't see.
- **Keyboard:** typed text plus Backspace, Tab, Enter, Esc, Delete, the arrow keys, Home and End.
  Ctrl shortcuts like copy and paste aren't forwarded.
- **Standalone platforms only:** Windows, Linux and macOS. It doesn't run on Android or WebGL.
- **Testing without a PLC:** point the Url at any website first, to confirm the panel works.
