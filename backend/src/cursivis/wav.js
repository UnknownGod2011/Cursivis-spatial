// Normalizes Gemini TTS output to a WAV buffer. Newer TTS models return audio/wav; older ones return
// raw little-endian 16-bit PCM ("audio/L16;codec=pcm;rate=24000"), which we wrap in a RIFF header.

export function pcmToWav(pcm, sampleRate = 24000, channels = 1, bitsPerSample = 16) {
  const header = Buffer.alloc(44);
  const byteRate = (sampleRate * channels * bitsPerSample) / 8;
  header.write("RIFF", 0, "ascii");
  header.writeUInt32LE(36 + pcm.length, 4);
  header.write("WAVE", 8, "ascii");
  header.write("fmt ", 12, "ascii");
  header.writeUInt32LE(16, 16);
  header.writeUInt16LE(1, 20);
  header.writeUInt16LE(channels, 22);
  header.writeUInt32LE(sampleRate, 24);
  header.writeUInt32LE(byteRate, 28);
  header.writeUInt16LE((channels * bitsPerSample) / 8, 32);
  header.writeUInt16LE(bitsPerSample, 34);
  header.write("data", 36, "ascii");
  header.writeUInt32LE(pcm.length, 40);
  return Buffer.concat([header, pcm]);
}

export function ensureWav(base64, mimeType = "") {
  const bytes = Buffer.from(base64, "base64");
  if (bytes.length >= 12 && bytes.toString("ascii", 0, 4) === "RIFF" && bytes.toString("ascii", 8, 12) === "WAVE") {
    return bytes;
  }
  const rate = Number((String(mimeType).match(/rate=(\d+)/i) || [])[1]) || 24000;
  const channels = Number((String(mimeType).match(/channels=(\d+)/i) || [])[1]) || 1;
  return pcmToWav(bytes, rate, channels, 16);
}

// Basic WAV inspection for request validation (Talk audio from the headset).
export function inspectWav(buffer) {
  if (buffer.length < 44 || buffer.toString("ascii", 0, 4) !== "RIFF" || buffer.toString("ascii", 8, 12) !== "WAVE") {
    return null;
  }
  let offset = 12;
  let fmt = null;
  let dataBytes = 0;
  while (offset + 8 <= buffer.length) {
    const id = buffer.toString("ascii", offset, offset + 4);
    const size = buffer.readUInt32LE(offset + 4);
    if (id === "fmt ") {
      fmt = {
        format: buffer.readUInt16LE(offset + 8),
        channels: buffer.readUInt16LE(offset + 10),
        sampleRate: buffer.readUInt32LE(offset + 12),
        bitsPerSample: buffer.readUInt16LE(offset + 22)
      };
    } else if (id === "data") {
      dataBytes = Math.min(size, buffer.length - offset - 8);
      break;
    }
    offset += 8 + size + (size % 2);
  }
  if (!fmt) return null;
  const bytesPerSecond = (fmt.sampleRate * fmt.channels * fmt.bitsPerSample) / 8;
  return { ...fmt, dataBytes, durationMs: bytesPerSecond ? Math.round((dataBytes / bytesPerSecond) * 1000) : 0 };
}
