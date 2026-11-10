import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom'
import { QueryClientProvider } from '@tanstack/react-query'
import { queryClient } from './lib/queryClient'
import './i18n'
import './styles/tailwind.css'
import './styles/brand.css'
import './styles/diwan.css'
import './styles/sanad.css'
import './styles/sanad-registry.css'
import './styles/sanad-motion.css'
import './styles/modern.css'
import './styles/hds.css'
// Last of the app sheets: its touch minimums must win over the component styles above.
import './styles/touch.css'
import './styles/print.css'
import './lib/theme'
import './lib/appearance'
import LoginPage from './pages/LoginPage'
import DashboardPage from './pages/DashboardPage'
import DiwanListPage from './pages/diwan/DiwanListPage'
import DiwanComposePage from './pages/diwan/DiwanComposePage'
import DiwanDetailPage from './pages/diwan/DiwanDetailPage'
import InternalListPage from './pages/internal/InternalListPage'
import InternalComposePage from './pages/internal/InternalComposePage'
import InternalDetailPage from './pages/internal/InternalDetailPage'
import WorkflowDesignerPage from './pages/workflow/WorkflowDesignerPage'
import DocumentsListPage from './pages/documents/DocumentsListPage'
import DocumentCreatePage from './pages/documents/DocumentCreatePage'
import DocumentEditPage from './pages/documents/DocumentEditPage'
import DocumentDetailPage from './pages/documents/DocumentDetailPage'
import DispositionPage from './pages/disposition/DispositionPage'
import EventManagerDemo from './pages/EventManagerDemo'
import LocationsPage from './pages/locations/LocationsPage'
import ArchivePage from './pages/archive/ArchivePage'
import ExternalDirectoryPage from './pages/external/ExternalDirectoryPage'
import DepartmentsPage from './pages/departments/DepartmentsPage'
import UsersPage from './pages/users/UsersPage'
import MonitoringPage from './pages/monitoring/MonitoringPage'
import SettingsPage from './pages/settings/SettingsPage'
import ScannerPage from './pages/scanner/ScannerPage'
import BackupSettings from './components/BackupSettings'
import AccountPage from './pages/account/AccountPage'
import PrintDesignSection from './pages/print-design/PrintDesignSection'
import AppLayout from './components/AppLayout'
import { registerServiceWorker } from './lib/installApp'
import ScanAgentConnectPage from './pages/ScanAgentConnectPage'
import VerifyDocumentPage from './pages/VerifyDocumentPage'
import ProtectedRoute from './components/ProtectedRoute'
import GuestRoute from './components/GuestRoute'
import { ToastProvider } from './components/toast'
import { ConfirmProvider } from './components/ui/confirm-dialog'
import { loadBranding } from './lib/branding'

loadBranding()
// Registers only in production builds — see the note in installApp.ts.
registerServiceWorker()

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <QueryClientProvider client={queryClient}>
      <BrowserRouter>
        <ToastProvider>
         <ConfirmProvider>
          <Routes>
            <Route path="/login" element={<GuestRoute><LoginPage /></GuestRoute>} />
            {/* Public on purpose: the scan agent opens this at Windows login, long before anyone has
                signed into the app, and it only copies a pairing token into localStorage. */}
            <Route path="/scan-agent/connect" element={<ScanAgentConnectPage />} />
            {/* Public on purpose too: this is where the QR printed on a document lands, and the
                person scanning it is usually not a user of this system at all. */}
            <Route path="/verify/:code" element={<VerifyDocumentPage />} />
            <Route
              path="/app"
              element={
                <ProtectedRoute>
                  <AppLayout />
                </ProtectedRoute>
              }
            >
              <Route index element={<DashboardPage />} />
              {/* الديوان — external register */}
              <Route path="diwan" element={<DiwanListPage />} />
              <Route path="diwan/compose" element={<DiwanComposePage />} />
              <Route path="diwan/:id" element={<DiwanDetailPage />} />
              {/* المراسلات — internal correspondence */}
              <Route path="internal" element={<InternalListPage />} />
              <Route path="internal/compose" element={<InternalComposePage />} />
              <Route path="internal/:id" element={<InternalDetailPage />} />
              {/* سير العمل — routes that drive المراسلات; the runs themselves live in the inbox */}
              <Route path="workflow" element={<WorkflowDesignerPage />} />
              {/* legacy links */}
              <Route path="correspondence" element={<Navigate to="/app/internal" replace />} />
              <Route path="documents" element={<DocumentsListPage />} />
              <Route path="documents/new" element={<DocumentCreatePage />} />
              {/* Scanning is merged into the single create form. */}
              <Route path="documents/scan" element={<Navigate to="/app/documents/new" replace />} />
              <Route path="documents/:id/edit" element={<DocumentEditPage />} />
              <Route path="documents/:id" element={<DocumentDetailPage />} />
              <Route path="disposition" element={<DispositionPage />} />
              <Route path="events" element={<EventManagerDemo />} />
              <Route path="archive" element={<ArchivePage />} />
              <Route path="external" element={<ExternalDirectoryPage />} />
              <Route path="departments" element={<DepartmentsPage />} />
              <Route path="users" element={<UsersPage />} />
              <Route path="locations" element={<LocationsPage />} />
              <Route path="monitoring" element={<MonitoringPage />} />
              <Route path="settings" element={<SettingsPage />} />
              <Route path="scanner" element={<ScannerPage />} />
              {/* Rendered bare: BackupSettings brings its own page header. */}
              <Route path="backup" element={<BackupSettings />} />
              <Route path="account" element={<AccountPage />} />
              {/* تصاميم الطباعة — the gallery, or the designer once a design is opened (the
                  section reads that off the query string). /edit is an old link shape. */}
              <Route path="print-design" element={<PrintDesignSection />} />
              <Route path="print-design/edit" element={<PrintDesignSection />} />
              {/* The letterhead designer was removed from the UI. Outgoing letters still render
                  server-side from their saved slot values, so old links go to the designs list
                  rather than 404ing. */}
              <Route path="letterhead" element={<Navigate to="/app/print-design" replace />} />
              {/* Any other /app path — a bookmark to a removed module such as the old
                  الاجتماعات والقرارات screen — lands on the dashboard. Without this the shell
                  renders with an empty content area, which reads as a broken page. */}
              <Route path="*" element={<Navigate to="/app" replace />} />
            </Route>
            <Route path="*" element={<Navigate to="/app" replace />} />
          </Routes>
         </ConfirmProvider>
        </ToastProvider>
      </BrowserRouter>
    </QueryClientProvider>
  </StrictMode>,
)