import React from 'react';
import { Database, Server, Layers, PanelRightClose, PanelRightOpen } from 'lucide-react';

export default function Navbar({ health, sidebarOpen, onToggleSidebar }) {
  const isOnline = health?.status === 'ok';

  return (
    <header className="bg-slate-900/90 backdrop-blur-md border-b border-slate-800 sticky top-0 z-50">
      <div className="w-full max-w-[1800px] mx-auto px-4 sm:px-6 lg:px-8 h-16 flex items-center justify-between">
        
        {/* Brand Logo & Name */}
        <div className="flex items-center space-x-3">
          <div className="p-2 bg-gradient-to-tr from-sky-500 to-indigo-600 rounded-xl shadow-lg shadow-sky-500/20 text-white">
            <Database className="w-5 h-5" />
          </div>
          <div>
            <h1 className="text-lg font-bold tracking-tight bg-gradient-to-r from-sky-400 via-indigo-300 to-white bg-clip-text text-transparent">
              Advanced RAG System
            </h1>
            <p className="text-xs text-slate-400 flex items-center gap-1 font-medium">
              <span className="text-sky-400 font-semibold">(SQL-based)</span> • Microsoft SQL Server 2022 + ChromaDB
            </p>
          </div>
        </div>

        {/* System Health Indicators & Sidebar Toggle */}
        <div className="flex items-center space-x-3 sm:space-x-4">
          <div className="hidden md:flex items-center gap-3 bg-slate-800/80 px-3 py-1.5 rounded-lg border border-slate-700/60 text-xs">
            <div className="flex items-center gap-1.5 text-slate-300">
              <Server className="w-3.5 h-3.5 text-sky-400" />
              <span>AdventureWorks2022</span>
            </div>
            <span className="text-slate-600">|</span>
            <div className="flex items-center gap-1.5 text-slate-300">
              <Layers className="w-3.5 h-3.5 text-emerald-400" />
              <span>Chroma Vector Store</span>
            </div>
          </div>

          {/* Online/Offline Pill */}
          <div className={`flex items-center gap-2 px-3 py-1 rounded-full text-xs font-semibold border ${
            isOnline 
              ? 'bg-emerald-500/10 text-emerald-400 border-emerald-500/30' 
              : 'bg-rose-500/10 text-rose-400 border-rose-500/30'
          }`}>
            <span className={`w-2 h-2 rounded-full ${isOnline ? 'bg-emerald-400 animate-pulse' : 'bg-rose-400'}`} />
            {isOnline ? 'API Connected' : 'API Offline'}
          </div>

          {/* Toggle Sidebar Button */}
          <button
            type="button"
            onClick={onToggleSidebar}
            title={sidebarOpen ? "Hide Sidebar (Expand Data Studio)" : "Show Sidebar"}
            className={`flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-xs font-semibold border transition-all cursor-pointer ${
              sidebarOpen
                ? 'bg-slate-800 hover:bg-slate-700 text-slate-300 border-slate-700'
                : 'bg-sky-500/20 text-sky-400 border-sky-500/40 shadow-sm shadow-sky-500/20'
            }`}
          >
            {sidebarOpen ? (
              <>
                <PanelRightClose className="w-4 h-4" />
                <span className="hidden sm:inline">Wide Mode</span>
              </>
            ) : (
              <>
                <PanelRightOpen className="w-4 h-4" />
                <span className="hidden sm:inline">Sidebar</span>
              </>
            )}
          </button>
        </div>

      </div>
    </header>
  );
}
