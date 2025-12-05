import React, { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { TrendingUp, TrendingDown, Activity, Search, Star } from 'lucide-react';
import { 
  StockCard, 
  StockSearch,
  PriceChart
} from '@/components';
import { Card, CardHeader, CardContent, Button } from '@/components/ui';
import { marketDataService, watchlistService } from '@/services/marketData';
import type { StockData, SearchResult, HistoricalStockData } from '@/types';

export const Dashboard: React.FC = () => {
  const [selectedStock, setSelectedStock] = useState<StockData | null>(null);
  const [watchlist, setWatchlist] = useState<any[]>([]);

  // Fetch NSE indices
  const { data: nseIndices = [], isLoading: loadingNSE } = useQuery({
    queryKey: ['nseIndices'],
    queryFn: marketDataService.getNseIndices,
    refetchInterval: 30000, // Refresh every 30 seconds during market hours
  });

  // Fetch BSE indices
  const { data: bseIndices = [], isLoading: loadingBSE } = useQuery({
    queryKey: ['bseIndices'],
    queryFn: marketDataService.getBseIndices,
    refetchInterval: 30000,
  });

  // Fetch top movers
  const { data: topMovers } = useQuery({
    queryKey: ['topMovers'],
    queryFn: marketDataService.getNseTopMovers,
    refetchInterval: 60000, // Refresh every minute
  });

  // Fetch market status
  const { data: marketStatus } = useQuery({
    queryKey: ['marketStatus'],
    queryFn: marketDataService.getMarketStatus,
    refetchInterval: 60000,
  });

  // Fetch historical data for selected stock
  const { data: historicalData, isLoading: loadingHistory } = useQuery({
    queryKey: ['historicalData', selectedStock?.symbol, selectedStock?.exchange],
    queryFn: () => {
      if (!selectedStock) return null;
      
      const toDate = new Date();
      const fromDate = new Date();
      fromDate.setMonth(fromDate.getMonth() - 12); // Last 12 months
      
      return selectedStock.exchange === 'NSE'
        ? marketDataService.getNseHistoricalData(
            selectedStock.symbol,
            fromDate.toISOString().split('T')[0],
            toDate.toISOString().split('T')[0]
          )
        : marketDataService.getBseHistoricalData(
            selectedStock.symbol,
            fromDate.toISOString().split('T')[0],
            toDate.toISOString().split('T')[0]
          );
    },
    enabled: !!selectedStock,
  });

  const handleStockSearch = (result: SearchResult) => {
    // Fetch the selected stock data
    const fetchStock = result.exchange === 'NSE' 
      ? marketDataService.getNseStockPrice(result.symbol)
      : marketDataService.getBseStockPrice(result.symbol);

    fetchStock.then(setSelectedStock).catch(console.error);
  };

  const handleAddToWatchlist = async (stock: StockData) => {
    try {
      await watchlistService.addToWatchlist({
        symbol: stock.symbol,
        companyName: stock.companyName,
        exchange: stock.exchange,
      });
      // Refresh watchlist
      const updatedWatchlist = await watchlistService.getWatchlist();
      setWatchlist(updatedWatchlist);
    } catch (error) {
      console.error('Failed to add to watchlist:', error);
    }
  };

  const formatMarketStatus = () => {
    if (!marketStatus) return 'Loading...';
    return marketStatus.isOpen ? 'Market Open' : 'Market Closed';
  };

  return (
    <div className="min-h-screen bg-gray-50">
      {/* Header */}
      <header className="bg-white shadow-sm border-b border-gray-200">
        <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
          <div className="flex items-center justify-between h-16">
            <div className="flex items-center">
              <div className="flex-shrink-0">
                <h1 className="text-2xl font-bold text-gradient">Financial Agent</h1>
              </div>
              <div className="ml-6">
                <span className={`inline-flex items-center px-3 py-1 rounded-full text-sm font-medium ${
                  marketStatus?.isOpen 
                    ? 'bg-success-100 text-success-700' 
                    : 'bg-gray-100 text-gray-700'
                }`}>
                  <Activity className="w-4 h-4 mr-2" />
                  {formatMarketStatus()}
                </span>
              </div>
            </div>
            
            <div className="flex items-center space-x-4">
              <Button variant="ghost" size="sm">
                <Star className="w-4 h-4 mr-2" />
                Watchlist ({watchlist.length})
              </Button>
            </div>
          </div>
        </div>
      </header>

      {/* Main Content */}
      <main className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
        <div className="grid grid-cols-1 lg:grid-cols-3 gap-8">
          {/* Left Column - Search & Indices */}
          <div className="space-y-6">
            {/* Stock Search */}
            <Card>
              <CardHeader>
                <h2 className="text-lg font-semibold text-gray-900 flex items-center">
                  <Search className="w-5 h-5 mr-2" />
                  Search Stocks
                </h2>
              </CardHeader>
              <CardContent>
                <StockSearch onSelect={handleStockSearch} />
              </CardContent>
            </Card>

            {/* Market Indices */}
            <Card>
              <CardHeader>
                <h2 className="text-lg font-semibold text-gray-900">Market Indices</h2>
              </CardHeader>
              <CardContent>
                <div className="space-y-4">
                  <div>
                    <h3 className="text-sm font-medium text-gray-700 mb-2">NSE Indices</h3>
                    {loadingNSE ? (
                      <div className="animate-pulse space-y-2">
                        {[1, 2, 3].map((i) => (
                          <div key={i} className="h-16 bg-gray-200 rounded"></div>
                        ))}
                      </div>
                    ) : (
                      <div className="space-y-2">
                        {nseIndices.slice(0, 5).map((index) => (
                          <div
                            key={index.symbol}
                            className="flex items-center justify-between p-3 bg-gray-50 rounded-lg"
                          >
                            <div>
                              <div className="font-medium text-sm">{index.symbol}</div>
                              <div className="text-lg font-bold">
                                ₹{index.price.toLocaleString('en-IN')}
                              </div>
                            </div>
                            <div className={`flex items-center text-sm font-medium ${
                              index.change >= 0 ? 'text-success-600' : 'text-danger-600'
                            }`}>
                              {index.change >= 0 ? (
                                <TrendingUp className="w-4 h-4 mr-1" />
                              ) : (
                                <TrendingDown className="w-4 h-4 mr-1" />
                              )}
                              {index.changePercent.toFixed(2)}%
                            </div>
                          </div>
                        ))}
                      </div>
                    )}
                  </div>
                </div>
              </CardContent>
            </Card>

            {/* Top Movers */}
            {topMovers && (
              <Card>
                <CardHeader>
                  <h2 className="text-lg font-semibold text-gray-900">Top Movers</h2>
                </CardHeader>
                <CardContent>
                  <div className="space-y-4">
                    <div>
                      <h3 className="text-sm font-medium text-success-600 mb-2">Top Gainers</h3>
                      <div className="space-y-2">
                        {topMovers.gainers?.slice(0, 3).map((stock) => (
                          <div
                            key={stock.symbol}
                            className="flex items-center justify-between p-2 bg-success-50 rounded cursor-pointer hover:bg-success-100"
                            onClick={() => setSelectedStock(stock)}
                          >
                            <div>
                              <div className="font-medium text-sm">{stock.symbol}</div>
                              <div className="text-sm text-gray-600">₹{stock.price}</div>
                            </div>
                            <div className="text-success-600 text-sm font-medium">
                              +{stock.changePercent.toFixed(2)}%
                            </div>
                          </div>
                        ))}
                      </div>
                    </div>

                    <div>
                      <h3 className="text-sm font-medium text-danger-600 mb-2">Top Losers</h3>
                      <div className="space-y-2">
                        {topMovers.losers?.slice(0, 3).map((stock) => (
                          <div
                            key={stock.symbol}
                            className="flex items-center justify-between p-2 bg-danger-50 rounded cursor-pointer hover:bg-danger-100"
                            onClick={() => setSelectedStock(stock)}
                          >
                            <div>
                              <div className="font-medium text-sm">{stock.symbol}</div>
                              <div className="text-sm text-gray-600">₹{stock.price}</div>
                            </div>
                            <div className="text-danger-600 text-sm font-medium">
                              {stock.changePercent.toFixed(2)}%
                            </div>
                          </div>
                        ))}
                      </div>
                    </div>
                  </div>
                </CardContent>
              </Card>
            )}
          </div>

          {/* Right Column - Selected Stock Details */}
          <div className="lg:col-span-2 space-y-6">
            {selectedStock ? (
              <>
                {/* Stock Info Card */}
                <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
                  <StockCard 
                    stock={selectedStock}
                    onClick={() => handleAddToWatchlist(selectedStock)}
                  />
                  
                  <Card>
                    <CardHeader>
                      <h3 className="text-lg font-semibold text-gray-900">Quick Actions</h3>
                    </CardHeader>
                    <CardContent>
                      <div className="space-y-3">
                        <Button 
                          variant="primary" 
                          size="md" 
                          className="w-full"
                          onClick={() => handleAddToWatchlist(selectedStock)}
                        >
                          <Star className="w-4 h-4 mr-2" />
                          Add to Watchlist
                        </Button>
                        <Button variant="secondary" size="md" className="w-full">
                          View Analysis
                        </Button>
                        <Button variant="secondary" size="md" className="w-full">
                          Set Price Alert
                        </Button>
                      </div>
                    </CardContent>
                  </Card>
                </div>

                {/* Historical Chart */}
                {historicalData && !loadingHistory && (
                  <PriceChart 
                    data={historicalData} 
                    height={500}
                  />
                )}

                {loadingHistory && (
                  <Card>
                    <CardContent>
                      <div className="animate-pulse">
                        <div className="h-96 bg-gray-200 rounded"></div>
                      </div>
                    </CardContent>
                  </Card>
                )}
              </>
            ) : (
              /* Welcome Message */
              <Card>
                <CardContent className="text-center py-16">
                  <Activity className="w-16 h-16 text-gray-400 mx-auto mb-4" />
                  <h3 className="text-xl font-semibold text-gray-900 mb-2">
                    Welcome to Financial Agent
                  </h3>
                  <p className="text-gray-600 mb-6">
                    Search for Indian stocks (NSE & BSE) to view detailed analysis, 
                    historical charts, and market insights.
                  </p>
                  <div className="flex flex-wrap justify-center gap-2">
                    {['RELIANCE', 'TCS', 'INFY', 'HDFCBANK'].map((symbol) => (
                      <Button
                        key={symbol}
                        variant="secondary"
                        size="sm"
                        onClick={() => handleStockSearch({
                          symbol,
                          companyName: `${symbol} Company`,
                          exchange: 'NSE',
                        })}
                      >
                        {symbol}
                      </Button>
                    ))}
                  </div>
                </CardContent>
              </Card>
            )}
          </div>
        </div>
      </main>
    </div>
  );
};