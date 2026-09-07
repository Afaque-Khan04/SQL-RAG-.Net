import React, { useState, useEffect } from 'react';
import { Loader2, Database, Clock } from 'lucide-react';

export default function ThinkingIndicator({ queryText }) {
  const [elapsedMs, setElapsedMs] = useState(0);

  useEffect(() => {
    const startTime = Date.now();
    const interval = setInterval(() => {
      setElapsedMs(Date.now() - startTime);
    }, 100);

    return () => clearInterval(interval);
  }, []);

  const formattedSeconds = (elapsedMs / 1000).toFixed(1);

  return (
    <div className="w-full bg-slate-900/90 backdrop-blur-xl border border-slate-800 rounded-2xl overflow-hidden shadow-xl space-y-0 animate-fadeIn">
      
      {/* Executive Status Header */}
      <div className="bg-slate-950/80 px-5 py-4 border-b border-slate-800 flex flex-wrap items-center justify-between gap-4">
        
        {/* Left: Spinner + Status Description */}
        <div className="flex items-center gap-3">
          <div className="p-2 bg-sky-500/10 border border-sky-500/20 rounded-xl text-sky-400 flex items-center justify-center">
            <Loader2 className="w-4 h-4 animate-spin" />
          </div>
          <div>
            <div className="flex items-center gap-2">
              <span className="text-sm font-semibold text-slate-100">Executing Query</span>
              <span className="flex items-center gap-1 px-2 py-0.5 rounded-full bg-slate-800 text-[11px] font-mono text-slate-400 border border-slate-700/60">
                <Clock className="w-3 h-3 text-sky-400" />
                {formattedSeconds}s
              </span>
            </div>
            <p className="text-xs text-slate-400 mt-0.5">
              Generating SQL & retrieving structured records from SQL Server 2022
            </p>
          </div>
        </div>

        {/* Right: Active Query Pill */}
        {queryText && (
          <div className="hidden sm:flex items-center gap-2 max-w-md">
            <span className="text-xs text-slate-400 font-medium">Query:</span>
            <span className="truncate px-3 py-1 bg-slate-900 border border-slate-700/80 rounded-lg text-xs font-mono text-slate-300">
              {queryText}
            </span>
          </div>
        )}

      </div>

      {/* Enterprise Data Table Skeleton View */}
      <div className="overflow-x-auto">
        <table className="w-full text-left border-collapse text-xs sm:text-sm">
          
          {/* Skeleton Table Header */}
          <thead className="bg-slate-950/90 border-b border-slate-800">
            <tr>
              <th className="w-12 px-3 py-3 text-center text-slate-400 font-mono text-xs font-semibold uppercase">
                #
              </th>
              <th className="px-4 py-3 text-xs font-semibold text-slate-400 tracking-wider uppercase">
                <div className="h-3 w-20 bg-slate-800 rounded animate-pulse" />
              </th>
              <th className="px-4 py-3 text-xs font-semibold text-slate-400 tracking-wider uppercase">
                <div className="h-3 w-32 bg-slate-800 rounded animate-pulse" />
              </th>
              <th className="px-4 py-3 text-xs font-semibold text-slate-400 tracking-wider uppercase">
                <div className="h-3 w-28 bg-slate-800 rounded animate-pulse" />
              </th>
              <th className="px-4 py-3 text-xs font-semibold text-slate-400 tracking-wider uppercase">
                <div className="h-3 w-24 bg-slate-800 rounded animate-pulse" />
              </th>
              <th className="px-4 py-3 text-xs font-semibold text-slate-400 tracking-wider uppercase">
                <div className="h-3 w-20 bg-slate-800 rounded animate-pulse" />
              </th>
            </tr>
          </thead>

          {/* Skeleton Table Body Rows */}
          <tbody className="divide-y divide-slate-800/60">
            {[1, 2, 3, 4, 5, 6].map((rowIdx) => (
              <tr key={rowIdx} className="hover:bg-slate-800/20 transition-colors">
                <td className="px-3 py-3.5 text-center font-mono text-xs text-slate-400">
                  {rowIdx}
                </td>
                <td className="px-4 py-3.5">
                  <div className="h-3.5 w-16 bg-slate-800/80 rounded animate-pulse" />
                </td>
                <td className="px-4 py-3.5">
                  <div 
                    className="h-3.5 bg-slate-800/60 rounded animate-pulse" 
                    style={{ width: `${65 + (rowIdx * 7) % 30}%` }}
                  />
                </td>
                <td className="px-4 py-3.5">
                  <div 
                    className="h-3.5 bg-slate-800/70 rounded animate-pulse" 
                    style={{ width: `${50 + (rowIdx * 11) % 40}%` }}
                  />
                </td>
                <td className="px-4 py-3.5">
                  <div className="h-3.5 w-20 bg-slate-800/60 rounded animate-pulse" />
                </td>
                <td className="px-4 py-3.5">
                  <div className="h-3.5 w-16 bg-slate-800/80 rounded animate-pulse" />
                </td>
              </tr>
            ))}
          </tbody>

        </table>
      </div>

      {/* Skeleton Footer Bar */}
      <div className="bg-slate-950/80 px-5 py-3 border-t border-slate-800 flex items-center justify-between text-xs text-slate-400">
        <div className="flex items-center gap-2">
          <div className="h-3 w-24 bg-slate-800/60 rounded animate-pulse" />
        </div>
        <div className="flex items-center gap-2">
          <div className="h-3 w-32 bg-slate-800/60 rounded animate-pulse" />
        </div>
      </div>

    </div>
  );
}
