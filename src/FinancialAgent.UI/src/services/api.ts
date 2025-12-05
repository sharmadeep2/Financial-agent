import axios, { AxiosInstance, AxiosResponse, AxiosError } from 'axios';

// Create axios instance with base configuration
const apiClient: AxiosInstance = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL || '/api',
  timeout: 30000,
  headers: {
    'Content-Type': 'application/json',
    'Accept': 'application/json',
  },
});

// Request interceptor
apiClient.interceptors.request.use(
  (config) => {
    // Add authentication token if available
    const token = localStorage.getItem('auth_token');
    if (token) {
      config.headers.Authorization = `Bearer ${token}`;
    }
    
    // Add request timestamp for debugging
    config.metadata = { startTime: new Date() };
    console.log(`🚀 API Request: ${config.method?.toUpperCase()} ${config.url}`);
    
    return config;
  },
  (error) => {
    console.error('❌ Request interceptor error:', error);
    return Promise.reject(error);
  }
);

// Response interceptor
apiClient.interceptors.response.use(
  (response: AxiosResponse) => {
    const endTime = new Date();
    const startTime = response.config.metadata?.startTime || endTime;
    const duration = endTime.getTime() - startTime.getTime();
    
    console.log(`✅ API Response: ${response.config.method?.toUpperCase()} ${response.config.url} - ${duration}ms`);
    return response;
  },
  (error: AxiosError) => {
    const endTime = new Date();
    const startTime = error.config?.metadata?.startTime || endTime;
    const duration = endTime.getTime() - startTime.getTime();
    
    console.error(`❌ API Error: ${error.config?.method?.toUpperCase()} ${error.config?.url} - ${duration}ms`, {
      status: error.response?.status,
      message: error.message,
      data: error.response?.data,
    });

    // Handle common error scenarios
    if (error.response?.status === 401) {
      // Unauthorized - redirect to login
      localStorage.removeItem('auth_token');
      window.location.href = '/login';
    } else if (error.response?.status >= 500) {
      // Server errors - show toast notification
      console.error('Server error occurred. Please try again later.');
    }

    return Promise.reject(error);
  }
);

// Generic API methods
export const api = {
  get: <T>(url: string, params?: Record<string, any>): Promise<T> =>
    apiClient.get(url, { params }).then((response) => response.data),

  post: <T>(url: string, data?: any): Promise<T> =>
    apiClient.post(url, data).then((response) => response.data),

  put: <T>(url: string, data?: any): Promise<T> =>
    apiClient.put(url, data).then((response) => response.data),

  delete: <T>(url: string): Promise<T> =>
    apiClient.delete(url).then((response) => response.data),

  patch: <T>(url: string, data?: any): Promise<T> =>
    apiClient.patch(url, data).then((response) => response.data),
};

export default apiClient;

// Extend axios config interface for metadata
declare module 'axios' {
  interface AxiosRequestConfig {
    metadata?: {
      startTime: Date;
    };
  }
}