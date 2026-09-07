import React from 'react';
import { AlertCircle, X } from 'lucide-react';

export default function ErrorBanner({ error, onDismiss }) {
  if (!error) return null;

  return (
    <div className="w-full bg-rose-500/10 border border-rose-500/30 rounded-xl p-4 flex items-start justify-between gap-3 text-rose-200 shadow-lg shadow-rose-950/30 animate-fadeIn">
      <div className="flex items-start gap-3">
        <div className="p-1 bg-rose-500/20 rounded-lg text-rose-400 mt-0.5">
          <AlertCircle className="w-5 h-5" />
        </div>
        <div>
          <h4 className="text-sm font-semibold text-rose-300">Execution Error</h4>
          <p className="text-xs text-rose-200/90 mt-0.5 leading-relaxed font-mono">
            {error}
          </p>
        </div>
      </div>

      {onDismiss && (
        <button
          type="button"
          onClick={onDismiss}
          className="text-rose-400 hover:text-rose-200 p-1 rounded-md hover:bg-rose-500/20 transition-colors"
        >
          <X className="w-4 h-4" />
        </button>
      )}
    </div>
  );
}
