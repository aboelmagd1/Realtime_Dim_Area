using System.Diagnostics;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using DimensionOverlay.Core;
using DimensionOverlay.Models;

namespace DimensionOverlay
{
    /// <summary>
    /// Module entry point. Owns the DimensionSettings singleton and manages
    /// the lifetimes of EditingMonitor and DimensionEngine.
    ///
    /// autoLoad="true" in Config.daml ensures this module starts as soon as ArcGIS Pro loads,
    /// enabling passive dimension observation for any editing or selection workflow.
    /// </summary>
    internal class Module1 : Module
    {
        private static Module1 _this;

        public static Module1 Current =>
            _this ??= (Module1)FrameworkApplication.FindModule("DimensionOverlay_Module");

        /// <summary>Shared user settings — read by the engine, renderer, and settings pane.</summary>
        public DimensionSettings Settings { get; } = new DimensionSettings();

        private DimensionEngine _engine;
        private EditingMonitor  _monitor;

        /// <summary>
        /// Exposes the engine. Initialized in Initialize() on the UI thread.
        /// </summary>
        public DimensionEngine Engine => _engine;

        protected override bool Initialize()
        {
            Trace.WriteLine("[DIM] Module1 Initialize: Starting Dynamic Dimension Overlay (Default: OFF)");
            _engine = new DimensionEngine();
            if (Settings.IsEnabled)
            {
                _engine.Enable();
            }

            _monitor = new EditingMonitor(_engine);
            _monitor.Start();

            return base.Initialize();
        }

        protected override bool CanUnload()
        {
            _monitor?.Stop();
            _engine?.Dispose();
            return true;
        }

        protected override void Uninitialize()
        {
            _monitor?.Stop();
            _engine?.Dispose();
            base.Uninitialize();
        }
    }
}
