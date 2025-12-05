# Financial Agent UI

Modern React-based user interface for the Indian Stock Market Financial Agent.

## 🚀 Quick Start

### Prerequisites
- Node.js 18+ 
- npm or yarn

### Installation
```bash
# Install dependencies
npm install

# Start development server
npm run dev

# Build for production
npm run build

# Run tests
npm test
```

### Development Server
The app will be available at http://localhost:3000 with hot-reload enabled.

## 🏗️ Project Structure

```
src/
├── components/          # Reusable UI components
│   ├── ui/             # Base UI components (Button, Card, Input)
│   ├── market/         # Market-specific components (StockCard, StockSearch)
│   └── charts/         # Chart components (PriceChart, VolumeChart)
├── pages/              # Page components
│   └── Dashboard.tsx   # Main dashboard page
├── services/           # API services and data fetching
│   ├── api.ts         # Axios configuration
│   └── marketData.ts  # Market data service
├── types/             # TypeScript type definitions
│   └── index.ts       # All type exports
└── styles/            # Global styles and Tailwind config
```

## 🔧 Configuration

### Environment Variables
Create a `.env` file in the root directory:

```env
VITE_API_BASE_URL=http://localhost:5000/api
VITE_APP_TITLE=Financial Agent
```

### API Integration
The UI connects to the .NET API backend:
- **Development**: http://localhost:5000/api
- **Production**: https://financial-agent-api.azurewebsites.net/api

## 🎨 UI Features

### Dashboard
- **Real-time Market Data**: Live NSE & BSE indices
- **Stock Search**: Intelligent search with autocomplete
- **Interactive Charts**: Historical price visualization with Recharts
- **Watchlist Management**: Save favorite stocks
- **Top Movers**: Gainers and losers tracking
- **Market Status**: Live market open/close status

### Components
- **Responsive Design**: Mobile-first responsive layout
- **Accessibility**: WCAG 2.1 compliant components
- **Performance**: Optimized bundle splitting and lazy loading
- **Dark Mode Ready**: Theme system prepared for dark mode

## 🔌 API Integration

### Market Data Service
```typescript
// Get NSE stock price
const stockData = await marketDataService.getNseStockPrice('RELIANCE');

// Search stocks
const results = await marketDataService.searchNseSymbols('REL');

// Get historical data
const history = await marketDataService.getNseHistoricalData(
  'RELIANCE', 
  '2024-01-01', 
  '2024-12-31'
);
```

### Query Management
- **React Query**: Server state management
- **Caching**: 5-minute stale time for market data
- **Retry Logic**: Exponential backoff for failed requests
- **Real-time Updates**: 30-second refresh intervals

## 🎯 Technology Stack

### Core
- **React 18**: Latest React with Suspense and Concurrent Features
- **TypeScript**: Full type safety and IntelliSense
- **Vite**: Lightning-fast build tool and dev server

### UI Framework
- **Tailwind CSS**: Utility-first styling with custom theme
- **Headless UI**: Unstyled accessible components
- **Lucide React**: Modern icon library
- **Framer Motion**: Smooth animations and transitions

### Data Management
- **React Query**: Server state and caching
- **Axios**: HTTP client with interceptors
- **Date-fns**: Date manipulation and formatting

### Visualization
- **Recharts**: Responsive chart library
- **Custom Chart Components**: Price charts, volume indicators

### Development Tools
- **ESLint**: Code linting and quality
- **Prettier**: Code formatting
- **Vitest**: Unit testing framework

## 📱 Responsive Design

### Breakpoints
- **Mobile**: 0px - 768px
- **Tablet**: 768px - 1024px  
- **Desktop**: 1024px+

### Layout Strategy
- **Mobile-first**: Progressive enhancement approach
- **Grid System**: CSS Grid and Flexbox
- **Adaptive Components**: Components adapt to screen size

## 🔍 Search Features

### Stock Search
- **Auto-complete**: Real-time search suggestions
- **Exchange Selection**: Switch between NSE/BSE
- **Popular Stocks**: Quick access to major stocks
- **Fuzzy Search**: Intelligent matching algorithm

### Popular Stocks Shortcuts
- RELIANCE, TCS, INFY, HDFCBANK, ICICIBANK, SBIN

## 📊 Chart Features

### Price Chart
- **Interactive**: Hover tooltips with detailed data
- **Time Periods**: 1M, 3M, 6M, 1Y views
- **Price Indicators**: High, low, volume statistics
- **Responsive**: Adapts to container size
- **Color Coding**: Green for gains, red for losses

### Technical Indicators (Future)
- Moving Averages (SMA, EMA)
- RSI, MACD, Bollinger Bands
- Volume indicators
- Support/Resistance levels

## 🎨 Theming

### Color Palette
```css
Primary: Blue (#3b82f6)
Success: Green (#22c55e) 
Danger: Red (#ef4444)
Gray Scale: Tailwind gray palette
```

### Custom Components
All UI components follow consistent design patterns:
- Rounded corners (8px)
- Drop shadows for depth
- Smooth transitions (200ms)
- Focus states for accessibility

## 🚀 Performance Optimizations

### Bundle Optimization
- **Code Splitting**: Separate chunks for vendor, UI, charts
- **Tree Shaking**: Remove unused code
- **Dynamic Imports**: Lazy load components

### Runtime Performance
- **Memoization**: React.memo for expensive components
- **Virtual Scrolling**: For large data lists
- **Image Optimization**: Responsive images with proper formats

### Caching Strategy
- **Query Caching**: 5-minute cache for market data
- **Asset Caching**: Static assets cached by service worker
- **API Response Caching**: HTTP cache headers respected

## 🧪 Testing

### Testing Strategy
```bash
# Run all tests
npm test

# Run tests in watch mode
npm run test:watch

# Run tests with UI
npm run test:ui
```

### Test Coverage
- Unit tests for utility functions
- Component tests for UI components
- Integration tests for API services
- E2E tests for critical user flows

## 🚀 Deployment

### Build Process
```bash
# Production build
npm run build

# Preview production build
npm run preview
```

### Deployment Targets
- **Azure Static Web Apps**: Recommended for production
- **Vercel**: Alternative deployment platform
- **Netlify**: Another deployment option

### CI/CD Integration
The project includes GitHub Actions workflow for:
- Automated testing
- Build verification
- Deployment to Azure

## 🔒 Security

### Best Practices
- **Environment Variables**: Sensitive data in env vars
- **HTTPS**: All API calls over secure connections
- **Input Validation**: Client-side validation for UX
- **XSS Protection**: React's built-in XSS protection

### API Security
- **CORS**: Configured for allowed origins
- **Rate Limiting**: Handled by backend API
- **Authentication**: Ready for JWT implementation

## 📝 Development Notes

### Code Standards
- **TypeScript Strict Mode**: Full type checking
- **ESLint Rules**: Consistent code style
- **Component Patterns**: Functional components with hooks
- **File Naming**: PascalCase for components, camelCase for utilities

### Git Workflow
- **Feature Branches**: Separate branch for each feature
- **Conventional Commits**: Structured commit messages
- **Pull Requests**: Code review before merge

This UI provides a modern, responsive, and feature-rich interface for your Financial Agent, designed to scale and evolve with your project requirements.