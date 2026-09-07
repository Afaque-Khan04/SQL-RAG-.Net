import React, { useState, useMemo, forwardRef, useImperativeHandle } from 'react';
import { 
  ArrowUpDown, 
  ChevronUp, 
  ChevronDown, 
  Search, 
  FileSpreadsheet, 
  Inbox, 
  ChevronLeft, 
  ChevronRight,
  Database,
  SlidersHorizontal,
  MoveHorizontal,
  Target,
  Sparkles,
  Layers,
  ShoppingBag,
  PackageCheck
} from 'lucide-react';

/**
 * Format column header names cleanly (e.g. 'TotalQuantitySold' -> 'Total Quantity Sold')
 */
function formatHeaderName(fieldName) {
  const map = {
    productid: 'Product ID',
    salesorderid: 'Sales Order ID',
    customerid: 'Customer ID',
    salespersonid: 'Sales Person ID',
    orderqty: 'Order Qty',
    totaldue: 'Total Due',
    listprice: 'List Price',
    standardcost: 'Standard Cost',
    totalquantitysold: 'Total Quantity Sold',
    salesytd: 'Sales YTD',
    orderdate: 'Order Date',
    salesordernumber: 'Sales Order Number',
    customername: 'Customer Name',
    salespersonname: 'Sales Person Name',
    territoryname: 'Territory Name',
    productname: 'Product Name',
  };

  const lower = fieldName.toLowerCase();
  if (map[lower]) return map[lower];

  return fieldName
    .replace(/([a-z])([A-Z])/g, '$1 $2')
    .replace(/_/g, ' ')
    .replace(/^./, (str) => str.toUpperCase())
    .trim();
}

/**
 * Render formatted cell value based on column name & type
 */
