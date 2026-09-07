import React, { useState, useRef, useEffect } from 'react';
import { 
  Search, 
  Mic, 
  MicOff, 
  Sparkles, 
  CornerDownLeft, 
  Loader2, 
  Link2, 
  PlusCircle, 
  X, 
  Target,
  Radio,
  SendHorizontal
} from 'lucide-react';
import { transcribeAudio } from '../services/api';

const PRESETS = [
  { label: '📊 Top 5 Accessories', query: 'What are the top 5 most popular accessories?' },
  { label: '📑 Revenue in 2013', query: 'What is the total sales revenue (TotalDue) for all orders placed in the year 2013?' },
  { label: '🗂️ Total Stock in Inventory', query: 'What is the total quantity of products currently in stock across all inventory locations?' },
  { label: '🔍 Long-Distance Touring Bikes', query: 'What products or bikes are good for long distance touring and travel?' },
  { label: '🪖 Lightweight Helmets', query: 'Tell me about lightweight helmets with good ventilation' }
];

export default function QueryInput({ 
  onSubmit, 
  isLoading,
  isFollowUp,
  onToggleFollowUp,
  selectedRowContext,
  onClearSelectedRow,
  hasActiveSession,
  turnCount,
  onNewSession
}) {
  const [query, setQuery] = useState('');
  const [isRecording, setIsRecording] = useState(false);
  const [isTranscribing, setIsTranscribing] = useState(false);
  const [recordDuration, setRecordDuration] = useState(0);
  const [liveTranscript, setLiveTranscript] = useState('');

  const mediaRecorderRef = useRef(null);
  const recognitionRef = useRef(null);
  const audioChunksRef = useRef([]);
  const timerRef = useRef(null);

  // Initialize Web Speech API for real-time live preview while speaking
  useEffect(() => {
    const SpeechRecognition = window.SpeechRecognition || window.webkitSpeechRecognition;
    if (SpeechRecognition) {
      const recognition = new SpeechRecognition();
      recognition.continuous = true;
      recognition.interimResults = true;
      recognition.lang = 'en-US';

      recognition.onresult = (event) => {
        let interimTranscript = '';
        for (let i = event.resultIndex; i < event.results.length; ++i) {
          interimTranscript += event.results[i][0].transcript;
        }
        if (interimTranscript.trim()) {
          setLiveTranscript(interimTranscript);
        }
      };

      recognition.onerror = (e) => {
        console.warn('SpeechRecognition warning:', e.error);
      };

      recognitionRef.current = recognition;
    }
  }, []);

  const handleFormSubmit = (e) => {
    e.preventDefault();
    if (query.trim() && !isLoading) {
      onSubmit(query.trim());
    }
  };

  const handlePresetClick = (presetQuery) => {
    setQuery(presetQuery);
    onSubmit(presetQuery, false);
  };

  const startRecording = async () => {
    setLiveTranscript('');
    try {
      const stream = await navigator.mediaDevices.getUserMedia({ audio: true });
      mediaRecorderRef.current = new MediaRecorder(stream);
      audioChunksRef.current = [];

      mediaRecorderRef.current.ondataavailable = (event) => {
        if (event.data.size > 0) {
          audioChunksRef.current.push(event.data);
        }
      };

      mediaRecorderRef.current.start();
      setIsRecording(true);
      setRecordDuration(0);

      // Start live speech recognizer for real-time text feedback
      if (recognitionRef.current) {
        try {
          recognitionRef.current.start();
        } catch (e) {
          console.warn('Live speech recognition already active', e);
        }
      }

      timerRef.current = setInterval(() => {
        setRecordDuration((prev) => prev + 1);
      }, 1000);
    } catch (err) {
      console.error('Microphone Access Error:', err);
      alert('Unable to access microphone. Please grant permission in browser settings.');
    }
  };

  const stopRecording = () => {
    if (!mediaRecorderRef.current || !isRecording) return;

    if (recognitionRef.current) {
      try {
        recognitionRef.current.stop();
      } catch (e) {}
    }

    if (timerRef.current) {
      clearInterval(timerRef.current);
    }

    mediaRecorderRef.current.onstop = async () => {
      const audioBlob = new Blob(audioChunksRef.current, { type: 'audio/wav' });
      setIsRecording(false);
      setIsTranscribing(true);

      try {
        // High accuracy backend transcription via faster-whisper
        const result = await transcribeAudio(audioBlob);
        const text = result?.text?.trim() || liveTranscript.trim();

        if (text) {
          setQuery(text); // Populates the transcribed text directly on the screen!
          onSubmit(text); // Automatically triggers query processing with transcribed text visible!
        } else {
          alert('No clear speech detected. Please try speaking again.');
        }
      } catch (err) {
        console.error('Transcription error, falling back to live transcript:', err);
        if (liveTranscript.trim()) {
          setQuery(liveTranscript.trim());
          onSubmit(liveTranscript.trim());
        } else {
          alert('Voice transcription failed. Please type your query.');
        }
      } finally {
        setIsTranscribing(false);
      }
    };

    mediaRecorderRef.current.stop();
  };

  const cancelRecording = () => {
    if (mediaRecorderRef.current && isRecording) {
      mediaRecorderRef.current.ondataavailable = null;
      mediaRecorderRef.current.onstop = null;
      mediaRecorderRef.current.stop();
      if (recognitionRef.current) {
        try { recognitionRef.current.stop(); } catch (e) {}
      }
      setIsRecording(false);
      setLiveTranscript('');
      if (timerRef.current) {
        clearInterval(timerRef.current);
      }
    }
  };

  return (
    <>
      {/* Cinematic Backdrop Blur while Voice Recording */}
      {isRecording && (
        <div 
          onClick={stopRecording}
          className="fixed inset-0 bg-slate-950/75 backdrop-blur-md z-40 transition-all duration-300 pointer-events-auto"
        />
      )}

      <div className={`w-full bg-slate-900/90 backdrop-blur-xl border rounded-2xl p-4 sm:p-5 shadow-2xl transition-all duration-300 space-y-3.5 ${
        isRecording 
          ? 'relative z-50 ring-2 ring-rose-500/50 border-rose-500/40 shadow-rose-950/60 shadow-2xl scale-[1.01]' 
          : isLoading 
          ? 'border-sky-500/40 shadow-sky-950/40 relative z-10' 
          : 'border-slate-800 shadow-black/40 relative z-10'
      }`}>
        
        {/* Target Row Context Banner */}
        {selectedRowContext && !isRecording && (
          <div className="bg-sky-500/10 border border-sky-500/30 rounded-xl px-3.5 py-2 flex items-center justify-between text-xs text-sky-200 animate-fadeIn">
            <div className="flex items-center gap-2">
              <Target className="w-4 h-4 text-sky-400" />
              <span>
                <strong>Targeting Row Entity:</strong>{' '}
                {selectedRowContext.ProductName || selectedRowContext.product_name || `ID: ${selectedRowContext.ProductID || selectedRowContext.product_id || selectedRowContext.SalesOrderID || ''}`}
              </span>
            </div>
            <button
              type="button"
              onClick={onClearSelectedRow}
              className="text-sky-400 hover:text-sky-200 p-0.5 rounded hover:bg-sky-500/20"
              title="Clear row target"
            >
              <X className="w-3.5 h-3.5" />
            </button>
          </div>
        )}

        {/* Input Box Area */}
        {!isRecording ? (
          <form onSubmit={handleFormSubmit} className="relative flex items-center">
            <div className="absolute left-4 pointer-events-none text-slate-400">
              <Search className="w-5 h-5" />
            </div>

            <input
              type="text"
              value={query}
              onChange={(e) => setQuery(e.target.value)}
              disabled={isLoading || isTranscribing}
              placeholder={
                isTranscribing
                  ? "Transcribing voice input..."
                  : isLoading
                  ? "Query in progress..."
                  : isFollowUp && hasActiveSession
                  ? "Ask a follow-up or drill-down... (e.g. 'Drill down on product 741' or 'Filter by Western region')"
                  : "Ask anything in natural language... (e.g. 'Detailed sales report for 2013')"
              }
              className={`w-full pl-12 pr-36 py-3.5 bg-slate-950/80 border rounded-xl text-slate-100 placeholder-slate-500 text-sm sm:text-base outline-none transition-all duration-200 shadow-inner ${
                isTranscribing
                  ? 'border-emerald-500/50 ring-1 ring-emerald-500/20'
                  : isLoading 
                  ? 'border-sky-500/40 ring-1 ring-sky-500/20' 
                  : 'border-slate-800 focus:border-sky-500 focus:ring-2 focus:ring-sky-500/20'
              }`}
            />

            <div className="absolute right-2 flex items-center space-x-2">

              {/* Microphone Recording Trigger */}
              <button
                type="button"
                onClick={startRecording}
                disabled={isLoading || isTranscribing}
                title="Speak query via microphone"
                className="p-2.5 rounded-lg bg-slate-800 hover:bg-slate-700 text-slate-300 hover:text-sky-400 transition-colors border border-slate-700/60 disabled:opacity-50 cursor-pointer"
              >
                <Mic className="w-4 h-4" />
              </button>

              {/* Submit Button */}
              <button
                type="submit"
                disabled={!query.trim() || isLoading || isTranscribing}
                className={`flex items-center gap-1.5 px-4 py-2 text-white font-medium text-sm rounded-lg transition-all shadow-md cursor-pointer disabled:cursor-not-allowed ${
                  isTranscribing || isLoading
                    ? 'bg-slate-800 border border-slate-700 opacity-90 shadow-none'
                    : 'bg-gradient-to-r from-sky-500 to-indigo-600 hover:from-sky-400 hover:to-indigo-500 shadow-sky-500/20 disabled:opacity-40'
                }`}
              >
                {isTranscribing ? (
                  <div className="flex items-center gap-1.5 text-slate-200">
                    <Loader2 className="w-3.5 h-3.5 animate-spin text-emerald-400" />
                    <span className="text-xs">Transcribing...</span>
                  </div>
                ) : isLoading ? (
                  <div className="flex items-center gap-1.5 text-slate-200">
                    <Loader2 className="w-3.5 h-3.5 animate-spin text-sky-400" />
                    <span className="text-xs">Querying...</span>
                  </div>
                ) : (
                  <>
                    <span>Run</span>
                    <CornerDownLeft className="w-3.5 h-3.5 opacity-70" />
                  </>
                )}
              </button>
            </div>
          </form>
        ) : (
          /* Active Voice Waveform & Live Real-Time Speech Stream */
          <div className="relative flex flex-col sm:flex-row items-stretch sm:items-center justify-between p-3 bg-slate-950 border border-rose-500/40 rounded-xl shadow-inner gap-3">
            
            {/* Left: Soundwave + Live Real-Time Transcript Stream */}
            <div className="flex items-center gap-3 pl-2 min-w-0 flex-1">
              
              {/* Animated Sound Frequency Equalizer */}
              <div className="flex items-center gap-1 h-6 flex-shrink-0">
                <span className="w-1 bg-rose-500 rounded-full animate-wave-1" />
                <span className="w-1 bg-rose-400 rounded-full animate-wave-2" />
                <span className="w-1 bg-rose-500 rounded-full animate-wave-3" />
                <span className="w-1 bg-rose-300 rounded-full animate-wave-4" />
                <span className="w-1 bg-rose-400 rounded-full animate-wave-2" />
                <span className="w-1 bg-rose-500 rounded-full animate-wave-1" />
              </div>

              {/* Real-Time Live Speech Text Display */}
              <div className="flex flex-col min-w-0 flex-1">
                <span className="text-xs font-semibold text-rose-300 flex items-center gap-1.5">
                  <Radio className="w-3 h-3 text-rose-400 animate-pulse" />
                  Listening to voice input...
                </span>
                
                {liveTranscript ? (
                  <p className="text-sm font-medium text-slate-100 truncate animate-fadeIn">
                    &ldquo;{liveTranscript}&rdquo;
                  </p>
                ) : (
                  <p className="text-xs text-slate-400 italic">
                    Speak your question clearly (e.g. &ldquo;Detailed sales report for 2013&rdquo;)
                  </p>
                )}
              </div>
            </div>

            {/* Right: Duration Timer & Single Run Action */}
            <div className="flex items-center justify-end gap-2.5 pr-1 flex-shrink-0">
              
              {/* Duration Timer */}
              <span className="font-mono text-xs font-semibold px-2.5 py-1.5 rounded-lg bg-rose-950/80 text-rose-300 border border-rose-500/30">
                0:{recordDuration < 10 ? `0${recordDuration}` : recordDuration}
              </span>

              {/* Transcribe and Run Button */}
              <button
                type="button"
                onClick={stopRecording}
                className="flex items-center gap-1.5 px-4 py-2 rounded-lg bg-rose-600 hover:bg-rose-500 text-white text-xs font-medium transition-all shadow-md shadow-rose-600/30 cursor-pointer"
                title="Transcribe speech and execute query"
              >
                <SendHorizontal className="w-3.5 h-3.5" />
                <span>Run</span>
              </button>

              {/* Cancel Button */}
              <button
                type="button"
                onClick={cancelRecording}
                className="px-2.5 py-2 text-slate-400 hover:text-slate-200 text-xs rounded-lg hover:bg-slate-800 transition-colors cursor-pointer"
                title="Cancel voice recording"
              >
                Cancel
              </button>
            </div>

          </div>
        )}

        {/* Mode Switcher & Presets Bar */}
        <div className="flex flex-wrap items-center justify-between gap-3 text-xs pt-1 border-t border-slate-800/60">
          
          {/* Follow-Up vs New Query Mode Switcher */}
          <div className="flex items-center gap-3">
            {hasActiveSession ? (
              <div className="flex items-center gap-2 bg-slate-950/80 px-3 py-1.5 rounded-lg border border-slate-800">
                <label className="flex items-center gap-1.5 cursor-pointer text-slate-300 hover:text-white">
                  <input
                    type="radio"
                    name="queryMode"
                    checked={isFollowUp}
                    onChange={() => onToggleFollowUp(true)}
                    className="accent-sky-500"
                  />
                  <span className="flex items-center gap-1 font-medium">
                    <Link2 className="w-3.5 h-3.5 text-sky-400" />
                    Follow-up on Context (Turn #{turnCount})
                  </span>
                </label>

                <span className="text-slate-700">|</span>

                <label className="flex items-center gap-1.5 cursor-pointer text-slate-400 hover:text-slate-200">
                  <input
                    type="radio"
                    name="queryMode"
                    checked={!isFollowUp}
                    onChange={() => onToggleFollowUp(false)}
                    className="accent-sky-500"
                  />
                  <span>Independent Query</span>
                </label>
              </div>
            ) : (
              <span className="text-slate-500 italic text-[11px]">Start your first query below</span>
            )}

            {hasActiveSession && onNewSession && (
              <button
                type="button"
                onClick={onNewSession}
                className="text-slate-400 hover:text-sky-400 flex items-center gap-1 transition-colors cursor-pointer"
              >
                <PlusCircle className="w-3.5 h-3.5" />
                <span>Reset Session</span>
              </button>
            )}
          </div>

          {/* Preset Chips */}
          <div className="flex items-center gap-2 flex-wrap">
            <span className="text-slate-500 font-medium flex items-center gap-1">
              <Sparkles className="w-3 h-3 text-amber-400" />
              Presets:
            </span>
            {PRESETS.slice(0, 3).map((p, idx) => (
              <button
                key={idx}
                type="button"
                onClick={() => handlePresetClick(p.query)}
                disabled={isLoading || isTranscribing}
                className="px-2 py-0.5 bg-slate-800/80 hover:bg-slate-700 border border-slate-700/60 rounded-md text-slate-400 hover:text-slate-200 transition-all text-[11px] cursor-pointer disabled:opacity-50"
              >
                {p.label}
              </button>
            ))}
          </div>

        </div>

      </div>
    </>
  );
}
