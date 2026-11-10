import { useSearchParams } from 'react-router-dom'
import DesignGalleryPage from './DesignGalleryPage'
import ReportDesignerPage from './ReportDesignerPage'

/**
 * تصاميم الطباعة: the gallery, or the designer once a design is opened.
 *
 * It is a section of its own again rather than a Settings panel — every user owns a personal
 * design, so it is not an administrator's screen. Both views share the one route and switch on
 * the query string; keeping that state in the URL (rather than in React state) means a design
 * being edited survives a refresh and can be linked to.
 */
export default function PrintDesignSection() {
  const [params] = useSearchParams()
  // `mode` is set for both "new" and "edit"; templateId alone means an existing design.
  const editing = params.has('mode') || params.has('templateId')
  return editing ? <ReportDesignerPage /> : <DesignGalleryPage />
}
