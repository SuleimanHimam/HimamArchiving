import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom'
import { QueryClientProvider } from '@tanstack/react-query'
import { queryClient } from './lib/queryClient'
import './i18n'
import './styles/tailwind.css'
import './styles/diwan.css'
import './styles/print.css'
import LoginPage from './pages/LoginPage'
import DashboardPage from './pages/DashboardPage'
import IncomingListPage from './pages/incoming/IncomingListPage'
import IncomingCreatePage from './pages/incoming/IncomingCreatePage'
import IncomingDetailPage from './pages/incoming/IncomingDetailPage'
import DocumentsListPage from './pages/documents/DocumentsListPage'
import DocumentCreatePage from './pages/documents/DocumentCreatePage'
import DocumentEditPage from './pages/documents/DocumentEditPage'
import DocumentScanPage from './pages/documents/DocumentScanPage'
import DocumentDetailPage from './pages/documents/DocumentDetailPage'
import OutgoingListPage from './pages/outgoing/OutgoingListPage'
import OutgoingCreatePage from './pages/outgoing/OutgoingCreatePage'
import OutgoingDetailPage from './pages/outgoing/OutgoingDetailPage'
import WorklistPage from './pages/workflow/WorklistPage'
import ArchivePage from './pages/archive/ArchivePage'
import ReportsPage from './pages/reports/ReportsPage'
import MonitoringPage from './pages/monitoring/MonitoringPage'
import SettingsPage from './pages/settings/SettingsPage'
import AppLayout from './components/AppLayout'
import ProtectedRoute from './components/ProtectedRoute'
import { ToastProvider } from './components/toast'
import { loadBranding } from './lib/branding'

loadBranding()

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <QueryClientProvider client={queryClient}>
    <BrowserRouter>
      <ToastProvider>
      <Routes>
        <Route path="/login" element={<LoginPage />} />
        <Route
          path="/app"
          element={
            <ProtectedRoute>
              <AppLayout />
            </ProtectedRoute>
          }
        >
          <Route index element={<DashboardPage />} />
          <Route path="incoming" element={<IncomingListPage />} />
          <Route path="incoming/new" element={<IncomingCreatePage />} />
          <Route path="incoming/:id" element={<IncomingDetailPage />} />
          <Route path="documents" element={<DocumentsListPage />} />
          <Route path="documents/new" element={<DocumentCreatePage />} />
          <Route path="documents/scan" element={<DocumentScanPage />} />
          <Route path="documents/:id/edit" element={<DocumentEditPage />} />
          <Route path="documents/:id" element={<DocumentDetailPage />} />
          <Route path="outgoing" element={<OutgoingListPage />} />
          <Route path="outgoing/new" element={<OutgoingCreatePage />} />
          <Route path="outgoing/:id" element={<OutgoingDetailPage />} />
          <Route path="workflow" element={<WorklistPage />} />
          <Route path="archive" element={<ArchivePage />} />
          <Route path="reports" element={<ReportsPage />} />
          <Route path="monitoring" element={<MonitoringPage />} />
          <Route path="settings" element={<SettingsPage />} />
        </Route>
        <Route path="*" element={<Navigate to="/app" replace />} />
      </Routes>
      </ToastProvider>
    </BrowserRouter>
    </QueryClientProvider>
  </StrictMode>,
)
