/**
 * API client module for communicating with the FastAPI backend.
 */

const BASE_URL = typeof window !== 'undefined' ? window.location.origin : 'http://localhost:5000';

async function parseResponse(response, defaultErrorMsg = 'Request failed') {
  const rawText = await response.text();
  let data = null;
  try {
    data = rawText ? JSON.parse(rawText) : null;
  } catch {
    data = null;
  }

  if (!response.ok) {
    const errorDetail = data?.detail || data?.error || data?.message || (rawText ? rawText.slice(0, 300) : null) || `${defaultErrorMsg} (status ${response.status})`;
    throw new Error(errorDetail);
  }

  return data;
}

/**
 * Execute a natural language text query (independent or contextual follow-up).
 * @param {string} query 
 * @param {string|null} sessionId
 * @param {boolean} isFollowUp
 * @param {Object|null} selectedRowContext
 * @returns {Promise<Object>}
 */
export async function executeQuery(query, sessionId = null, isFollowUp = false, selectedRowContext = null) {
  try {
    const response = await fetch(`${BASE_URL}/query`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
      },
      body: JSON.stringify({ 
        query: query.trim(),
        session_id: sessionId,
        is_follow_up: isFollowUp,
        selected_row_context: selectedRowContext
      }),
    });

    return await parseResponse(response, 'Server returned an error');
  } catch (err) {
    console.error('API Query Error:', err);
    throw err;
  }
}

/**
 * Transcribe speech audio to text without running the full query.
 * @param {Blob} audioBlob 
 * @returns {Promise<Object>}
 */
export async function transcribeAudio(audioBlob) {
  try {
    const formData = new FormData();
    formData.append('file', audioBlob, 'recording.wav');

    const response = await fetch(`${BASE_URL}/transcribe`, {
      method: 'POST',
      body: formData,
    });

    return await parseResponse(response, 'Audio Transcription failed');
  } catch (err) {
    console.error('API Transcribe Error:', err);
    throw err;
  }
}

/**
 * Execute a voice audio query (WAV/WEBM/MP3 Blob).
 * @param {Blob} audioBlob 
 * @param {string|null} sessionId
 * @param {boolean} isFollowUp
 * @returns {Promise<Object>}
 */
export async function executeAudioQuery(audioBlob, sessionId = null, isFollowUp = false) {
  try {
    const formData = new FormData();
    formData.append('file', audioBlob, 'recording.wav');
    if (sessionId) formData.append('session_id', sessionId);
    formData.append('is_follow_up', isFollowUp ? 'true' : 'false');

    const response = await fetch(`${BASE_URL}/query_audio`, {
      method: 'POST',
      body: formData,
    });

    return await parseResponse(response, 'Audio Query failed');
  } catch (err) {
    console.error('API Audio Query Error:', err);
    throw err;
  }
}

/**
 * Initialize a fresh session on backend.
 * @returns {Promise<string>}
 */
export async function createNewSession() {
  try {
    const response = await fetch(`${BASE_URL}/session/new`, { method: 'POST' });
    if (response.ok) {
      const data = await response.json();
      return data.session_id;
    }
  } catch (err) {
    console.warn('Unable to create backend session, generating local fallback', err);
  }
  return `sess_${Math.random().toString(36).substring(2, 11)}`;
}

/**
 * Check backend system health and data source status.
 * @returns {Promise<Object>}
 */
export async function checkSystemHealth() {
  try {
    const response = await fetch(`${BASE_URL}/health`, {
      method: 'GET',
    });
    if (response.ok) {
      return await response.json();
    }
    return { status: 'error', database: 'disconnected', vector_store: 'unknown' };
  } catch (err) {
    return { status: 'offline', database: 'offline', vector_store: 'offline' };
  }
}
