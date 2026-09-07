import React from 'react';
import { History, Bookmark, TrendingUp, Package, ShoppingCart, Search, Trash2 } from 'lucide-react';

const CATEGORIES = [
  {
    title: 'Sales & Revenue',
    icon: TrendingUp,
    color: 'text-sky-400',
    queries: [
      'What are the top 5 most popular accessories?',
      'What is the total sales revenue for all orders placed in the year 2013?',
      'Find the top 10 distinct items most purchased by customers ordered by quantity sold',
    ],
  },
  {
    title: 'Inventory & Catalog',
    icon: Package,
    color: 'text-amber-400',
    queries: [
      'List the top 5 most expensive products by list price, showing name and price',
      'What is the total quantity of products currently in stock across all inventory locations?',
      'Show products in the Mountain Bikes category with their standard cost and list price',
    ],
  },
  {
    title: 'Semantic Vector Search',
    icon: Search,
    color: 'text-emerald-400',
    queries: [
      'What products or bikes are good for long distance touring and travel?',
      'Tell me about lightweight helmets with good ventilation',
      'Describe socks or apparel that stay dry during sports',
    ],
  },
];

export default function Sidebar({ history, onSelectQuery, onClearHistory }) {
  return (
    <aside className="w-full lg:w-80 flex-shrink-0 space-y-6">

      {/* Recent Query History */}
      <div className="bg-slate-900/60 backdrop-blur-xl border border-slate-800 rounded-2xl p-4 shadow-xl">
        <div className="flex items-center justify-between pb-3 border-b border-slate-800/80 mb-3">
          <div className="flex items-center gap-2 text-xs font-semibold text-slate-200">
            <History className="w-4 h-4 text-sky-400" />
            <span>Recent Query History</span>
          </div>
          {history.length > 0 && (
            <button
              type="button"
              onClick={onClearHistory}
              title="Clear history"
              className="text-slate-500 hover:text-rose-400 transition-colors p-1"
            >
              <Trash2 className="w-3.5 h-3.5" />
            </button>
          )}
        </div>

        {history.length === 0 ? (
          <p className="text-xs text-slate-500 italic py-2 text-center">No queries executed yet.</p>
        ) : (
          <ul className="space-y-1.5 max-h-52 overflow-y-auto pr-1">
            {history.map((item, idx) => (
              <li key={idx}>
                <button
                  type="button"
                  onClick={() => onSelectQuery(item.query)}
                  className="w-full text-left p-2 rounded-lg bg-slate-950/50 hover:bg-slate-800 border border-slate-800/50 hover:border-sky-500/30 text-xs text-slate-300 hover:text-white transition-all duration-150 truncate block"
                >
                  <span className="font-mono text-slate-500 mr-1.5">›</span>
                  {item.query}
                </button>
              </li>
            ))}
          </ul>
        )}
      </div>

      {/* Preset Query Collections */}
      <div className="bg-slate-900/60 backdrop-blur-xl border border-slate-800 rounded-2xl p-4 shadow-xl space-y-4">
        <div className="flex items-center gap-2 pb-2 border-b border-slate-800/80 text-xs font-semibold text-slate-200">
          <Bookmark className="w-4 h-4 text-indigo-400" />
          <span>Preset Query Library</span>
        </div>

        {CATEGORIES.map((cat, idx) => {
          const Icon = cat.icon;
          return (
            <div key={idx} className="space-y-2">
              <div className="flex items-center gap-1.5 text-xs font-semibold text-slate-400">
                <Icon className={`w-3.5 h-3.5 ${cat.color}`} />
                <span>{cat.title}</span>
              </div>
              <div className="space-y-1">
                {cat.queries.map((q, qIdx) => (
                  <button
                    key={qIdx}
                    type="button"
                    onClick={() => onSelectQuery(q)}
                    className="w-full text-left px-2.5 py-1.5 rounded-lg bg-slate-950/40 hover:bg-slate-800/80 border border-slate-800/60 hover:border-slate-700 text-xs text-slate-400 hover:text-slate-200 transition-all truncate block"
                  >
                    {q}
                  </button>
                ))}
              </div>
            </div>
          );
        })}
      </div>

      {/* System Metadata Card */}
      <div className="bg-gradient-to-br from-slate-900/90 to-indigo-950/40 border border-slate-800 rounded-2xl p-4 text-xs text-slate-400 space-y-2 shadow-xl">
        <div className="font-semibold text-slate-300">System Architecture</div>
        <div className="space-y-1 text-[11px] leading-relaxed">
          <div>• <strong className="text-slate-300">Database:</strong> Microsoft SQL Server 2022</div>
          <div>• <strong className="text-slate-300">LLM Tier:</strong> Groq (GPT-OSS-120B) with Circuit Breaker</div>
          <div>• <strong className="text-slate-300">Vector Store:</strong> ChromaDB (294 vectors)</div>
          <div>• <strong className="text-slate-300">Voice:</strong> Faster-Whisper (int8 CPU)</div>
        </div>
      </div>

    </aside>
  );
}
