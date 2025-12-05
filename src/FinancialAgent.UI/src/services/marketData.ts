import { api } from './api';
import type { 
  StockData, 
  HistoricalStockData, 
  TechnicalIndicators, 
  SearchResult, 
  MarketAnnouncement 
} from '@/types';

export const marketDataService = {
  // NSE Services
  getNseStockPrice: (symbol: string): Promise<StockData> =>
    api.get(`/marketdata/nse/${symbol}`),

  getNseHistoricalData: (
    symbol: string, 
    fromDate: string, 
    toDate: string
  ): Promise<HistoricalStockData> =>
    api.get(`/marketdata/nse/${symbol}/historical`, { 
      fromDate, 
      toDate 
    }),

  searchNseSymbols: (query: string): Promise<SearchResult[]> =>
    api.get(`/marketdata/nse/search`, { query }),

  getNseIndices: (): Promise<StockData[]> =>
    api.get('/marketdata/nse/indices'),

  getNseTopMovers: (): Promise<{ gainers: StockData[], losers: StockData[] }> =>
    api.get('/marketdata/nse/top-movers'),

  // BSE Services
  getBseStockPrice: (scripCode: string): Promise<StockData> =>
    api.get(`/marketdata/bse/${scripCode}`),

  getBseHistoricalData: (
    scripCode: string, 
    fromDate: string, 
    toDate: string
  ): Promise<HistoricalStockData> =>
    api.get(`/marketdata/bse/${scripCode}/historical`, { 
      fromDate, 
      toDate 
    }),

  searchBseSymbols: (query: string): Promise<SearchResult[]> =>
    api.get(`/marketdata/bse/search`, { query }),

  getBseIndices: (): Promise<StockData[]> =>
    api.get('/marketdata/bse/indices'),

  getBseAnnouncements: (symbol?: string): Promise<MarketAnnouncement[]> =>
    api.get('/marketdata/bse/announcements', symbol ? { symbol } : {}),

  // Technical Analysis Services
  getTechnicalIndicators: (symbol: string, exchange: 'NSE' | 'BSE'): Promise<TechnicalIndicators> =>
    api.get(`/marketdata/technical/${exchange.toLowerCase()}/${symbol}`),

  // Market Status
  getMarketStatus: (): Promise<{ isOpen: boolean, nextOpen?: string, lastClose?: string }> =>
    api.get('/marketdata/status'),

  // Health Check
  healthCheck: (): Promise<{ status: string, timestamp: string }> =>
    api.get('/health'),
};

export const watchlistService = {
  getWatchlist: (): Promise<any[]> => {
    const watchlist = localStorage.getItem('watchlist');
    return Promise.resolve(watchlist ? JSON.parse(watchlist) : []);
  },

  addToWatchlist: (item: { symbol: string, companyName: string, exchange: 'NSE' | 'BSE' }): Promise<void> => {
    const watchlist = JSON.parse(localStorage.getItem('watchlist') || '[]');
    const newItem = {
      id: Date.now().toString(),
      ...item,
      addedAt: new Date().toISOString(),
    };
    watchlist.push(newItem);
    localStorage.setItem('watchlist', JSON.stringify(watchlist));
    return Promise.resolve();
  },

  removeFromWatchlist: (id: string): Promise<void> => {
    const watchlist = JSON.parse(localStorage.getItem('watchlist') || '[]');
    const filtered = watchlist.filter((item: any) => item.id !== id);
    localStorage.setItem('watchlist', JSON.stringify(filtered));
    return Promise.resolve();
  },
};