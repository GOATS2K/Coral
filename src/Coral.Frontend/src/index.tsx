import React from 'react';
import ReactDOM from 'react-dom/client';
import App from './App';
import { OpenAPI } from './client';

if (process.env.NODE_ENV === 'development') {
  OpenAPI.BASE = 'https://localhost:7031';
}

const root = ReactDOM.createRoot(
  document.getElementById('root') as HTMLElement
);
root.render(
  <React.StrictMode>
    <App />
  </React.StrictMode>
);