import React from 'react';
import { motion } from 'framer-motion';
import { TrendingUp, TrendingDown, Minus } from 'lucide-react';
import { Card, CardContent } from '@/components/ui';
import type { StockData } from '@/types';

interface StockCardProps {
  stock: StockData;
  onClick?: () => void;
}

export const StockCard: React.FC<StockCardProps> = ({ stock, onClick }) => {
  const isPositive = stock.change > 0;
  const isNegative = stock.change < 0;

  const formatPrice = (price: number) => {
    return new Intl.NumberFormat('en-IN', {
      style: 'currency',
      currency: 'INR',
      minimumFractionDigits: 2,
    }).format(price);
  };

  const formatChange = (change: number, changePercent: number) => {
    const sign = change >= 0 ? '+' : '';
    return `${sign}${change.toFixed(2)} (${sign}${changePercent.toFixed(2)}%)`;
  };

  const formatVolume = (volume: number) => {
    if (volume >= 10000000) {
      return `${(volume / 10000000).toFixed(1)}Cr`;
    } else if (volume >= 100000) {
      return `${(volume / 100000).toFixed(1)}L`;
    } else if (volume >= 1000) {
      return `${(volume / 1000).toFixed(1)}K`;
    }
    return volume.toString();
  };

  const getTrendIcon = () => {
    if (isPositive) return <TrendingUp className="h-4 w-4" />;
    if (isNegative) return <TrendingDown className="h-4 w-4" />;
    return <Minus className="h-4 w-4" />;
  };

  const getPriceColorClass = () => {
    if (isPositive) return 'price-positive';
    if (isNegative) return 'price-negative';
    return 'price-neutral';
  };

  return (
    <motion.div
      whileHover={{ scale: 1.02 }}
      whileTap={{ scale: 0.98 }}
      initial={{ opacity: 0, y: 20 }}
      animate={{ opacity: 1, y: 0 }}
      transition={{ duration: 0.3 }}
    >
      <Card 
        className={`cursor-pointer transition-all duration-200 hover:shadow-lg ${
          onClick ? 'hover:shadow-primary-100' : ''
        }`}
        onClick={onClick}
      >
        <CardContent>
          <div className="space-y-3">
            {/* Header */}
            <div className="flex items-start justify-between">
              <div>
                <h3 className="font-semibold text-gray-900 truncate">
                  {stock.symbol}
                </h3>
                <p className="text-sm text-gray-600 truncate">
                  {stock.companyName}
                </p>
                <span className="inline-flex items-center px-2 py-1 rounded-full text-xs font-medium bg-gray-100 text-gray-700 mt-1">
                  {stock.exchange}
                </span>
              </div>
              <div className={`flex items-center space-x-1 px-2 py-1 rounded-full text-sm font-medium ${getPriceColorClass()}`}>
                {getTrendIcon()}
                <span>{formatChange(stock.change, stock.changePercent)}</span>
              </div>
            </div>

            {/* Price */}
            <div>
              <div className="text-2xl font-bold text-gray-900">
                {formatPrice(stock.price)}
              </div>
            </div>

            {/* Market Data Grid */}
            <div className="grid grid-cols-2 gap-3 text-sm">
              <div>
                <span className="text-gray-500">Open:</span>
                <div className="font-medium">{formatPrice(stock.open)}</div>
              </div>
              <div>
                <span className="text-gray-500">Prev Close:</span>
                <div className="font-medium">{formatPrice(stock.previousClose)}</div>
              </div>
              <div>
                <span className="text-gray-500">High:</span>
                <div className="font-medium text-success-600">{formatPrice(stock.dayHigh)}</div>
              </div>
              <div>
                <span className="text-gray-500">Low:</span>
                <div className="font-medium text-danger-600">{formatPrice(stock.dayLow)}</div>
              </div>
            </div>

            {/* Volume */}
            <div className="pt-2 border-t border-gray-100">
              <div className="flex justify-between items-center text-sm">
                <span className="text-gray-500">Volume:</span>
                <span className="font-medium font-mono">{formatVolume(stock.volume)}</span>
              </div>
              {stock.marketCap && (
                <div className="flex justify-between items-center text-sm mt-1">
                  <span className="text-gray-500">Market Cap:</span>
                  <span className="font-medium font-mono">{formatVolume(stock.marketCap)}</span>
                </div>
              )}
            </div>

            {/* Timestamp */}
            <div className="text-xs text-gray-400 text-right">
              Last updated: {new Date(stock.timestamp).toLocaleTimeString('en-IN', {
                hour: '2-digit',
                minute: '2-digit',
                second: '2-digit',
              })}
            </div>
          </div>
        </CardContent>
      </Card>
    </motion.div>
  );
};