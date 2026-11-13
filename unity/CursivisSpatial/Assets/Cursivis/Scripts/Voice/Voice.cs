using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Cursivis.Spatial.Voice
{
    public static class Wav
    {
        public static byte[] EncodePcm16(float[] samples, int count, int sampleRate)
        {
            using (var ms = new MemoryStream(44 + count * 2))
            using (var w = new BinaryWriter(ms))
            {
                w.Write(Encoding.ASCII.GetBytes("RIFF"));
                w.Write(36 + count * 2);
                w.Write(Encoding.ASCII.GetBytes("WAVE"));
                w.Write(Encoding.ASCII.GetBytes("fmt "));
                w.Write(16);
                w.Write((short)1);
                w.Write((short)1);
                w.Write(sampleRate);
                w.Write(sampleRate * 2);
                w.Write((short)2);
                w.Write((short)16);
                w.Write(Encoding.ASCII.GetBytes("data"));
                w.Write(count * 2);
                for (int i = 0; i < count; i++)
                    w.Write((short)Mathf.Clamp(Mathf.RoundToInt(samples[i] * 32767f), -32768, 32767));
                return ms.ToArray();
            }
        }

        /// <summary>Decodes 16-bit PCM WAV (mono/stereo) into an AudioClip.</summary>
        public static AudioClip ToClip(byte[] wav, string name = "speech")
        {
            if (wav == null || wav.Length < 44 || Encoding.ASCII.GetString(wav, 0, 4) != "RIFF") throw new InvalidDataException("Not a WAV file");
            int pos = 12, channels = 1, rate = 24000, bits = 16, dataStart = -1, dataLen = 0;
            while (pos + 8 <= wav.Length)
            {
                var id = Encoding.ASCII.GetString(wav, pos, 4);
                int size = BitConverter.ToInt32(wav, pos + 4);
                if (id == "fmt ")
                {
                    channels = BitConverter.ToInt16(wav, pos + 10);
                    rate = BitConverter.ToInt32(wav, pos + 12);
                    bits = BitConverter.ToInt16(wav, pos + 22);
                }
                else if (id == "data")
                {
                    dataStart = pos + 8;
                    dataLen = Math.Min(size, wav.Length - dataStart);
                    break;
                }
                pos += 8 + size + (size & 1);
            }
            if (dataStart < 0 || bits != 16) throw new InvalidDataException("Unsupported WAV layout");
            int frames = dataLen / (2 * channels);
            var data = new float[frames * channels];
            for (int i = 0; i < data.Length; i++) data[i] = BitConverter.ToInt16(wav, dataStart + i * 2) / 32768f;
            var clip = AudioClip.Create(name, frames, channels, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        public static float[] Resample(float[] src, int count, int fromRate, int toRate, out int outCount)
        {
            if (fromRate == toRate) { outCount = count; return src; }
            outCount = Mathf.Max(1, (int)((long)count * toRate / fromRate));
            var dst = new float[outCount];
            double step = (double)fromRate / toRate;
            for (int i = 0; i < outCount; i++)
            {
                double p = i * step;
                int a = (int)p;
                int b = Math.Min(a + 1, count - 1);
                float t = (float)(p - a);
                dst[i] = Mathf.Lerp(src[Math.Min(a, count - 1)], src[b], t);
            }
            return dst;
        }
    }

    public enum StopReason { Released, Tapped, Silence, MaxLength, Cancelled }

    /// <summary>
    /// Push-to-talk microphone capture with consent: records only between press and release (or until silence after
    /// speech), measures a live level for the orb, and encodes 16 kHz mono PCM WAV. Detects OS-muted microphones.
    /// </summary>
    public sealed class VoiceCapture : MonoBehaviour
    {
        public const int TargetRate = 16000;
        public float MaxSeconds = 15f;
        public float SilenceStopSeconds = 1.2f;

        public bool IsRecording { get; private set; }
        public float Level { get; private set; }
        public bool HeardSpeech { get; private set; }
        public float RecordedSeconds => _count / (float)Mathf.Max(1, _rate);
        public event Action<StopReason> AutoStopped;

        AudioClip _clip;
        string _device;
        int _rate, _lastPos, _count;
        float[] _buffer = new float[TargetRate * 16];
        float[] _chunk = new float[4096];
        float _noiseFloor = 0.004f, _silence, _started, _peak;
        bool _autoStop;

        public bool HasPermission
        {
            get
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                return UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Microphone);
#else
                return true;
#endif
            }
        }

        public void RequestPermission(Action<bool> done)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            var cb = new UnityEngine.Android.PermissionCallbacks();
            cb.PermissionGranted += _ => done?.Invoke(true);
            cb.PermissionDenied += _ => done?.Invoke(false);
            cb.PermissionDeniedAndDontAskAgain += _ => done?.Invoke(false);
            UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.Microphone, cb);
#else
            done?.Invoke(true);
#endif
        }

        public bool Begin(bool autoStopOnSilence, out string error)
        {
            error = null;
            if (IsRecording) return true;
            if (Microphone.devices == null || Microphone.devices.Length == 0)
            {
                error = "No microphone was found.";
                return false;
            }
            _device = Microphone.devices[0];
            Microphone.GetDeviceCaps(_device, out var minF, out var maxF);
            _rate = (minF == 0 && maxF == 0) ? TargetRate : Mathf.Clamp(TargetRate, minF, maxF);
            if (_rate < 8000) _rate = maxF > 0 ? maxF : 48000;
            _clip = Microphone.Start(_device, true, 20, _rate);
            if (_clip == null)
            {
                error = "The microphone could not be started.";
                return false;
            }
            int needed = Mathf.CeilToInt(_rate * (MaxSeconds + 1f));
            if (_buffer.Length < needed) _buffer = new float[needed];
            _lastPos = 0;
            _count = 0;
            _peak = 0f;
            _silence = 0f;
            HeardSpeech = false;
            _autoStop = autoStopOnSilence;
            _started = Time.realtimeSinceStartup;
            IsRecording = true;
            return true;
        }

        public void SetAutoStop(bool on) => _autoStop = on;

        void Update()
        {
            if (!IsRecording) return;
            int pos = Microphone.GetPosition(_device);
            if (pos < 0) return;
            int available = pos - _lastPos;
            if (available < 0) available += _clip.samples;
            while (available > 0)
            {
                int n = Mathf.Min(available, _chunk.Length, _clip.samples - _lastPos);
                if (n <= 0) break;
                _clip.GetData(_chunk, _lastPos);
                float sum = 0f;
                for (int i = 0; i < n; i++)
                {
                    float s = _chunk[i];
                    sum += s * s;
                    _peak = Mathf.Max(_peak, Mathf.Abs(s));
                    if (_count < _buffer.Length) _buffer[_count++] = s;
                }
                float rms = Mathf.Sqrt(sum / Mathf.Max(1, n));
                float elapsed = Time.realtimeSinceStartup - _started;
                if (elapsed < 0.25f) _noiseFloor = Mathf.Lerp(_noiseFloor, rms, 0.3f);
                bool speech = rms > Mathf.Max(0.012f, _noiseFloor * 3.2f);
                if (speech) { HeardSpeech = true; _silence = 0f; }
                else if (HeardSpeech) _silence += n / (float)_rate;
                Level = Mathf.Clamp01(Mathf.InverseLerp(_noiseFloor, 0.12f, rms));
                _lastPos = (_lastPos + n) % _clip.samples;
                available -= n;
            }
            if (RecordedSeconds >= MaxSeconds) AutoStopped?.Invoke(StopReason.MaxLength);
            else if (_autoStop && HeardSpeech && _silence >= SilenceStopSeconds) AutoStopped?.Invoke(StopReason.Silence);
        }

        /// <summary>Stops recording. Returns 16 kHz WAV or null if nothing usable was captured (error explains why).</summary>
        public byte[] End(out string error, out float seconds)
        {
            error = null;
            seconds = RecordedSeconds;
            if (!IsRecording) { error = "Not recording."; return null; }
            IsRecording = false;
            Level = 0f;
            Microphone.End(_device);
            if (_clip != null) { Destroy(_clip); _clip = null; }
            if (_count < _rate * 0.3f) { error = "That was too short. Hold Talk while you speak."; return null; }
            if (_peak < 0.0005f) { error = "The microphone seems muted. Check the headset's microphone settings."; return null; }
            if (!HeardSpeech) { error = "I didn't catch anything. Hold Talk and speak a little louder."; return null; }
            var samples = Wav.Resample(_buffer, _count, _rate, TargetRate, out var outCount);
            // light normalization so quiet speakers are still transcribed well
            float peak = 0f;
            for (int i = 0; i < outCount; i++) peak = Mathf.Max(peak, Mathf.Abs(samples[i]));
            if (peak > 0.0001f && peak < 0.5f)
            {
                float g = Mathf.Min(4f, 0.7f / peak);
                if (!ReferenceEquals(samples, _buffer)) for (int i = 0; i < outCount; i++) samples[i] *= g;
                else { var copy = new float[outCount]; for (int i = 0; i < outCount; i++) copy[i] = samples[i] * g; samples = copy; }
            }
            return Wav.EncodePcm16(samples, outCount, TargetRate);
        }

        public void Cancel()
        {
            if (!IsRecording) return;
            IsRecording = false;
            Level = 0f;
            Microphone.End(_device);
            if (_clip != null) { Destroy(_clip); _clip = null; }
        }

        void OnApplicationPause(bool paused) { if (paused) Cancel(); }
        void OnDisable() => Cancel();
    }

    /// <summary>
    /// Read-aloud via the backend's Gemini TTS: the answer is split at sentence boundaries, the first chunk is short so
    /// speech starts quickly, and the next chunk is fetched while the current one plays. Cancellable at any time.
    /// </summary>
    public sealed class SpeechPlayer : MonoBehaviour
    {
        public AudioSource Source;
        public Func<string, CancellationToken, Task<byte[]>> Synthesize;
        public bool IsSpeaking { get; private set; }
        public event Action Started, Finished;
        public event Action<string> Failed;
        CancellationTokenSource _cts;

        public static List<string> Chunk(string text, int first = 160, int rest = 360)
        {
            var parts = new List<string>();
            if (string.IsNullOrWhiteSpace(text)) return parts;
            var sentences = Regex.Split(text.Trim(), @"(?<=[\.\!\?\u2026:;])\s+|\n+");
            var sb = new StringBuilder();
            foreach (var s0 in sentences)
            {
                var s = s0.Trim();
                if (s.Length == 0) continue;
                int limit = parts.Count == 0 ? first : rest;
                if (sb.Length > 0 && sb.Length + s.Length + 1 > limit)
                {
                    parts.Add(sb.ToString());
                    sb.Clear();
                }
                while (s.Length > rest)
                {
                    int cut = s.LastIndexOf(' ', rest);
                    if (cut < 40) cut = rest;
                    if (sb.Length > 0) { parts.Add(sb.ToString()); sb.Clear(); }
                    parts.Add(s.Substring(0, cut));
                    s = s.Substring(cut).Trim();
                }
                if (sb.Length > 0) sb.Append(' ');
                sb.Append(s);
            }
            if (sb.Length > 0) parts.Add(sb.ToString());
            return parts;
        }

        public async void Speak(string text)
        {
            Stop();
            var chunks = Chunk(text);
            if (chunks.Count == 0 || Synthesize == null) return;
            _cts = new CancellationTokenSource();
            var ct = _cts.Token;
            IsSpeaking = true;
            Started?.Invoke();
            var t0 = Time.realtimeSinceStartup;
            try
            {
                Task<byte[]> next = Synthesize(chunks[0], ct);
                for (int i = 0; i < chunks.Count; i++)
                {
                    var wav = await next;
                    ct.ThrowIfCancellationRequested();
                    next = i + 1 < chunks.Count ? Synthesize(chunks[i + 1], ct) : null;
                    var clip = Wav.ToClip(wav, "speech_" + i);
                    if (i == 0) CursivisMetrics.Instance?.Sample("tts_first_audio_ms", (Time.realtimeSinceStartup - t0) * 1000f);
                    Source.clip = clip;
                    Source.Play();
                    while (Source.isPlaying || (Source.time > 0 && Source.time < clip.length - 0.02f))
                    {
                        ct.ThrowIfCancellationRequested();
                        await Task.Yield();
                    }
                    Destroy(clip);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                if (!ct.IsCancellationRequested) Failed?.Invoke(e is CursivisApiException api ? api.UserMessage : "Read aloud failed. Please try again.");
            }
            finally
            {
                if (!ct.IsCancellationRequested || IsSpeaking)
                {
                    IsSpeaking = false;
                    Finished?.Invoke();
                }
            }
        }

        public void Stop()
        {
            if (_cts != null)
            {
                _cts.Cancel();
                _cts.Dispose();
                _cts = null;
            }
            if (Source != null) Source.Stop();
            if (IsSpeaking)
            {
                IsSpeaking = false;
                Finished?.Invoke();
            }
        }

        void OnApplicationPause(bool paused) { if (paused) Stop(); }
    }
}
