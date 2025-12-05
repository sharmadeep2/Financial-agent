// API Response Types
export interface StockData {
  symbol: string;
  companyName: string;
  exchange: 'NSE' | 'BSE';
  price: number;
  change: number;
  changePercent: number;
  volume: number;
  marketCap?: number;
  dayHigh: number;
  dayLow: number;
  open: number;
  previousClose: number;
  timestamp: string;
}

export interface DailyPrice {
  date: string;
  open: number;
  high: number;
  low: number;
  close: number;
  volume: number;
  adjustedClose: number;
}

export interface HistoricalStockData {
  id: string;
  symbol: string;
  exchange: string;
  fromDate: string;
  toDate: string;
  dailyPrices: DailyPrice[];
  recordCount: number;
}

export interface TechnicalIndicators {
  symbol: string;
  exchange: string;
  sma20: number;
  sma50: number;
  sma200: number;
  ema12: number;
  ema26: number;
  rsi: number;
  macd: number;
  macdSignal: number;
  macdHistogram: number;
  bollingerUpper: number;
  bollingerMiddle: number;
  bollingerLower: number;
  stochasticK: number;
  stochasticD: number;
  williamsR: number;
  atr: number;
  adx: number;
  cci: number;
  momentum: number;
  roc: number;
  timestamp: string;
}

export interface MarketAnnouncement {
  id: string;
  title: string;
  description: string;
  symbol?: string;
  category: string;
  publishDate: string;
  url?: string;
}

// UI State Types
export interface SearchResult {
  symbol: string;
  companyName: string;
  exchange: 'NSE' | 'BSE';
}

export interface WatchlistItem {
  id: string;
  symbol: string;
  companyName: string;
  exchange: 'NSE' | 'BSE';
  addedAt: string;
}

export interface ChartConfig {
  timeRange: '1D' | '1W' | '1M' | '3M' | '6M' | '1Y' | '5Y';
  chartType: 'line' | 'candlestick' | 'area';
  indicators: string[];
}

// API Error Types
export interface ApiError {
  message: string;
  statusCode: number;
  timestamp: string;
  path: string;
}

export interface ApiResponse<T> {
  data?: T;
  error?: ApiError;
  success: boolean;
}

// React Query Types
export interface UseQueryResult<T> {
  data?: T;
  isLoading: boolean;
  isError: boolean;
  error?: Error;
  refetch: () => void;
}