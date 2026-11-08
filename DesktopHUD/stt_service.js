/**
 * AIGRAV Desktop HUD - Local STT Service (whisper.cpp)
 * Manages isolated child-process transcription workers, concurrency limits,
 * model availability checks, temporary audio files, and error handling.
 */

const fs = require('fs');
const path = require('path');
const os = require('os');
const { spawn } = require('child_process');

const STT_CONFIG_PATH = 'C:\\AIGRAV\\STT\\config.json';
const DEFAULT_BINARY = 'C:\\AIGRAV\\STT\\bin\\whisper-cli.exe';
const DEFAULT_MODEL = 'C:\\AIGRAV\\STT\\models\\ggml-base.bin';

class LocalSTTService {
  constructor() {
    this.isTranscribing = false;
    this.config = this.loadConfig();
  }

  loadConfig() {
    try {
      if (fs.existsSync(STT_CONFIG_PATH)) {
        const raw = fs.readFileSync(STT_CONFIG_PATH, 'utf8');
        return JSON.parse(raw);
      }
    } catch (e) {
      console.warn('[STT_SERVICE]: Could not read config.json, using defaults:', e.message);
    }
    return {
      engine: 'whisper.cpp',
      binary_path: DEFAULT_BINARY,
      model_path: DEFAULT_MODEL,
      language: 'id',
      threads: 4,
      timeout_ms: 30000
    };
  }

  checkStatus() {
    const config = this.loadConfig();
    const binaryExists = fs.existsSync(config.binary_path);
    const modelExists = fs.existsSync(config.model_path);
    let modelSizeBytes = 0;
    if (modelExists) {
      try {
        modelSizeBytes = fs.statSync(config.model_path).size;
      } catch (e) {}
    }

    const isReady = binaryExists && modelExists && modelSizeBytes > 1000000;

    let message = 'STT Lokal Offline Siap (whisper.cpp - ggml-base.bin)';
    if (!binaryExists) {
      message = 'Engine STT belum terpasang (whisper-cli.exe tidak ditemukan).';
    } else if (!modelExists || modelSizeBytes < 1000000) {
      message = 'Model STT belum terpasang (ggml-base.bin tidak ditemukan).';
    }

    return {
      available: isReady,
      engine: config.engine || 'whisper.cpp',
      model_path: config.model_path,
      model_size_mb: (modelSizeBytes / (1024 * 1024)).toFixed(1),
      language: config.language || 'id',
      status_message: message
    };
  }

  /**
   * Transcribes a raw WAV or audio buffer.
   * @param {Buffer|ArrayBuffer|Uint8Array} audioData
   * @returns {Promise<{success: boolean, text?: string, error?: string}>}
   */
  async transcribe(audioData) {
    if (this.isTranscribing) {
      return {
        success: false,
        error: 'Sesi transkripsi sedang berjalan. Mohon tunggu proses sebelumnya selesai.'
      };
    }

    const status = this.checkStatus();
    if (!status.available) {
      return {
        success: false,
        error: status.status_message
      };
    }

    if (!audioData || audioData.length < 500) {
      return {
        success: false,
        error: 'Data audio terlalu pendek atau kosong.'
      };
    }

    this.isTranscribing = true;

    // Convert to Buffer if ArrayBuffer / Uint8Array
    let buf;
    if (Buffer.isBuffer(audioData)) {
      buf = audioData;
    } else if (audioData instanceof ArrayBuffer) {
      buf = Buffer.from(audioData);
    } else if (audioData.buffer instanceof ArrayBuffer) {
      buf = Buffer.from(audioData.buffer, audioData.byteOffset, audioData.byteLength);
    } else {
      buf = Buffer.from(audioData);
    }

    const tempWavPath = path.join(os.tmpdir(), `aigrav_rec_${Date.now()}.wav`);

    try {
      fs.writeFileSync(tempWavPath, buf);

      const config = this.loadConfig();
      const args = [
        '-m', config.model_path,
        '-f', tempWavPath,
        '-l', config.language || 'id',
        '-nt',
        '-np',
        '-t', String(config.threads || 4),
        '-ng'
      ];

      if (config.prompt && typeof config.prompt === 'string' && config.prompt.trim().length > 0) {
        args.push('--prompt', config.prompt.trim());
      }

      console.log('[STT_SERVICE]: Spawning whisper-cli with args:', args);

      const resultText = await new Promise((resolve, reject) => {
        let stdoutData = '';
        let stderrData = '';
        const child = spawn(config.binary_path, args, {
          windowsHide: true,
          stdio: ['ignore', 'pipe', 'pipe']
        });

        const timer = setTimeout(() => {
          try {
            child.kill('SIGKILL');
          } catch (e) {}
          reject(new Error('Batas waktu transkripsi suara (timeout 30s) terlampaui.'));
        }, config.timeout_ms || 30000);

        child.stdout.on('data', (d) => {
          stdoutData += d.toString();
        });

        child.stderr.on('data', (d) => {
          stderrData += d.toString();
        });

        child.on('error', (err) => {
          clearTimeout(timer);
          reject(new Error(`Gagal menjalankan whisper-cli: ${err.message}`));
        });

        child.on('close', (code) => {
          clearTimeout(timer);
          if (code === 0) {
            resolve(stdoutData);
          } else {
            reject(new Error(`Proses whisper-cli selesai dengan kode kesalahan ${code}: ${stderrData || stdoutData}`));
          }
        });
      });

      // Post-process transcribed text
      let cleaned = resultText
        .replace(/\[BLANK_AUDIO\]/gi, '')
        .replace(/\[MUSIC\]/gi, '')
        .replace(/\[APPLAUSE\]/gi, '')
        .replace(/\[LAUGHTER\]/gi, '')
        .trim();

      // Guard against classic Whisper hallucinations on silence / low background noise
      const SILENCE_HALLUCINATIONS = [
        "apa-apa lewat pun tak tak tahu",
        "apa-apa lewat pun tak tahu",
        "terima kasih telah menonton",
        "jangan lupa subscribe",
        "bagaimana untuk berhubungan",
        "kepakatan yang berlaku"
      ];
      const normalizedCheck = cleaned.toLowerCase().replace(/[.,!?-]/g, '').trim();
      if (SILENCE_HALLUCINATIONS.some(h => normalizedCheck === h || normalizedCheck.startsWith(h))) {
        console.warn(`[STT_SERVICE]: Filtered out Whisper silence hallucination: "${cleaned}"`);
        cleaned = '';
      }

      if (!cleaned || cleaned.length === 0) {
        return {
          success: false,
          error: "Suara percakapan tidak dapat dikenali."
        };
      }

      console.log(`[STT_SERVICE]: Transcription completed successfully: "${cleaned}"`);

      return {
        success: true,
        text: cleaned
      };
    } catch (err) {
      console.error('[STT_SERVICE]: Transcription error:', err);
      return {
        success: false,
        error: err.message || 'Terjadi kesalahan saat memproses audio lokal.'
      };
    } finally {
      this.isTranscribing = false;
      // Clean up temporary audio file
      try {
        if (fs.existsSync(tempWavPath)) {
          fs.unlinkSync(tempWavPath);
        }
      } catch (e) {}
    }
  }
}

module.exports = new LocalSTTService();
