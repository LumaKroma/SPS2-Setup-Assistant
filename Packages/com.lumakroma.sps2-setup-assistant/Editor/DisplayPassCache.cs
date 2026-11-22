using System;

namespace LumaKroma.Sps2SetupAssistant.Editor
{
    // Display only. A pass is one Layout or Repaint, never a timer or input event.
    // End releases all captured references, including after a drawing exception.
    internal sealed class DisplayPassCache<T> where T : class
    {
        private bool enabled;
        private T value;
        private Func<bool> contextCurrent;
        internal void Begin(bool displayOnly) { End(); enabled = displayOnly; }
        internal void End() { enabled = false; Invalidate(); }
        internal void Invalidate() { value = null; contextCurrent = null; }
        internal T Get(Func<T> capture, Func<Func<bool>> captureContext)
        {
            if (!enabled) return capture(); // Input, callbacks and direct calls stay fresh.
            if (value != null && contextCurrent()) return value;
            // Never retain a prior result if capture or context capture fails.
            Invalidate();
            var next = capture();
            var nextContext = captureContext();
            value = next; contextCurrent = nextContext;
            return value;
        }
    }
}
