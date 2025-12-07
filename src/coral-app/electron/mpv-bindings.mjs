/**
 * libmpv FFI bindings using koffi
 * JavaScript implementation for Electron main process
 */

/* eslint-env node */
import koffi from 'koffi';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

// ESM equivalent of __dirname
const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

// Platform detection
function getPlatformLibPath() {
  const platform = process.platform;

  // In production (packaged app), extraResources are in process.resourcesPath
  // In development, they're relative to the electron directory
  const isDevelopment = process.env.NODE_ENV === 'development' || !process.resourcesPath;
  const baseDir = isDevelopment
    ? path.join(__dirname, 'native/libmpv')
    : path.join(process.resourcesPath, 'native/libmpv');

  const brewDir = '/opt/homebrew/lib'

  switch (platform) {
    case 'win32':
      return path.join(baseDir, 'win', 'libmpv-2.dll');
    case 'darwin':
      return path.join(brewDir, 'libmpv.dylib');
    case 'linux':
      return path.join(baseDir, 'linux', 'libmpv.so');
    default:
      throw new Error(`Unsupported platform: ${platform}`);
  }
}

// Load the libmpv library
let lib;

try {
  const libPath = getPlatformLibPath();
  lib = koffi.load(libPath);
  console.info('[MpvBindings] Loaded libmpv from:', libPath);
} catch (error) {
  console.error('[MpvBindings] Failed to load libmpv:', error);
  throw new Error(`Failed to load libmpv library: ${error}`);
}

// mpv_event types (enums)
export const MpvEventId = {
  MPV_EVENT_NONE: 0,
  MPV_EVENT_SHUTDOWN: 1,
  MPV_EVENT_LOG_MESSAGE: 2,
  MPV_EVENT_GET_PROPERTY_REPLY: 3,
  MPV_EVENT_SET_PROPERTY_REPLY: 4,
  MPV_EVENT_COMMAND_REPLY: 5,
  MPV_EVENT_START_FILE: 6,
  MPV_EVENT_END_FILE: 7,
  MPV_EVENT_FILE_LOADED: 8,
  MPV_EVENT_IDLE: 11,
  MPV_EVENT_TICK: 14,
  MPV_EVENT_CLIENT_MESSAGE: 16,
  MPV_EVENT_VIDEO_RECONFIG: 17,
  MPV_EVENT_AUDIO_RECONFIG: 18,
  MPV_EVENT_SEEK: 20,
  MPV_EVENT_PLAYBACK_RESTART: 21,
  MPV_EVENT_PROPERTY_CHANGE: 22,
  MPV_EVENT_QUEUE_OVERFLOW: 24,
};

export const MpvEndFileReason = {
  MPV_END_FILE_REASON_EOF: 0,
  MPV_END_FILE_REASON_STOP: 2,
  MPV_END_FILE_REASON_QUIT: 3,
  MPV_END_FILE_REASON_ERROR: 4,
  MPV_END_FILE_REASON_REDIRECT: 5,
};

export const MpvFormat = {
  MPV_FORMAT_NONE: 0,
  MPV_FORMAT_STRING: 1,
  MPV_FORMAT_OSD_STRING: 2,
  MPV_FORMAT_FLAG: 3,
  MPV_FORMAT_INT64: 4,
  MPV_FORMAT_DOUBLE: 5,
  MPV_FORMAT_NODE: 6,
  MPV_FORMAT_NODE_ARRAY: 7,
  MPV_FORMAT_NODE_MAP: 8,
  MPV_FORMAT_BYTE_ARRAY: 9,
};

// Event structures
export const MpvEventEndFileStruct = koffi.struct('mpv_event_end_file', {
  reason: 'int',
  error: 'int',
  playlist_entry_id: 'int64',
});

export const MpvEventPropertyStruct = koffi.struct('mpv_event_property', {
  name: 'string',
  format: 'int',
  data: 'void*',
});

export const MpvEventStruct = koffi.struct('mpv_event', {
  event_id: 'int',
  error: 'int',
  reply_userdata: 'uint64',
  data: 'void*',
});

// Core functions - matching official signatures from client.h
export const mpv_create = lib.func('mpv_create', 'void*', []);
export const mpv_initialize = lib.func('mpv_initialize', 'int', ['void*']);
export const mpv_destroy = lib.func('mpv_destroy', 'void', ['void*']);
export const mpv_terminate_destroy = lib.func('mpv_terminate_destroy', 'void', ['void*']);

// Command functions
export const mpv_command = lib.func('mpv_command', 'int', ['void*', koffi.pointer('string')]);
export const mpv_command_string = lib.func('mpv_command_string', 'int', ['void*', 'string']);
export const mpv_command_async = lib.func('mpv_command_async', 'int', ['void*', 'uint64', koffi.pointer('string')]);

// Property functions
export const mpv_set_property_string = lib.func('mpv_set_property_string', 'int', ['void*', 'string', 'string']);
export const mpv_get_property_string = lib.func('mpv_get_property_string', 'string', ['void*', 'string']);
export const mpv_set_property = lib.func('mpv_set_property', 'int', ['void*', 'string', 'int', 'void*']);
export const mpv_get_property = lib.func('mpv_get_property', 'int', ['void*', 'string', 'int', 'void*']);

// Property observation
export const mpv_observe_property = lib.func('mpv_observe_property', 'int', ['void*', 'uint64', 'string', 'int']);
export const mpv_unobserve_property = lib.func('mpv_unobserve_property', 'int', ['void*', 'uint64']);

// Event functions
export const mpv_wait_event = lib.func('mpv_wait_event', koffi.pointer(MpvEventStruct), ['void*', 'double']);
export const mpv_request_event = lib.func('mpv_request_event', 'int', ['void*', 'int', 'int']);
export const mpv_request_log_messages = lib.func('mpv_request_log_messages', 'int', ['void*', 'string']);

// Error handling
export const mpv_error_string = lib.func('mpv_error_string', 'string', ['int']);

// Helper function to create command arrays for mpv_command
export function createCommandArray(args) {
  // Allocate array with null terminator
  const arr = new Array(args.length + 1);
  for (let i = 0; i < args.length; i++) {
    arr[i] = args[i];
  }
  arr[args.length] = null;
  return arr;
}

// Helper to check error codes
export function checkMpvError(errorCode, operation) {
  if (errorCode < 0) {
    const errorMsg = mpv_error_string(errorCode);
    throw new Error(`[Mpv] ${operation} failed: ${errorMsg} (code: ${errorCode})`);
  }
}

// Export koffi for decoding pointers
export { koffi };
