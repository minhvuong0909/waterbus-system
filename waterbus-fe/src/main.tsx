import React from 'react'
import ReactDOM from 'react-dom/client'
import { Provider } from 'react-redux'
import { BrowserRouter } from 'react-router-dom'
import App from './App'
import { ErrorBoundary } from './components/ui'
import { I18nProvider } from './i18n'
import { store } from './store/store'
import './index.css'

// Tải lại trang (F5) phải về đầu trang: tắt việc trình duyệt tự khôi phục vị trí cuộn cũ, vì nó chạy
// sau khi nội dung tải xong và ghi đè lên lệnh cuộn lên đầu của App. Chuyển trang đã tự cuộn lên đầu.
if ('scrollRestoration' in window.history) window.history.scrollRestoration = 'manual'

ReactDOM.createRoot(document.getElementById('root') as HTMLElement).render(
  <React.StrictMode>
    <Provider store={store}>
      <BrowserRouter>
        <I18nProvider>
          <ErrorBoundary>
            <App />
          </ErrorBoundary>
        </I18nProvider>
      </BrowserRouter>
    </Provider>
  </React.StrictMode>,
)
