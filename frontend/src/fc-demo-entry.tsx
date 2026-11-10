// Scratch preview entry for the FileCard integration — served at /fc-demo.html
// by `npm run dev`. Not referenced by the app; delete once verified.
// Renders the documents register's own markup (reg-table / reg-filetype) against
// the app's real stylesheet stack, so the cell is checked in the context it ships
// in rather than in isolation. The API is not involved; rows below are fixtures.
import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import './styles/tailwind.css'
import './styles/brand.css'
import './styles/diwan.css'
import './styles/sanad.css'
import './styles/sanad-registry.css'
import './styles/sanad-motion.css'
import './styles/modern.css'
import './styles/hds.css'
import './styles/touch.css'
import './styles/forms.css'
import { applyTheme } from './lib/theme'
import { FileCard } from '@/components/ui/file-card-collections'
import { fileFormatOf } from '@/lib/fileFormats'

const rows: Array<{ num: string; title: string; ext: string | null; count: number }> = [
  { num: 'DOC-2026-0001', title: 'عقد توريد أجهزة حاسوب', ext: 'pdf', count: 1 },
  { num: 'DOC-2026-0002', title: 'محضر اجتماع مجلس الإدارة', ext: 'docx', count: 3 },
  { num: 'DOC-2026-0003', title: 'الميزانية التقديرية للربع الأول', ext: 'xlsx', count: 1 },
  { num: 'DOC-2026-0004', title: 'صورة مسح ضوئي للهوية', ext: 'tiff', count: 2 },
  { num: 'DOC-2026-0005', title: 'عرض تقديمي للخطة السنوية', ext: 'pptx', count: 1 },
  { num: 'DOC-2026-0006', title: 'أرشيف المراسلات المضغوط', ext: 'zip', count: 1 },
  { num: 'DOC-2026-0007', title: 'مذكرة داخلية بلا مرفقات', ext: null, count: 0 },
]

function Register() {
  return (
    <div className="p-8">
      <table className="reg-table">
        <thead>
          <tr>
            <th>رقم الوثيقة</th>
            <th>الملف</th>
            <th>العنوان</th>
            <th>الحالة</th>
          </tr>
        </thead>
        <tbody>
          {rows.map((r) => (
            <tr key={r.num} className="reg-row">
              <td className="mono num">{r.num}</td>
              <td className="reg-filetype">
                <span
                  className="reg-filetype__card"
                  title={r.ext ? `${r.ext.toUpperCase()}${r.count > 1 ? ` · ${r.count} مرفقات` : ''}` : 'بلا مرفق'}
                >
                  {r.ext ? (
                    <>
                      <FileCard formatFile={fileFormatOf(r.ext)} />
                      {r.count > 1 && <span className="reg-filetype__count">{r.count}</span>}
                    </>
                  ) : (
                    <span className="reg-filetype__none">—</span>
                  )}
                </span>
              </td>
              <td className="reg-subject">{r.title}</td>
              <td><span className="status-pill s-active">نشطة</span></td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}

applyTheme((new URLSearchParams(location.search).get('theme') as 'dark') === 'dark' ? 'dark' : 'light')

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <Register />
  </StrictMode>,
)
