import React, { useState } from 'react';
import { Search, X } from 'lucide-react';
import { Input, Button } from '@/components/ui';
import { useQuery } from '@tanstack/react-query';
import { marketDataService } from '@/services/marketData';
import type { SearchResult } from '@/types';

interface StockSearchProps {
  onSelect: (result: SearchResult) => void;
  placeholder?: string;
  className?: string;
}

export const StockSearch: React.FC<StockSearchProps> = ({
  onSelect,
  placeholder = "Search for stocks (e.g., RELIANCE, INFY, TCS...)",
  className = "",
}) => {
  const [query, setQuery] = useState('');
  const [isOpen, setIsOpen] = useState(false);
  const [selectedExchange, setSelectedExchange] = useState<'NSE' | 'BSE'>('NSE');

  const { data: searchResults = [], isLoading, error } = useQuery({
    queryKey: ['stockSearch', query, selectedExchange],
    queryFn: () => 
      selectedExchange === 'NSE' 
        ? marketDataService.searchNseSymbols(query)
        : marketDataService.searchBseSymbols(query),
    enabled: query.length >= 2,
    staleTime: 5 * 60 * 1000, // 5 minutes
  });

  const handleInputChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const value = e.target.value;
    setQuery(value);
    setIsOpen(value.length >= 2);
  };

  const handleSelect = (result: SearchResult) => {
    onSelect(result);
    setQuery(result.symbol);
    setIsOpen(false);
  };

  const clearSearch = () => {
    setQuery('');
    setIsOpen(false);
  };

  return (
    <div className={`relative ${className}`}>
      {/* Exchange Selector */}
      <div className="flex mb-2">
        <Button
          variant={selectedExchange === 'NSE' ? 'primary' : 'secondary'}
          size="sm"
          onClick={() => setSelectedExchange('NSE')}
          className="mr-2"
        >
          NSE
        </Button>
        <Button
          variant={selectedExchange === 'BSE' ? 'primary' : 'secondary'}
          size="sm"
          onClick={() => setSelectedExchange('BSE')}
        >
          BSE
        </Button>
      </div>

      {/* Search Input */}
      <div className="relative">
        <div className="absolute inset-y-0 left-0 pl-3 flex items-center pointer-events-none">
          <Search className="h-5 w-5 text-gray-400" />
        </div>
        
        <Input
          type="text"
          value={query}
          onChange={handleInputChange}
          placeholder={placeholder}
          className="pl-10 pr-10"
        />
        
        {query && (
          <button
            type="button"
            onClick={clearSearch}
            className="absolute inset-y-0 right-0 pr-3 flex items-center"
          >
            <X className="h-5 w-5 text-gray-400 hover:text-gray-600" />
          </button>
        )}
      </div>

      {/* Search Results Dropdown */}
      {isOpen && (
        <div className="absolute z-50 mt-1 w-full bg-white shadow-lg max-h-60 rounded-md py-1 text-base ring-1 ring-black ring-opacity-5 overflow-auto focus:outline-none">
          {isLoading ? (
            <div className="px-4 py-2 text-sm text-gray-500">
              <div className="flex items-center">
                <div className="animate-spin mr-2 h-4 w-4 border-2 border-primary-500 border-t-transparent rounded-full"></div>
                Searching...
              </div>
            </div>
          ) : error ? (
            <div className="px-4 py-2 text-sm text-danger-600">
              Failed to search. Please try again.
            </div>
          ) : searchResults.length === 0 ? (
            <div className="px-4 py-2 text-sm text-gray-500">
              No stocks found for "{query}"
            </div>
          ) : (
            searchResults.map((result, index) => (
              <button
                key={`${result.symbol}-${result.exchange}-${index}`}
                type="button"
                onClick={() => handleSelect(result)}
                className="w-full text-left px-4 py-2 hover:bg-gray-100 focus:bg-gray-100 focus:outline-none transition-colors"
              >
                <div className="flex items-center justify-between">
                  <div>
                    <div className="font-medium text-gray-900">{result.symbol}</div>
                    <div className="text-sm text-gray-600 truncate">{result.companyName}</div>
                  </div>
                  <span className="inline-flex items-center px-2 py-1 rounded-full text-xs font-medium bg-primary-100 text-primary-700">
                    {result.exchange}
                  </span>
                </div>
              </button>
            ))
          )}
        </div>
      )}

      {/* Popular Symbols */}
      {!query && (
        <div className="mt-4">
          <h4 className="text-sm font-medium text-gray-700 mb-2">Popular Stocks</h4>
          <div className="flex flex-wrap gap-2">
            {[
              { symbol: 'RELIANCE', name: 'Reliance Industries', exchange: 'NSE' as const },
              { symbol: 'TCS', name: 'Tata Consultancy Services', exchange: 'NSE' as const },
              { symbol: 'INFY', name: 'Infosys Limited', exchange: 'NSE' as const },
              { symbol: 'HDFCBANK', name: 'HDFC Bank', exchange: 'NSE' as const },
              { symbol: 'ICICIBANK', name: 'ICICI Bank', exchange: 'NSE' as const },
              { symbol: 'SBIN', name: 'State Bank of India', exchange: 'NSE' as const },
            ].map((stock) => (
              <button
                key={stock.symbol}
                type="button"
                onClick={() => handleSelect({
                  symbol: stock.symbol,
                  companyName: stock.name,
                  exchange: stock.exchange,
                })}
                className="inline-flex items-center px-3 py-1 rounded-full text-sm font-medium bg-gray-100 text-gray-700 hover:bg-gray-200 transition-colors"
              >
                {stock.symbol}
              </button>
            ))}
          </div>
        </div>
      )}
    </div>
  );
};