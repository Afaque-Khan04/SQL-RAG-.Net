import React from 'react';
import { Clock, Database, FileSpreadsheet, CheckCircle, AlertTriangle, Layers } from 'lucide-react';

export default function MetricsBar({ result, onExportCsv }) {
  if (!result) return null;

  const isStructured = String(result.intent || '').toLowerCase() === 'structured' || Boolean(result.generated_sql);
  const rowCount = result.results?.length || 0;
  const totalRecords = result.total_records || rowCount;
  const isTruncated = Boolean(result.is_truncated);
  const execTime = Math.round(result.execution_time_ms || 0);

  return (
    <div className="bg-slate-900/80 border border-slate-800 rounded-2xl p-4 flex flex-wrap items-center justify-between gap-3 shadow-lg shadow-black/20">
      
      {/* Left Badges */}
      <div className="flex items-center flex-wrap gap-2.5 text-xs font-medium">
        
        {/* Intent Badge */}
        {isStructured ? (
          <span className="flex items-center gap-1.5 px-3 py-1 bg-sky-500/15 text-sky-400 border border-sky-500/30 rounded-lg font-semibold tracking-wide uppercase">
            <Database className="w-3.5 h-3.5" />
            <span>SQL Structured Output</span>
          </span>
        ) : (
          <span className="flex items-center gap-1.5 px-3 py-1 bg-emerald-500/15 text-emerald-400 border border-emerald-500/30 rounded-lg font-semibold tracking-wide uppercase">
            <Layers className="w-3.5 h-3.5" />
            <span>Vector Semantic Output</span>
          </span>
        )}

        {/* Execution Time */}
        <span className="flex items-center gap-1.5 px-3 py-1 bg-slate-800 text-slate-300 border border-slate-700/60 rounded-lg">
          <Clock className="w-3.5 h-3.5 text-amber-400" />
          <span>{execTime} ms</span>
        </span>

        {/* Row Count */}
        <span className="flex items-center gap-1.5 px-3 py-1 bg-slate-800 text-slate-300 border border-slate-700/60 rounded-lg">
          <CheckCircle className="w-3.5 h-3.5 text-emerald-400" />
          <span>{rowCount.toLocaleString()} record{rowCount !== 1 ? 's' : ''} retrieved</span>
        </span>

        {/* Truncation Warning (if any) */}
        {isTruncated && (
          <span className="flex items-center gap-1.5 px-3 py-1 bg-amber-500/15 text-amber-300 border border-amber-500/30 rounded-lg font-medium">
            <AlertTriangle className="w-3.5 h-3.5 text-amber-400" />
            <span>Capped at {rowCount.toLocaleString()} (Total: {totalRecords.toLocaleString()})</span>
          </span>
        )}

        {/* Transcribed Audio Indicator (if applicable) */}
        {result.transcribed_text && (
          <span className="px-3 py-1 bg-indigo-500/15 text-indigo-300 border border-indigo-500/30 rounded-lg italic">
            🎙️ Voice Query: "{result.transcribed_text}"
          </span>
        )}
      </div>

      {/* Right Action: CSV Export */}
      {rowCount > 0 && onExportCsv && (
        <button
          type="button"
          onClick={onExportCsv}
          className="flex items-center gap-1.5 px-3.5 py-1.5 bg-emerald-600 hover:bg-emerald-500 text-white rounded-lg text-xs font-semibold transition-all shadow-md shadow-emerald-600/25 hover:shadow-emerald-500/40 cursor-pointer"
        >
          <FileSpreadsheet className="w-3.5 h-3.5" />
          <span>Export to CSV</span>
        </button>
      )}

    </div>
  );
}
