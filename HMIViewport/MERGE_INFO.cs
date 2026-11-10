// HMIViewport Merge Summary
// This adapter class provides a bridge between the core CDP protocol implementations
// and the PLCSimUnity application without requiring Unity dependencies.

namespace PLCSimUnity.HMIViewport
{
    /// <summary>
    /// Provides access to the integrated HMIViewport CDP WebView components.
    /// 
    /// This namespace exposes the following core components:
    /// - CdpWebView.BrowserProcess: Launches and manages a headless Chrome/Edge browser
    /// - CdpWebView.CdpClient: Chrome DevTools Protocol client for browser communication
    /// 
    /// Note: WebViewPanel.cs (Unity UI component) is kept in the HMIViewpoint folder
    /// but excluded from compilation as it requires Unity libraries.
    /// </summary>
    public static class HMIViewportInfo
    {
        public const string MergeDate = "2024";
        public const string Source = "HMIViewpoint/com.HMIViewport.cdpwebview";
        public const string Description = "CDP WebView components for browser automation and rendering";

        /// <summary>
        /// Integrated components from HMIViewpoint:
        /// - BrowserProcess.cs (122 lines): Browser process management
        /// - CdpClient.cs (145 lines): Chrome DevTools Protocol client
        /// 
        /// Not integrated (Unity-dependent):
        /// - WebViewPanel.cs (328 lines): Requires UnityEngine libraries
        /// </summary>
        public static string GetIntegrationStatus()
        {
            return "Partially integrated - Core CDP protocol components included, " +
                   "Unity UI components excluded due to framework incompatibility.";
        }
    }
}
