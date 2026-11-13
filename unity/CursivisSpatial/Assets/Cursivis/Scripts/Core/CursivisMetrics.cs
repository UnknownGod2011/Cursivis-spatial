using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace Cursivis.Spatial
{
    /// <summary>
    /// Measured (never estimated) runtime metrics: gesture-to-feedback latency, time to suggestions,
    /// action/voice latency, capture time, request errors, frame rate and memory. Every sample is logged as a
    /// [CursivisMetric] line and the aggregate is written to persistentDataPath/cursivis-metrics.json.
    /// </summary>
    public sealed class CursivisMetrics : MonoBehaviour
    {
        public static CursivisMetrics Instance { get; private set; }

        sealed class Series
        {
            public readonly List<float> Values = new List<float>();
            public void Add(float v) { if (Values.Count < 2000) Values.Add(v); }
        }

        readonly Dictionary<string, Series> _series = new Dictionary<string, Series>();
        readonly Dictionary<string, int> _counters = new Dictionary<string, int>();
        readonly Dictionary<string, float> _marks = new Dictionary<string, float>();
        float _fpsWindowStart;
        int _fpsFrames;
        float _lastFlush;
        public float CurrentFps { get; private set; }

        void Awake()
        {
            Instance = this;
            _fpsWindowStart = Time.realtimeSinceStartup;
        }

        void Update()
        {
            _fpsFrames++;
            var now = Time.realtimeSinceStartup;
            if (now - _fpsWindowStart >= 1f)
            {
                CurrentFps = _fpsFrames / (now - _fpsWindowStart);
                Sample("fps", CurrentFps, log: false);
                _fpsFrames = 0;
                _fpsWindowStart = now;
                if (Time.frameCount % 5 == 0) Sample("memory_mb", (float)(GC.GetTotalMemory(false) / (1024.0 * 1024.0)), log: false);
            }
            if (now - _lastFlush > 30f)
            {
                _lastFlush = now;
                Flush();
            }
        }

        public static void Mark(string name)
        {
            if (Instance == null) return;
            Instance._marks[name] = Time.realtimeSinceStartup;
        }

        /// <summary>Records milliseconds elapsed since Mark(startMark) as metric `name`.</summary>
        public static float Since(string startMark, string name)
        {
            if (Instance == null || !Instance._marks.TryGetValue(startMark, out var t0)) return -1f;
            var ms = (Time.realtimeSinceStartup - t0) * 1000f;
            Instance.Sample(name, ms);
            return ms;
        }

        public static void Count(string name)
        {
            if (Instance == null) return;
            Instance._counters.TryGetValue(name, out var c);
            Instance._counters[name] = c + 1;
            Debug.Log($"[CursivisMetric] count {name}={c + 1}");
        }

        public void Sample(string name, float value, bool log = true)
        {
            if (!_series.TryGetValue(name, out var s)) _series[name] = s = new Series();
            s.Add(value);
            if (log) Debug.Log($"[CursivisMetric] {name}={value:0.0}");
        }

        public string Summary()
        {
            var sb = new StringBuilder();
            sb.Append("{\"platform\":\"").Append(Application.platform).Append("\",\"device\":\"").Append(SystemInfo.deviceModel.Replace("\"", "'"))
              .Append("\",\"gpu\":\"").Append(SystemInfo.graphicsDeviceName.Replace("\"", "'")).Append("\",\"when\":\"").Append(DateTime.UtcNow.ToString("o")).Append("\",\"series\":{");
            bool first = true;
            foreach (var kv in _series)
            {
                var v = kv.Value.Values;
                if (v.Count == 0) continue;
                var sorted = new List<float>(v);
                sorted.Sort();
                float sum = 0f;
                foreach (var x in v) sum += x;
                if (!first) sb.Append(',');
                first = false;
                sb.Append('"').Append(kv.Key).Append("\":{\"n\":").Append(v.Count)
                  .Append(",\"mean\":").Append((sum / v.Count).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture))
                  .Append(",\"p50\":").Append(sorted[sorted.Count / 2].ToString("0.0", System.Globalization.CultureInfo.InvariantCulture))
                  .Append(",\"p90\":").Append(sorted[Mathf.Min(sorted.Count - 1, (int)(sorted.Count * 0.9f))].ToString("0.0", System.Globalization.CultureInfo.InvariantCulture))
                  .Append(",\"min\":").Append(sorted[0].ToString("0.0", System.Globalization.CultureInfo.InvariantCulture))
                  .Append(",\"max\":").Append(sorted[sorted.Count - 1].ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)).Append('}');
            }
            sb.Append("},\"counters\":{");
            first = true;
            foreach (var kv in _counters)
            {
                if (!first) sb.Append(',');
                first = false;
                sb.Append('"').Append(kv.Key).Append("\":").Append(kv.Value);
            }
            sb.Append("}}");
            return sb.ToString();
        }

        public void Flush()
        {
            try
            {
                File.WriteAllText(Path.Combine(Application.persistentDataPath, "cursivis-metrics.json"), Summary());
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Cursivis] metrics flush failed: {e.Message}");
            }
        }

        void OnApplicationPause(bool paused)
        {
            if (paused) Flush();
        }

        void OnDestroy() => Flush();
    }
}
