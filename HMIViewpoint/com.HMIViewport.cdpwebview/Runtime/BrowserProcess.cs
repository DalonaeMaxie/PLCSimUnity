// BrowserProcess.cs
// Starts a headless Chromium-based browser (Chrome, Edge, Chromium) with the
// DevTools port open on localhost, and finds the page's WebSocket URL.

using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace CdpWebView
{
    public sealed class BrowserProcess : IDisposable
    {
        Process _proc;
        string _profileDir;
        public int Port { get; private set; }

        public static string FindBrowser(string overridePath)
        {
            if (!string.IsNullOrEmpty(overridePath) && File.Exists(overridePath)) return overridePath;

            string[] candidates;
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
            string pf = Environment.GetEnvironmentVariable("ProgramFiles") ?? @"C:\Program Files";
            string pf86 = Environment.GetEnvironmentVariable("ProgramFiles(x86)") ?? @"C:\Program Files (x86)";
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            candidates = new[]
            {
                Path.Combine(pf, @"Google\Chrome\Application\chrome.exe"),
                Path.Combine(pf86, @"Google\Chrome\Application\chrome.exe"),
                Path.Combine(local, @"Google\Chrome\Application\chrome.exe"),
                Path.Combine(pf86, @"Microsoft\Edge\Application\msedge.exe"),
                Path.Combine(pf, @"Microsoft\Edge\Application\msedge.exe"),
            };
#elif UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
            candidates = new[]
            {
                "/Applications/Google Chrome.app/Contents/MacOS/Google Chrome",
                "/Applications/Chromium.app/Contents/MacOS/Chromium",
                "/Applications/Microsoft Edge.app/Contents/MacOS/Microsoft Edge",
            };
#else
            candidates = new[]
            {
                "/usr/bin/google-chrome", "/usr/bin/google-chrome-stable",
                "/usr/bin/chromium", "/usr/bin/chromium-browser",
                "/usr/bin/microsoft-edge", "/snap/bin/chromium",
            };
#endif
            foreach (var c in candidates) if (File.Exists(c)) return c;
            throw new FileNotFoundException("No Chrome/Edge/Chromium found. Set 'Browser Path' on the WebView component.");
        }

        /// Launch the browser and return the page target's WebSocket debugger URL.
        public async Task<string> StartAsync(string browserPath, int port, int width, int height,
                                             bool ignoreCertErrors, int timeoutMs = 15000)
        {
            Port = port;
            _profileDir = Path.Combine(Path.GetTempPath(), "cdpwebview_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_profileDir);

            string args =
                "--headless=new " +
                $"--remote-debugging-port={port} " +
                "--remote-debugging-address=127.0.0.1 " +
                $"--user-data-dir=\"{_profileDir}\" " +
                $"--window-size={width},{height} " +
                "--no-first-run --no-default-browser-check --disable-extensions " +
                "--disable-background-timer-throttling --disable-renderer-backgrounding " +
                "--disable-backgrounding-occluded-windows " +
                (ignoreCertErrors ? "--ignore-certificate-errors " : "") +
                "about:blank";

            _proc = new Process
            {
                StartInfo = new ProcessStartInfo(browserPath, args)
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                }
            };
            _proc.OutputDataReceived += (_, __) => { };
            _proc.ErrorDataReceived += (_, __) => { };
            _proc.Start();
            _proc.BeginOutputReadLine();
            _proc.BeginErrorReadLine();

            using (var http = new HttpClient { Timeout = TimeSpan.FromSeconds(2) })
            {
                var sw = Stopwatch.StartNew();
                while (sw.ElapsedMilliseconds < timeoutMs)
                {
                    if (_proc.HasExited)
                        throw new Exception($"Browser exited early (code {_proc.ExitCode}). Is port {port} already in use?");
                    try
                    {
                        var json = await http.GetStringAsync($"http://127.0.0.1:{port}/json/list");
                        foreach (var t in JArray.Parse(json))
                            if ((string)t["type"] == "page" && t["webSocketDebuggerUrl"] != null)
                                return (string)t["webSocketDebuggerUrl"];
                    }
                    catch { /* not up yet */ }
                    await Task.Delay(200);
                }
            }
            throw new TimeoutException("Browser DevTools endpoint did not come up.");
        }

        public void Dispose()
        {
            try { if (_proc != null && !_proc.HasExited) _proc.Kill(); } catch { }
            try { _proc?.WaitForExit(2000); } catch { }
            try { _proc?.Dispose(); } catch { }
            _proc = null;
            try { if (_profileDir != null && Directory.Exists(_profileDir)) Directory.Delete(_profileDir, true); } catch { }
        }
    }
}