function renderCellValue(fieldName, value) {
  if (value == null || value === '') {
    return <span className="text-slate-600 italic">-</span>;
  }

  const name = fieldName.toLowerCase();

  // 1. Currency Fields -> Glowing emerald mono text
  if (/price|revenue|totaldue|subtotal|linetotal|cost|spend|amount|salesytd/i.test(name)) {
    const num = Number(value);
    if (!isNaN(num)) {
      return (
        <span className="font-mono font-semibold text-emerald-400">
          {new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' }).format(num)}
        </span>
      );
    }
  }

  // 2. Quantities & Counts -> Cyan mono text with commas
  if (/qty|quantity|count|stock/i.test(name)) {
    const num = Number(value);
    if (!isNaN(num)) {
      return (
        <span className="font-mono font-medium text-sky-300">
          {new Intl.NumberFormat('en-US').format(num)}
        </span>
      );
    }
  }

  // 3. IDs and Identifiers -> Subtle pill badge
  if (/id|number|code|key/i.test(name)) {
    return (
      <span className="inline-block px-2 py-0.5 bg-slate-800/90 border border-slate-700/60 rounded text-xs font-mono text-slate-300 shadow-sm">
        {String(value)}
      </span>
    );
  }

  // 4. Vector Distance / Similarity Score
  if (/distance|score|similarity/i.test(name)) {
    const num = Number(value);
    if (!isNaN(num)) {
      return (
        <span className="font-mono text-xs text-amber-400 bg-amber-400/10 px-2 py-0.5 rounded border border-amber-400/20">
          Score: {num.toFixed(4)}
        </span>
      );
    }
  }

  // 5. Dates -> Formatted local date
  if (/date|time/i.test(name)) {
    try {
      const d = new Date(value);
      if (!isNaN(d.getTime())) {
        return <span className="font-mono text-slate-300">{d.toLocaleDateString()}</span>;
      }
    } catch {}
  }

  // 6. Default text
  return <span className="text-slate-200">{String(value)}</span>;
}

const DataGrid = forwardRef(({ 
  rawResults, 
  intent, 
  selectedRow, 
  onSelectRow, 
  onDrillDown,
  hasExecutedQuery = false
}, ref) => {
  const [searchTerm, setSearchTerm] = useState('');
  const [sortConfig, setSortConfig] = useState({ key: null, direction: 'asc' });
  const [currentPage, setCurrentPage] = useState(1);
  const [pageSize, setPageSize] = useState(15);
  const [isCompact, setIsCompact] = useState(false);

  // Normalize row data (flatten semantic metadata if vector search)
  const rows = useMemo(() => {
    if (!rawResults || !Array.isArray(rawResults) || rawResults.length === 0) {
      return [];
    }

    const isStructured = String(intent || '').toLowerCase() === 'structured';
    if (!isStructured && rawResults[0]?.metadata) {
      return rawResults.map((item) => ({
        product_name: item.metadata?.product_name || item.product_name || 'Product',
        category: item.metadata?.category || item.category || '-',
        price: item.metadata?.price != null ? item.metadata.price : item.price,
        similarity_score: item.score != null ? item.score : item.distance,
        description: item.document || item.metadata?.description || item.description || '',
      }));
    }

    return rawResults;
  }, [rawResults, intent]);

  // Extract column keys
  const columns = useMemo(() => {
    if (rows.length === 0) return [];
    return Object.keys(rows[0]);
  }, [rows]);

  // Filtered rows
  const filteredRows = useMemo(() => {
    if (!searchTerm.trim()) return rows;
    const term = searchTerm.toLowerCase();
    return rows.filter((row) =>
      columns.some((col) => {
        const val = row[col];
        return val != null && String(val).toLowerCase().includes(term);
      })
    );
  }, [rows, columns, searchTerm]);

  // Sorted rows
  const sortedRows = useMemo(() => {
    if (!sortConfig.key) return filteredRows;

    return [...filteredRows].sort((a, b) => {
      const aVal = a[sortConfig.key];
      const bVal = b[sortConfig.key];

      if (aVal == null) return 1;
      if (bVal == null) return -1;

      // Numeric sort
      if (typeof aVal === 'number' && typeof bVal === 'number') {
        return sortConfig.direction === 'asc' ? aVal - bVal : bVal - aVal;
      }

      // String sort
      const aStr = String(aVal).toLowerCase();
      const bStr = String(bVal).toLowerCase();
      if (aStr < bStr) return sortConfig.direction === 'asc' ? -1 : 1;
      if (aStr > bStr) return sortConfig.direction === 'asc' ? 1 : -1;
      return 0;
    });
  }, [filteredRows, sortConfig]);

  // Paginated rows
  const totalPages = Math.ceil(sortedRows.length / pageSize) || 1;
  const paginatedRows = useMemo(() => {
    const start = (currentPage - 1) * pageSize;
    return sortedRows.slice(start, start + pageSize);
  }, [sortedRows, currentPage, pageSize]);

  // Export CSV handler
  const handleExportCsv = () => {
    if (rows.length === 0) return;
    const headers = columns;
    const csvContent = [
      headers.join(','),
      ...rows.map((row) =>
        headers
          .map((field) => {
            const val = row[field] ?? '';
            const clean = String(val).replace(/"/g, '""');
            return `"${clean}"`;
          })
          .join(',')
      ),
    ].join('\n');

    const blob = new Blob([csvContent], { type: 'text/csv;charset=utf-8;' });
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.setAttribute('href', url);
    link.setAttribute('download', `sql_studio_export_${new Date().toISOString().slice(0, 10)}.csv`);
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    URL.revokeObjectURL(url);
  };

  useImperativeHandle(ref, () => ({
    exportCsv: handleExportCsv,
  }));

  const handleSort = (key) => {
    setSortConfig((prev) => {
      if (prev.key === key) {
        return {
          key,
          direction: prev.direction === 'asc' ? 'desc' : 'asc',
        };
      }
      return { key, direction: 'desc' }; // Default descending for analytical queries
    });
  };

  if (rows.length === 0) {
    if (hasExecutedQuery) {
      return (
        <div className="w-full bg-slate-900/40 backdrop-blur-xl border border-slate-800/80 rounded-2xl p-10 flex flex-col items-center justify-center text-center shadow-xl animate-fadeIn">
          <div className="p-3 bg-amber-500/10 border border-amber-500/20 rounded-xl text-amber-400 mb-3 shadow-inner">
            <Inbox className="w-7 h-7" />
          </div>
          <h3 className="text-sm font-semibold text-slate-200">No Records Found</h3>
          <p className="text-xs text-slate-400 mt-1 max-w-md leading-relaxed">
            The query executed successfully, but 0 records matched the specified filter criteria in the database.
          </p>
        </div>
      );
    }

    return (
      <div className="w-full bg-slate-900/40 backdrop-blur-xl border border-slate-800/80 rounded-2xl p-14 flex flex-col items-center justify-center text-center shadow-xl">
        <div className="p-4 bg-slate-800/70 border border-slate-700/50 rounded-2xl text-slate-400 mb-4 shadow-inner">
          <Database className="w-8 h-8 text-sky-400" />
        </div>
        <h3 className="text-base font-semibold text-slate-200">Database Query Studio Ready</h3>
        <p className="text-xs text-slate-400 mt-1.5 max-w-md leading-relaxed">
          Select any preset query or type a question above to instantly retrieve and explore SQL records in this high-performance data table.
        </p>
      </div>
    );
  }

  const hasManyColumns = columns.length > 5;

  return (
    <div className="w-full bg-slate-900/95 backdrop-blur-xl border border-slate-800 rounded-2xl overflow-hidden shadow-2xl shadow-black/50 transition-all flex flex-col space-y-0">
      
      {/* 1-Click Interactive Row Drill-Down Action Bar */}
      {selectedRow && onDrillDown && (
        <div className="bg-gradient-to-r from-sky-950/80 via-slate-900 to-indigo-950/80 border-b border-sky-500/30 px-4 py-2.5 flex flex-wrap items-center justify-between gap-3 text-xs animate-fadeIn">
          <div className="flex items-center gap-2 text-sky-200">
            <Target className="w-4 h-4 text-sky-400" />
            <span>
              <strong>Selected Entity:</strong> {selectedRow.ProductName || selectedRow.product_name || `Row ID ${selectedRow.ProductID || selectedRow.SalesOrderID || ''}`}
            </span>
          </div>

          <div className="flex items-center gap-2">
            {(selectedRow.ProductID || selectedRow.product_id) && (
              <button
                type="button"
                onClick={() => onDrillDown(selectedRow, `Drill down on inventory stock levels for product ${selectedRow.ProductID || selectedRow.product_id}`)}
                className="flex items-center gap-1 px-2.5 py-1 bg-sky-600/30 hover:bg-sky-600/50 border border-sky-500/40 text-sky-200 rounded-lg font-medium transition-all"
              >
                <PackageCheck className="w-3.5 h-3.5" />
                <span>Drill Down Inventory</span>
              </button>
            )}

            {(selectedRow.ProductID || selectedRow.product_id) && (
              <button
                type="button"
                onClick={() => onDrillDown(selectedRow, `Show purchasing vendor orders for product ${selectedRow.ProductID || selectedRow.product_id}`)}
                className="flex items-center gap-1 px-2.5 py-1 bg-indigo-600/30 hover:bg-indigo-600/50 border border-indigo-500/40 text-indigo-200 rounded-lg font-medium transition-all"
              >
                <ShoppingBag className="w-3.5 h-3.5" />
                <span>Drill Down Purchasing</span>
              </button>
            )}

            {(selectedRow.CustomerID || selectedRow.customer_id) && (
              <button
                type="button"
                onClick={() => onDrillDown(selectedRow, `Show customer order history for customer ${selectedRow.CustomerID || selectedRow.customer_id}`)}
                className="flex items-center gap-1 px-2.5 py-1 bg-emerald-600/30 hover:bg-emerald-600/50 border border-emerald-500/40 text-emerald-200 rounded-lg font-medium transition-all"
              >
                <Layers className="w-3.5 h-3.5" />
                <span>Customer History</span>
              </button>
            )}
          </div>
        </div>
      )}

      {/* Table Toolbar Header */}
      <div className="bg-slate-950/90 px-4 py-3 border-b border-slate-800 flex flex-wrap items-center justify-between gap-3 sticky top-0 z-30">
        
        {/* Left: Row Counts & Scroll Helper */}
        <div className="flex items-center gap-3">
          <span className="text-xs font-semibold text-slate-300 tracking-wide uppercase">
            Data Studio Table
          </span>
          <span className="px-2.5 py-0.5 bg-sky-500/15 text-sky-400 border border-sky-500/30 rounded-full text-xs font-mono font-medium">
            {sortedRows.length.toLocaleString()} {sortedRows.length === 1 ? 'row' : 'rows'}
          </span>
          {hasManyColumns && (
            <span className="hidden md:flex items-center gap-1 text-[11px] text-slate-400 bg-slate-800/80 px-2 py-0.5 rounded border border-slate-700/60">
              <MoveHorizontal className="w-3 h-3 text-sky-400" />
              <span>Scroll horizontally for all {columns.length} columns</span>
            </span>
          )}
        </div>

        {/* Right: Search Filter, Density, and CSV Export */}
        <div className="flex items-center gap-2 sm:gap-3">
          
          {/* Search within active table */}
          <div className="relative">
            <Search className="w-3.5 h-3.5 absolute left-3 top-1/2 -translate-y-1/2 text-slate-400 pointer-events-none" />
            <input
              type="text"
              value={searchTerm}
              onChange={(e) => {
                setSearchTerm(e.target.value);
                setCurrentPage(1);
              }}
              placeholder="Search table..."
              className="pl-8 pr-3 py-1.5 bg-slate-900 border border-slate-700/80 focus:border-sky-500 rounded-lg text-xs text-slate-200 placeholder-slate-500 outline-none transition-all w-36 sm:w-52"
            />
          </div>

          {/* Density Toggle */}
          <button
            type="button"
            onClick={() => setIsCompact((prev) => !prev)}
            title={isCompact ? "Switch to Comfortable View" : "Switch to Compact View"}
            className={`p-1.5 rounded-lg border text-xs transition-colors cursor-pointer ${
              isCompact 
                ? 'bg-sky-500/20 text-sky-400 border-sky-500/40' 
                : 'bg-slate-850 hover:bg-slate-800 text-slate-400 hover:text-slate-200 border-slate-700'
            }`}
          >
            <SlidersHorizontal className="w-3.5 h-3.5" />
          </button>

          {/* CSV Export Button */}
          <button
            type="button"
            onClick={handleExportCsv}
            className="flex items-center gap-1.5 px-3 py-1.5 bg-emerald-600 hover:bg-emerald-500 text-white rounded-lg text-xs font-semibold transition-all shadow-sm shadow-emerald-600/20 cursor-pointer"
          >
            <FileSpreadsheet className="w-3.5 h-3.5" />
            <span>CSV</span>
          </button>

        </div>
      </div>

      {/* Main Responsive & Scrollable Table Container */}
      <div className="overflow-x-auto max-h-[580px] overflow-y-auto">
        <table className="w-full text-left border-collapse text-xs sm:text-sm">
          
          {/* Sticky Header */}
          <thead className="sticky top-0 z-20 bg-slate-950/95 backdrop-blur-md shadow-sm border-b border-slate-800">
            <tr>
              {/* Sticky Row Index Column */}
              <th className="w-12 px-3 py-3 text-center text-slate-500 font-mono text-xs font-semibold uppercase sticky left-0 z-20 bg-slate-950/95 border-r border-slate-800/80">
                #
              </th>
              {columns.map((col) => {
                const isSorted = sortConfig.key === col;
                return (
                  <th
                    key={col}
                    onClick={() => handleSort(col)}
                    className="px-4 py-3 font-semibold text-slate-400 text-xs tracking-wider uppercase hover:text-sky-400 cursor-pointer select-none transition-colors group whitespace-nowrap"
                  >
                    <div className="flex items-center gap-1.5">
                      <span>{formatHeaderName(col)}</span>
                      {isSorted ? (
                        sortConfig.direction === 'asc' ? (
                          <ChevronUp className="w-3.5 h-3.5 text-sky-400" />
                        ) : (
                          <ChevronDown className="w-3.5 h-3.5 text-sky-400" />
                        )
                      ) : (
                        <ArrowUpDown className="w-3 h-3 opacity-0 group-hover:opacity-60 transition-opacity text-slate-500" />
                      )}
                    </div>
                  </th>
                );
              })}
            </tr>
          </thead>

          {/* Table Body */}
          <tbody className="divide-y divide-slate-800/60">
            {paginatedRows.map((row, rowIdx) => {
              const globalIndex = (currentPage - 1) * pageSize + rowIdx + 1;
              const paddingClass = isCompact ? 'py-1.5' : 'py-3';
              const isRowSelected = selectedRow === row;

              return (
                <tr
                  key={rowIdx}
                  onClick={() => onSelectRow && onSelectRow(isRowSelected ? null : row)}
                  className={`transition-colors duration-100 cursor-pointer group ${
                    isRowSelected
                      ? 'bg-sky-500/20 border-l-2 border-sky-400'
                      : 'hover:bg-sky-500/[0.08]'
                  }`}
                >
                  {/* Sticky Index Column */}
                  <td className={`px-3 ${paddingClass} text-center font-mono text-xs ${isRowSelected ? 'text-sky-300 font-bold' : 'text-slate-500'} group-hover:text-sky-400 sticky left-0 z-10 bg-slate-950/95 border-r border-slate-800/80`}>
                    {globalIndex}
                  </td>
                  {columns.map((col) => (
                    <td
                      key={col}
                      className={`px-4 ${paddingClass} leading-relaxed whitespace-nowrap`}
                    >
                      {renderCellValue(col, row[col])}
                    </td>
                  ))}
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>

      {/* Pagination Footer */}
      <div className="bg-slate-950/90 px-4 py-3 border-t border-slate-800 flex flex-wrap items-center justify-between gap-3 text-xs text-slate-400 sticky bottom-0 z-20">
        
        {/* Page Size Selector */}
        <div className="flex items-center gap-2">
          <span>Rows per page:</span>
          <select
            value={pageSize}
            onChange={(e) => {
              setPageSize(Number(e.target.value));
              setCurrentPage(1);
            }}
            className="bg-slate-900 border border-slate-700 rounded px-2.5 py-1 text-slate-200 outline-none"
          >
            <option value={10}>10</option>
            <option value={15}>15</option>
            <option value={25}>25</option>
            <option value={50}>50</option>
            <option value={100}>100</option>
          </select>
        </div>

        {/* Page Navigation */}
        <div className="flex items-center gap-3">
          <span>
            Showing <strong className="text-slate-200">{((currentPage - 1) * pageSize + 1).toLocaleString()}</strong> to <strong className="text-slate-200">{Math.min(currentPage * pageSize, sortedRows.length).toLocaleString()}</strong> of <strong className="text-slate-200">{sortedRows.length.toLocaleString()}</strong>
          </span>
          <div className="flex items-center gap-1">
            <button
              type="button"
              onClick={() => setCurrentPage((p) => Math.max(p - 1, 1))}
              disabled={currentPage === 1}
              className="px-2 py-1 rounded bg-slate-800 hover:bg-slate-700 disabled:opacity-40 disabled:cursor-not-allowed text-slate-300 flex items-center gap-1"
            >
              <ChevronLeft className="w-3.5 h-3.5" />
              <span>Prev</span>
            </button>
            <span className="px-2 font-mono text-slate-300 font-semibold">
              {currentPage} / {totalPages}
            </span>
            <button
              type="button"
              onClick={() => setCurrentPage((p) => Math.min(p + 1, totalPages))}
              disabled={currentPage === totalPages}
              className="px-2 py-1 rounded bg-slate-800 hover:bg-slate-700 disabled:opacity-40 disabled:cursor-not-allowed text-slate-300 flex items-center gap-1"
            >
              <span>Next</span>
              <ChevronRight className="w-3.5 h-3.5" />
            </button>
          </div>
        </div>

      </div>

    </div>
  );
});

export default DataGrid;
