import React, { useState } from 'react';
import {
  LineChart,
  Line,
  XAxis,
  YAxis,
  CartesianGrid,
  Tooltip,
  Legend,
  ResponsiveContainer,
  ReferenceLine,
} from 'recharts';
import { format, parseISO } from 'date-fns';
import { Card, CardHeader, CardContent, Button } from '@/components/ui';
import type { HistoricalStockData, ChartConfig } from '@/types';

interface PriceChartProps {
  data: HistoricalStockData;
  height?: number;
  showVolume?: boolean;
}

interface ChartDataPoint {
  date: string;
  close: number;
  volume: number;
  high: number;
  low: number;
  open: number;
  displayDate: string;
}

export const PriceChart: React.FC<PriceChartProps> = ({
  data,
  height = 400,
  showVolume = false,
}) => {
  const [config, setConfig] = useState<ChartConfig>({
    timeRange: '1Y',
    chartType: 'line',
    indicators: [],
  });

  const formatChartData = (historicalData: HistoricalStockData): ChartDataPoint[] => {
    return historicalData.dailyPrices.map((price) => ({
      date: price.date,
      close: price.close,
      volume: price.volume,
      high: price.high,
      low: price.low,
      open: price.open,
      displayDate: format(parseISO(price.date), 'MMM dd'),
    }));
  };

  const chartData = formatChartData(data);

  const formatPrice = (value: number) => {
    return new Intl.NumberFormat('en-IN', {
      style: 'currency',
      currency: 'INR',
      minimumFractionDigits: 0,
      maximumFractionDigits: 0,
    }).format(value);
  };

  const formatVolume = (value: number) => {
    if (value >= 10000000) {
      return `${(value / 10000000).toFixed(1)}Cr`;
    } else if (value >= 100000) {
      return `${(value / 100000).toFixed(1)}L`;
    } else if (value >= 1000) {
      return `${(value / 1000).toFixed(1)}K`;
    }
    return value.toString();
  };

  const CustomTooltip = ({ active, payload, label }: any) => {
    if (active && payload && payload.length) {
      const data = payload[0].payload;
      return (
        <div className="bg-white p-4 border border-gray-200 rounded-lg shadow-lg">
          <p className="font-medium text-gray-900">
            {format(parseISO(data.date), 'MMM dd, yyyy')}
          </p>
          <div className="mt-2 space-y-1">
            <div className="flex justify-between items-center">
              <span className="text-gray-600">Close:</span>
              <span className="font-medium">{formatPrice(data.close)}</span>
            </div>
            <div className="flex justify-between items-center">
              <span className="text-gray-600">High:</span>
              <span className="font-medium text-success-600">{formatPrice(data.high)}</span>
            </div>
            <div className="flex justify-between items-center">
              <span className="text-gray-600">Low:</span>
              <span className="font-medium text-danger-600">{formatPrice(data.low)}</span>
            </div>
            <div className="flex justify-between items-center">
              <span className="text-gray-600">Volume:</span>
              <span className="font-medium font-mono">{formatVolume(data.volume)}</span>
            </div>
          </div>
        </div>
      );
    }
    return null;
  };

  const getLineColor = () => {
    const firstPrice = chartData[0]?.close || 0;
    const lastPrice = chartData[chartData.length - 1]?.close || 0;
    return lastPrice >= firstPrice ? '#22c55e' : '#ef4444';
  };

  const calculatePriceChange = () => {
    if (chartData.length < 2) return { change: 0, changePercent: 0 };
    
    const firstPrice = chartData[0].close;
    const lastPrice = chartData[chartData.length - 1].close;
    const change = lastPrice - firstPrice;
    const changePercent = (change / firstPrice) * 100;
    
    return { change, changePercent };
  };

  const { change, changePercent } = calculatePriceChange();

  return (
    <Card>
      <CardHeader>
        <div className="flex items-center justify-between">
          <div>
            <h3 className="text-lg font-semibold text-gray-900">
              {data.symbol} ({data.exchange})
            </h3>
            <div className="flex items-center space-x-4 mt-1">
              <span className="text-2xl font-bold text-gray-900">
                {chartData.length > 0 && formatPrice(chartData[chartData.length - 1].close)}
              </span>
              <span
                className={`text-sm font-medium ${
                  change >= 0 
                    ? 'text-success-600 bg-success-50' 
                    : 'text-danger-600 bg-danger-50'
                } px-2 py-1 rounded-full`}
              >
                {change >= 0 ? '+' : ''}{formatPrice(change)} ({changePercent >= 0 ? '+' : ''}{changePercent.toFixed(2)}%)
              </span>
            </div>
          </div>
          
          <div className="flex space-x-2">
            {['1M', '3M', '6M', '1Y'].map((period) => (
              <Button
                key={period}
                variant={config.timeRange === period ? 'primary' : 'secondary'}
                size="sm"
                onClick={() => setConfig({ ...config, timeRange: period as any })}
              >
                {period}
              </Button>
            ))}
          </div>
        </div>
      </CardHeader>
      
      <CardContent>
        <div style={{ height }}>
          <ResponsiveContainer width="100%" height="100%">
            <LineChart data={chartData} margin={{ top: 5, right: 30, left: 20, bottom: 5 }}>
              <CartesianGrid strokeDasharray="3 3" stroke="#f1f5f9" />
              <XAxis 
                dataKey="displayDate"
                axisLine={false}
                tickLine={false}
                tick={{ fontSize: 12, fill: '#64748b' }}
              />
              <YAxis 
                domain={['dataMin - 10', 'dataMax + 10']}
                axisLine={false}
                tickLine={false}
                tick={{ fontSize: 12, fill: '#64748b' }}
                tickFormatter={formatPrice}
              />
              <Tooltip content={<CustomTooltip />} />
              
              <Line
                type="monotone"
                dataKey="close"
                stroke={getLineColor()}
                strokeWidth={2}
                dot={false}
                activeDot={{ r: 6, stroke: getLineColor(), strokeWidth: 2, fill: '#fff' }}
              />
              
              {/* Reference line for starting price */}
              {chartData.length > 0 && (
                <ReferenceLine 
                  y={chartData[0].close} 
                  stroke="#9ca3af" 
                  strokeDasharray="5 5" 
                  strokeWidth={1}
                />
              )}
            </LineChart>
          </ResponsiveContainer>
        </div>

        {/* Chart Statistics */}
        <div className="mt-4 grid grid-cols-2 md:grid-cols-4 gap-4 pt-4 border-t border-gray-100">
          <div className="text-center">
            <div className="text-sm text-gray-500">Period</div>
            <div className="font-medium">
              {data.dailyPrices.length} days
            </div>
          </div>
          <div className="text-center">
            <div className="text-sm text-gray-500">High</div>
            <div className="font-medium text-success-600">
              {formatPrice(Math.max(...chartData.map(d => d.high)))}
            </div>
          </div>
          <div className="text-center">
            <div className="text-sm text-gray-500">Low</div>
            <div className="font-medium text-danger-600">
              {formatPrice(Math.min(...chartData.map(d => d.low)))}
            </div>
          </div>
          <div className="text-center">
            <div className="text-sm text-gray-500">Avg Volume</div>
            <div className="font-medium font-mono">
              {formatVolume(chartData.reduce((sum, d) => sum + d.volume, 0) / chartData.length)}
            </div>
          </div>
        </div>
      </CardContent>
    </Card>
  );
};