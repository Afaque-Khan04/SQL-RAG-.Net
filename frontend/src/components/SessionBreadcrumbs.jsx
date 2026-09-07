import React from 'react';
import { GitCommit, PlusCircle, ArrowRight, RotateCcw } from 'lucide-react';

export default function SessionBreadcrumbs({ 
  turns, 
  activeTurnIndex, 
  onSelectTurn, 
  onNewSession 
}) {
  if (!turns || turns.length === 0) return null;

  return (
    <div className="bg-slate-900/70 border border-slate-800 rounded-xl p-3 flex flex-wrap items-center justify-between gap-3 shadow-md">
      
      {/* Breadcrumb Thread */}
      <div className="flex items-center flex-wrap gap-2 text-xs">
        <div className="flex items-center gap-1 text-slate-400 font-semibold uppercase tracking-wider text-[11px] mr-1">
          <GitCommit className="w-3.5 h-3.5 text-indigo-400" />
          <span>Thread:</span>
        </div>

        {turns.map((t, idx) => {
          const isActive = activeTurnIndex === t.turn_index || (!activeTurnIndex && idx === turns.length - 1);
          return (
            <React.Fragment key={idx}>
              {idx > 0 && <ArrowRight className="w-3 h-3 text-slate-600 flex-shrink-0" />}
              
              <button
                type="button"
                onClick={() => onSelectTurn(t)}
                className={`flex items-center gap-1.5 px-2.5 py-1 rounded-lg transition-all text-xs cursor-pointer border ${
                  isActive
                    ? 'bg-sky-500/20 text-sky-300 border-sky-500/40 font-semibold shadow-sm shadow-sky-500/20'
                    : 'bg-slate-950/60 hover:bg-slate-800 text-slate-400 hover:text-slate-200 border-slate-800'
                }`}
              >
                <span className="font-mono text-[11px] opacity-70">#{t.turn_index}</span>
                <span className="max-w-[160px] truncate">{t.query}</span>
                <span className="text-[10px] px-1.5 py-0.2 rounded-full bg-slate-800 text-slate-400 font-mono">
                  {t.total_rows || t.results?.length || 0}
                </span>
              </button>
            </React.Fragment>
          );
        })}
      </div>

      {/* New Independent Session Action */}
      <button
        type="button"
        onClick={onNewSession}
        className="flex items-center gap-1 px-3 py-1 bg-slate-800 hover:bg-slate-700 text-slate-300 hover:text-white rounded-lg text-xs font-semibold border border-slate-700 transition-colors cursor-pointer ml-auto"
      >
        <PlusCircle className="w-3.5 h-3.5 text-sky-400" />
        <span>New Session</span>
      </button>

    </div>
  );
}
