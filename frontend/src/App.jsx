import React, { useState, useEffect, useRef } from 'react';
import Navbar from './components/Navbar';
import QueryInput from './components/QueryInput';
import MetricsBar from './components/MetricsBar';
import DataGrid from './components/DataGrid';
import SessionBreadcrumbs from './components/SessionBreadcrumbs';
import ErrorBanner from './components/ErrorBanner';
import Sidebar from './components/Sidebar';
import ThinkingIndicator from './components/ThinkingIndicator';
import { executeQuery, executeAudioQuery, checkSystemHealth, createNewSession } from './services/api';

export default function App() {
  const [health, setHealth] = useState(null);
  const [isLoading, setIsLoading] = useState(false);
  const [activeQueryText, setActiveQueryText] = useState('');
  const [error, setError] = useState(null);
  const [pipelineResult, setPipelineResult] = useState(null);
  const [sidebarOpen, setSidebarOpen] = useState(true);

  // Multi-Turn Session State
  const [sessionId, setSessionId] = useState(null);
  const [sessionTurns, setSessionTurns] = useState([]);
  const [activeTurnIndex, setActiveTurnIndex] = useState(null);
  const [isFollowUp, setIsFollowUp] = useState(false);
  const [selectedRowContext, setSelectedRowContext] = useState(null);

  const [history, setHistory] = useState(() => {
    try {
      const saved = localStorage.getItem('adv_rag_history');
      return saved ? JSON.parse(saved) : [];
    } catch {
      return [];
    }
  });

  const gridRef = useRef(null);

  // Poll system health once on startup
  useEffect(() => {
    checkSystemHealth().then(setHealth).catch(() => setHealth({ status: 'offline' }));
  }, []);

  // Save history to localStorage
  useEffect(() => {
    try {
      localStorage.setItem('adv_rag_history', JSON.stringify(history));
    } catch (e) {
      console.warn('Unable to persist query history to localStorage', e);
    }
  }, [history]);

  const addToHistory = (queryText) => {
    setHistory((prev) => {
      const filtered = prev.filter((item) => item.query !== queryText);
      return [{ query: queryText, timestamp: new Date().toISOString() }, ...filtered].slice(0, 15);
    });
  };

  // Start fresh independent session
  const handleNewSession = async () => {
    const newSid = await createNewSession();
    setSessionId(newSid);
    setSessionTurns([]);
    setActiveTurnIndex(null);
    setIsFollowUp(false);
    setSelectedRowContext(null);
    setPipelineResult(null);
    setError(null);
    setActiveQueryText('');
  };

  // Execute text query (handles both root queries and contextual follow-ups)
  const handleTextSubmit = async (queryText, forceFollowUp = null) => {
    setIsLoading(true);
    setActiveQueryText(queryText);
    setError(null);

    const followUpMode = forceFollowUp !== null ? forceFollowUp : isFollowUp;

    try {
      const data = await executeQuery(
        queryText,
        sessionId,
        followUpMode,
        selectedRowContext
      );

      setPipelineResult(data);
      setSessionId(data.session_id);

      // Record turn into breadcrumb thread
      const newTurn = {
        turn_index: data.turn_index,
        query: queryText,
        intent: data.intent,
        generated_sql: data.generated_sql,
        total_rows: data.results?.length || 0,
        results: data.results,
        answer: data.answer,
        execution_time_ms: data.execution_time_ms
      };

      setSessionTurns((prev) => [...prev, newTurn]);
      setActiveTurnIndex(data.turn_index);

      // Auto-switch to follow-up mode for subsequent queries within this session
      setIsFollowUp(true);
      setSelectedRowContext(null); // Reset single-row target once consumed

      addToHistory(queryText);
    } catch (err) {
      setError(err.message || 'Failed to execute query');
    } finally {
      setIsLoading(false);
    }
  };

  // 1-Click Drill-Down on Table Row/Entity
  const handleDrillDown = (row, drillDownPrompt) => {
    setSelectedRowContext(row);
    setIsFollowUp(true);
    handleTextSubmit(drillDownPrompt, true);
  };

  // Jump to an earlier turn in the active thread
  const handleSelectTurn = (turn) => {
    setActiveTurnIndex(turn.turn_index);
    setPipelineResult({
      query: turn.query,
      intent: turn.intent,
      generated_sql: turn.generated_sql,
      results: turn.results,
      answer: turn.answer,
      execution_time_ms: turn.execution_time_ms,
      session_id: sessionId,
      turn_index: turn.turn_index
    });
  };

  // Voice Query Submission
  const handleAudioSubmit = async (audioBlob) => {
    setIsLoading(true);
    setActiveQueryText('Transcribing voice audio...');
    setError(null);
    try {
      const data = await executeAudioQuery(audioBlob, sessionId, isFollowUp);
      setPipelineResult(data);
      setSessionId(data.session_id);

      if (data.transcribed_text) {
        addToHistory(data.transcribed_text);
        const newTurn = {
          turn_index: data.turn_index,
          query: data.transcribed_text,
          intent: data.intent,
          generated_sql: data.generated_sql,
          total_rows: data.results?.length || 0,
          results: data.results,
          answer: data.answer,
          execution_time_ms: data.execution_time_ms
        };
        setSessionTurns((prev) => [...prev, newTurn]);
        setActiveTurnIndex(data.turn_index);
        setIsFollowUp(true);
      }
    } catch (err) {
      setError(err.message || 'Failed to process voice query');
    } finally {
      setIsLoading(false);
    }
  };

  const handleClearHistory = () => {
    setHistory([]);
    try {
      localStorage.removeItem('adv_rag_history');
    } catch { }
  };

  const handleExportCsv = () => {
    if (gridRef.current && gridRef.current.exportCsv) {
      gridRef.current.exportCsv();
    }
  };

  return (
    <div className="min-h-screen bg-slate-950 text-slate-100 flex flex-col selection:bg-sky-500/30 selection:text-white">
      {/* Top Navigation with Widescreen Layout & Sidebar Toggle */}
      <Navbar
        health={health}
        sidebarOpen={sidebarOpen}
        onToggleSidebar={() => setSidebarOpen((prev) => !prev)}
      />

      {/* Main Container - Expands to Widescreen Canvas */}
      <main className="flex-1 w-full max-w-[1800px] mx-auto px-4 sm:px-6 lg:px-8 py-5 space-y-5">

        {/* Active Multi-Turn Thread Breadcrumbs */}
        {sessionTurns.length > 0 && (
          <SessionBreadcrumbs
            turns={sessionTurns}
            activeTurnIndex={activeTurnIndex}
            onSelectTurn={handleSelectTurn}
            onNewSession={handleNewSession}
          />
        )}

        {/* Top Query & Voice Input Bar */}
        <QueryInput
          onSubmit={handleTextSubmit}
          onAudioSubmit={handleAudioSubmit}
          isLoading={isLoading}
          isFollowUp={isFollowUp}
          onToggleFollowUp={setIsFollowUp}
          selectedRowContext={selectedRowContext}
          onClearSelectedRow={() => setSelectedRowContext(null)}
          hasActiveSession={sessionTurns.length > 0}
          turnCount={sessionTurns.length + 1}
          onNewSession={handleNewSession}
        />

        {/* Error Notification Banner */}
        {error && (
          <ErrorBanner error={error} onDismiss={() => setError(null)} />
        )}

        {/* Studio Content Grid */}
        <div className="flex flex-col lg:flex-row gap-5 items-start">

          {/* Main Left/Center Tabular Studio (Spans full width when sidebar is collapsed) */}
          <div className="flex-1 w-full min-w-0 space-y-4">

            {/* Live Loading Indicator Skeleton */}
            {isLoading && (
              <ThinkingIndicator queryText={activeQueryText} />
            )}

            {/* Execution Metrics & Toolbar */}
            {!isLoading && pipelineResult && (
              <MetricsBar
                result={pipelineResult}
                onExportCsv={handleExportCsv}
              />
            )}

            {/* Primary Tabular Data Studio with Interactive Row Drill-Down */}
            {!isLoading && (
              <DataGrid
                ref={gridRef}
                rawResults={pipelineResult?.results || []}
                intent={pipelineResult?.intent}
                selectedRow={selectedRowContext}
                onSelectRow={setSelectedRowContext}
                onDrillDown={handleDrillDown}
                hasExecutedQuery={!!pipelineResult}
              />
            )}

          </div>

          {/* Right Sidebar: History & Presets (Collapsible) */}
          {sidebarOpen && (
            <Sidebar
              history={history}
              onSelectQuery={(q) => handleTextSubmit(q, false)}
              onClearHistory={handleClearHistory}
            />
          )}

        </div>

      </main>

      {/* Subtle Studio Footer */}
      <footer className="w-full border-t border-slate-900 py-3 text-center text-slate-400 text-xs bg-slate-950">
        Advanced RAG System (SQL-based) • Natural Language SQL Studio
      </footer>
    </div>
  );
}
