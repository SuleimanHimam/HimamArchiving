import { ArrowRight, Star, Download, Users, Pencil, Printer, PackageCheck, Archive, X, Scan } from 'lucide-react'
import { useEffect, useState, useCallback, useRef } from 'react'
import { useParams, Link, useNavigate } from 'react-router-dom'
import { AxiosError } from 'axios'
import { motion } from 'motion/react'
import { auth } from '../../lib/auth'
import { documents, type DocumentDetail, type ScanFormat, DOC_STATUS_LABELS, formatBytes } from '../../lib/documents'
import { scanAgent } from '../../lib/scanAgent'
import { scannerSettings } from '../../lib/scannerSettings'
import LocationPicker from '../../components/LocationPicker'
import { favoritesApi, sharingApi, exportApi, foldersApi, type Share, type Folder } from '../../lib/userFeatures'
import { FolderGlyph } from '../../components/ui/folder-dialog'
import RequestDispositionModal from '../../components/RequestDispositionModal'
import DocumentLocation from '../../components/DocumentLocation'
import { CONFIDENTIALITY_LABELS } from '../../lib/shared'
import { useScreenGuard, isSensitive } from '../../lib/useScreenGuard'
import { useToast } from '../../components/toast'
import { useConfirmDialog } from '../../components/ui/confirm-dialog'
import { LabeledSelect } from '../../components/ui/labeled-input'
import { useDateFormat } from '../../lib/useCalendar'
import PreservationPackagesPanel from '../../components/PreservationPackagesPanel'
import DocumentNotesPanel from '../../components/DocumentNotesPanel'
import CustomFieldsPanel from '../../components/CustomFieldsPanel'
import DocumentVerificationCard from '../../components/DocumentVerificationCard'
import '../../styles/forms.css'
// The folder glyph's styles live here; this page names a folder, so it draws one.
import '../../styles/file-browser.css'
import './documents.css'

export default function DocumentDetailPage() {
  const { id } = useParams()
  const docId = Number(id)
  const navigate = useNavigate()
  const { dateTime } = useDateFormat()
  const toast = useToast()
  const { confirm } = useConfirmDialog()
  const fileInput = useRef<HTMLInputElement>(null)
  const scanFallbackInput = useRef<HTMLInputElement>(null)
  const previewFrame = useRef<HTMLIFrameElement>(null)
  const [doc, setDoc] = useState<DocumentDetail | null>(null)
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)
  const [scanMsg, setScanMsg] = useState('')
  const [scanName, setScanName] = useState('')
  const [scanFormat] = useState<ScanFormat>('pdf')   // scans are always saved as PDF
  const [preview, setPreview] = useState<{ url: string; name: string; type: string } | null>(null)
  const [archiveOpen, setArchiveOpen] = useState(false)
  /* The receipt shown after a successful hand-over to قسم الأرشيف. Held in its own state rather
     than read off `doc`, so the number on screen is the one that was actually sent even after the
     page reloads the document behind it. */
  const [sentInfo, setSentInfo] = useState<{ number: string; title: string } | null>(null)
  const [archBoxId, setArchBoxId] = useState<number | null>(null)
  const [fav, setFav] = useState(false)
  const [shareOpen, setShareOpen] = useState(false)
  const [shares, setShares] = useState<Share[]>([])
  const [shareUsers, setShareUsers] = useState<{ id: number; fullName: string }[]>([])
  const [shareForm, setShareForm] = useState({ userId: '', canEdit: false })
  const [folders, setFolders] = useState<Folder[]>([])

  const load = useCallback(async () => {
    setError('')
    try { setDoc(await documents.get(docId)) }
    catch (err) {
      /* "You may not have access" was told to everyone, including people arriving from a
         notification about a document that had since been deleted — they went looking for a
         permission problem that did not exist. The status says which it is: 404 is gone, 403 is
         not yours. */
      const status = (err as AxiosError).response?.status
      setError(
        status === 404 ? 'هذه الوثيقة لم تعد موجودة — ربما حُذفت بعد إرسال الإشعار.'
        : status === 403 ? 'لا تملك صلاحية الوصول إلى هذه الوثيقة.'
        : 'تعذّر تحميل الوثيقة — حاول مرة أخرى.',
      )
    }
  }, [docId])

  const canPrintDoc = auth.hasPermission('Documents.Print')

  // The capture guard covers سري/سري للغاية documents and every open preview.
  useScreenGuard(!!preview || isSensitive(doc?.confidentiality), {
    onViolation: () => toast.error('محتوى محمي — لا يُسمح بالتقاط صورة الشاشة'),
  })

  useEffect(() => { load() }, [load])
  useEffect(() => { setFav(!!doc?.isFavorite) }, [doc?.isFavorite])
  useEffect(() => { foldersApi.list().then(setFolders).catch(() => {}) }, [])

  async function toggleFavorite() {
    try {
      if (fav) { await favoritesApi.remove(docId); setFav(false) }
      else { await favoritesApi.add(docId); setFav(true) }
    } catch { toast.error('تعذّر تحديث المفضلة') }
  }

  const [reqOpen, setReqOpen] = useState(false)

  async function sendToArchive() {
    if (!doc) return
    // The number, not just the title: titles repeat across departments («محضر اجتماع — …») and the
    // number is what the archive will ask for on the phone, so it is what the sender must check.
    if (!(await confirm(
      `إرسال الوثيقة رقم ${doc.documentNumber} «${doc.title}» إلى قسم الأرشيف لمراجعتها وتخصيص موقع فيزيائي لها؟`
    ))) return
    setBusy(true)
    try {
      await documents.sendToArchive(docId)
      // A dialog rather than a toast: this is the receipt for a hand-over to another department,
      // and a 4-second toast takes the reference number away with it.
      setSentInfo({ number: doc.documentNumber, title: doc.title })
      await load()
    } catch (e) {
      const msg = (e as { response?: { data?: { error?: string } } })?.response?.data?.error
      toast.error(msg ?? 'تعذّر الإرسال إلى الأرشيف')
    } finally { setBusy(false) }
  }

  async function downloadZip() {
    if (!doc) return
    try { await exportApi.documentZip(docId, doc.documentNumber) }
    catch { toast.error('تعذّر تنزيل الملف المضغوط') }
  }

  async function moveToFolder(folderId: number | null) {
    try { await foldersApi.moveDocument(docId, folderId); await load(); toast.success('تم نقل الوثيقة') }
    catch { toast.error('تعذّر نقل الوثيقة') }
  }

  async function openShare() {
    setShareOpen(true)
    try {
      const [s, u] = await Promise.all([sharingApi.list(docId), documents.users()])
      setShares(s); setShareUsers(u)
    } catch { toast.error('تعذّر تحميل المشاركات') }
  }
  async function doShare(e: React.FormEvent) {
    e.preventDefault()
    if (!shareForm.userId) return
    try {
      await sharingApi.share(docId, Number(shareForm.userId), shareForm.canEdit)
      setShareForm({ userId: '', canEdit: false })
      setShares(await sharingApi.list(docId))
      toast.success('تمت المشاركة')
    } catch { toast.error('تعذّر المشاركة') }
  }
  async function removeShare(userId: number) {
    try { await sharingApi.unshare(docId, userId); setShares(await sharingApi.list(docId)) }
    catch { toast.error('تعذّر إلغاء المشاركة') }
  }

  // Modal a11y: lock body scroll and close on Escape while a preview is open.
  useEffect(() => {
    if (!preview) return
    const onKey = (e: KeyboardEvent) => { if (e.key === 'Escape') closePreview() }
    document.addEventListener('keydown', onKey)
    const prevOverflow = document.body.style.overflow
    document.body.style.overflow = 'hidden'
    return () => { document.removeEventListener('keydown', onKey); document.body.style.overflow = prevOverflow }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [preview])

  async function onUpload(e: React.ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0]
    if (!file) return
    setBusy(true); setError('')
    try {
      await documents.upload(docId, file)
      await load()
      toast.success('تم رفع المرفق')
    } catch (err) {
      const ax = err as { response?: { data?: { error?: string } } }
      toast.error(ax.response?.data?.error ?? 'تعذّر رفع المرفق')
    } finally {
      setBusy(false)
      if (fileInput.current) fileInput.current.value = ''
    }
  }

  // Scan from the user's local scanner via the scan agent; fall back to a file picker
  // (a file produced by the scanner's own software) if the agent isn't running.
  async function onScan() {
    setBusy(true); setError(''); setScanMsg('جارٍ الاتصال بالماسح الضوئي…')
    try {
      const status = await scanAgent.status()
      if (!status) {
        setScanMsg('')
        setBusy(false)
        scanFallbackInput.current?.click() // no local agent → let the user pick a scanned file
        return
      }
      const chosen = scannerSettings.get() ?? undefined
      setScanMsg(chosen ? `جارٍ المسح من: ${chosen}…` : 'جارٍ المسح الضوئي…')
      // Upload the raw scanner image (JPEG) — robust across scanners and color modes.
      const { blob } = await scanAgent.scan('jpeg', chosen)
      await documents.scan(docId, blob, buildScanFileName(scanFormat), scanFormat)
      setScanMsg(''); setScanName('')
      await load()
      toast.success('تمت إضافة المسح الضوئي')
    } catch (err) {
      const msg = (err as { response?: { data?: { error?: string } }; message?: string })
      toast.error(msg.response?.data?.error ?? msg.message ?? 'تعذّر المسح الضوئي')
      setScanMsg('')
    } finally { setBusy(false) }
  }

  async function onScanFallback(e: React.ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0]
    if (!file) return
    setBusy(true); setError('')
    try {
      // Honor a user-given name; otherwise keep the picked file's own name.
      const fileName = scanName.trim() ? buildScanFileName(scanFormat) : file.name
      await documents.scan(docId, file, fileName, scanFormat)
      setScanName('')
      await load()
      toast.success('تمت إضافة المسح الضوئي')
    } catch (err) {
      const ax = err as { response?: { data?: { error?: string } } }
      toast.error(ax.response?.data?.error ?? 'تعذّر رفع المسح الضوئي')
    } finally {
      setBusy(false)
      if (scanFallbackInput.current) scanFallbackInput.current.value = ''
    }
  }

  // Uses the user-given title as the file name (the scan time is shown separately per row).
  function buildScanFileName(ext: string): string {
    const base = scanName.trim() || 'مسح-ضوئي'
    return `${base}.${ext}`
  }

  async function openPreview(a: DocumentDetail['attachments'][number]) {
    setError('')
    try {
      const url = await documents.fetchObjectUrl(docId, a.id, a.contentType)
      setPreview({ url, name: a.fileName, type: a.contentType })
    } catch {
      toast.error('تعذّر فتح المعاينة')
    }
  }

  function closePreview() {
    if (preview) URL.revokeObjectURL(preview.url)
    setPreview(null)
  }

  // Print the previewed attachment: PDFs print via the embedded viewer; images via a print window.
  function printPreview() {
    if (!preview) return
    if (preview.type.includes('pdf')) {
      previewFrame.current?.contentWindow?.print()
    } else {
      const w = window.open(preview.url, '_blank')
      w?.addEventListener('load', () => w.print())
    }
  }

  // Print a PDF blob URL via a hidden iframe + the browser's PDF viewer.
  function printPdfUrl(url: string) {
    const frame = document.createElement('iframe')
    frame.style.cssText = 'position:fixed;right:0;bottom:0;width:0;height:0;border:0'
    frame.src = url
    frame.onload = () => {
      frame.contentWindow?.focus()
      frame.contentWindow?.print()
      setTimeout(() => { frame.remove(); URL.revokeObjectURL(url) }, 60_000)
    }
    document.body.appendChild(frame)
  }

  // Print an attachment directly from its row (PDF / PNG / JPG) without opening the preview.
  async function printAttachment(a: DocumentDetail['attachments'][number]) {
    try {
      const url = await documents.fetchObjectUrl(docId, a.id, a.contentType)
      if (a.contentType.includes('pdf')) {
        printPdfUrl(url)
      } else {
        const w = window.open('', '_blank')
        if (w) {
          w.document.write(
            `<title>${a.fileName}</title><img src="${url}" style="max-width:100%" ` +
            `onload="window.focus();window.print()" />`)
          w.document.close()
          setTimeout(() => URL.revokeObjectURL(url), 60_000)
        }
      }
    } catch {
      toast.error('تعذّر طباعة الملف')
    }
  }

  // Print an attachment on a physical printer via the local agent (the chosen default printer,
  // or the first one the agent reports). PDFs/images are spooled by the agent on the user's PC.
  async function printToPrinter(a: DocumentDetail['attachments'][number]) {
    setBusy(true)
    try {
      let printer = scannerSettings.getPrinter()
      if (!printer) {
        const st = await scanAgent.status()
        if (!st) { toast.error('الوكيل المحلي غير مُشغّل — حمّله وشغّله من الإعدادات'); return }
        printer = st.printers?.[0] ?? null
        if (!printer) { toast.error('لا توجد طابعة متاحة — اختر طابعة من الإعدادات'); return }
      }
      const url = await documents.fetchObjectUrl(docId, a.id, a.contentType)
      const blob = await (await fetch(url)).blob()
      URL.revokeObjectURL(url)
      const r = await scanAgent.print(blob, { printer, ext: a.fileExtension })
      toast.success(`تمت الطباعة على ${r.printer} (${r.pages} صفحة)`)
    } catch (err) {
      toast.error((err as Error).message || 'تعذّر الطباعة على الطابعة')
    } finally { setBusy(false) }
  }

  // Merge all attachments into one PDF (server-side) and print it in a single job.
  async function printAllAttachments() {
    if (!doc || doc.attachments.length === 0) { toast.error('لا توجد مرفقات للطباعة'); return }
    setBusy(true)
    try {
      const url = await documents.fetchCombinedUrl(docId)
      printPdfUrl(url)
    } catch {
      toast.error('تعذّر تجهيز المرفقات للطباعة')
    } finally { setBusy(false) }
  }

  async function removeAttachment(attachmentId: number, fileName: string) {
    if (!(await confirm({ message: `حذف المرفق «${fileName}»؟ لا يمكن التراجع.`, destructive: true }))) return
    setBusy(true); setError('')
    try {
      await documents.removeAttachment(docId, attachmentId)
      await load()
      toast.success('تم حذف المرفق')
    } catch (err) {
      const ax = err as { response?: { data?: { error?: string } } }
      toast.error(ax.response?.data?.error ?? 'تعذّر حذف المرفق')
    } finally { setBusy(false) }
  }

  async function remove() {
    if (!(await confirm({ message: 'حذف هذه الوثيقة؟ (حذف منطقي قابل للاسترجاع)', destructive: true }))) return
    setBusy(true); setError('')
    try {
      await documents.remove(docId)
      toast.success('تم حذف الوثيقة')
      navigate('/app/documents', { replace: true })
    } catch { toast.error('تعذّر حذف الوثيقة (تحقق من صلاحياتك)'); setBusy(false) }
  }

  // File this document into a physical box from the normalized location hierarchy.
  function openArchive() {
    setArchBoxId(doc?.boxId ?? null)
    setArchiveOpen(true)
  }

  async function saveArchive(e: React.FormEvent) {
    e.preventDefault()
    if (!doc) return
    setBusy(true)
    try {
      // The update endpoint is a full replace, so echo the document's current fields and change only the box —
      // otherwise the metadata (description/keywords/category/confidentiality) would be wiped.
      const CONF_LEVEL: Record<string, number> = { Public: 0, Internal: 1, Confidential: 2, HighlyConfidential: 3 }
      await documents.update(docId, {
        title: doc.title,
        description: doc.description,
        categoryId: doc.categoryId,
        owningOrgUnitId: doc.owningOrgUnitId,
        ownerPositionId: doc.ownerPositionId,
        confidentiality: (CONF_LEVEL[doc.confidentiality] ?? 1) as never,
        keywords: doc.keywords,
        documentDate: doc.documentDate ?? null,
        boxId: archBoxId,
      })
      setArchiveOpen(false)
      await load()
      toast.success(archBoxId ? 'تم تحديد مكان الحفظ الفعلي' : 'تم إلغاء مكان الحفظ')
    } catch (err) {
      const ax = err as { response?: { data?: { error?: string } } }
      toast.error(ax.response?.data?.error ?? 'تعذّر حفظ الموقع')
    } finally { setBusy(false) }
  }

  if (error && !doc) return (
    <div><Link to="/app/documents" className="btn btn-ghost"><ArrowRight size={16} className="icon-dir" aria-hidden /> رجوع</Link><p className="login__error">{error}</p></div>
  )
  if (!doc) return <p className="muted">…جارٍ التحميل</p>

  const c = CONFIDENTIALITY_LABELS[doc.confidentiality] ?? { ar: doc.confidentiality, cls: 'internal' }

  return (
    <div>
      <div className="print-header">
        <h2>{doc.title}</h2>
        <div className="print-sub mono">{doc.documentNumber} · {doc.documentTypeName} · {dateTime(new Date().toISOString())}</div>
        <hr />
      </div>

      <header className="page__head">
        <div>
          <span className="mono">{doc.documentNumber}</span>
          <h1>{doc.title}</h1>
        </div>
        <div className="page__headactions">
          <button className="btn btn-ghost" title={fav ? 'إزالة من المفضلة' : 'إضافة للمفضلة'} onClick={toggleFavorite}>
            <Star size={16} aria-hidden fill={fav ? 'currentColor' : 'none'} /> مفضلة
          </button>
          {auth.hasPermission('Export.View') && (
            <button className="btn btn-ghost" title="تنزيل كملف مضغوط" onClick={downloadZip}><Download size={16} aria-hidden /> ZIP</button>
          )}
          {auth.hasPermission('Organization.View') && (
            <button className="btn btn-ghost" onClick={openShare}><Users size={16} aria-hidden /> مشاركة</button>
          )}
          {auth.hasPermission('Documents.Edit') && (
            <Link to={`/app/documents/${docId}/edit`} className="btn btn-primary"><Pencil size={16} aria-hidden /> تعديل</Link>
          )}
          {canPrintDoc && (
            <button className="btn btn-ghost" onClick={() => window.print()}><Printer size={16} aria-hidden /> طباعة</button>
          )}
          {auth.hasPermission('Disposition.Create') && !doc.isTombstone && (
            <button className="btn btn-seal" onClick={() => setReqOpen(true)}>⊗ طلب إتلاف</button>
          )}
          {auth.hasPermission('Documents.Archive') && !doc.isTombstone
            && (doc.status === 'Draft' || doc.status === 'Active') && (
            <button className="btn btn-primary" disabled={busy} onClick={sendToArchive}><PackageCheck size={16} aria-hidden /> إرسال إلى قسم الأرشيف</button>
          )}
          <Link to="/app/documents" className="btn btn-ghost"><ArrowRight size={16} className="icon-dir" aria-hidden /> رجوع للقائمة</Link>
        </div>
      </header>

      {doc.boxId && <DocumentLocation boxId={doc.boxId} />}

      {/* Returned by the archive: the objection belongs on the document, next to the button that
          sends it back — a notification scrolls away, this does not. */}
      {doc.archiveReturnReason && doc.status !== 'PendingArchive' && (
        <div className="login__error" style={{ background: 'rgba(154,99,18,.1)', border: '1px solid var(--brass, #9a6312)', borderRadius: 8, padding: '.7rem 1rem', marginBottom: '1rem' }}>
          أعاد قسم الأرشيف هذه الوثيقة — السبب: {doc.archiveReturnReason}. عالِج الملاحظة ثم أعِد الإرسال.
        </div>
      )}

      {doc.status === 'PendingArchive' && (
        <div className="login__error" style={{ background: 'rgba(30,64,120,.08)', border: '1px solid var(--ink, #1e4078)', borderRadius: 8, padding: '.7rem 1rem', marginBottom: '1rem' }}>
          الوثيقة لدى قسم الأرشيف بانتظار تحديد مكان حفظها
          {doc.sentToArchiveAtUtc ? ` (أُرسلت في ${new Date(doc.sentToArchiveAtUtc).toLocaleString('ar-u-nu-latn')})` : ''}.
        </div>
      )}

      {doc.isTombstone && (
        <div className="login__error" style={{ background: 'rgba(155,34,38,.1)', border: '1px solid var(--seal)', borderRadius: 8, padding: '.7rem 1rem', marginBottom: '1rem' }}>
          ⊗ أُتلِفت هذه الوثيقة{doc.destroyedAtUtc ? ` بتاريخ ${new Date(doc.destroyedAtUtc).toLocaleString('ar-u-nu-latn')}` : ''} — المحتوى غير متوفر، والبيانات الوصفية محفوظة كسجل شاهد بموجب شهادة الإتلاف.
        </div>
      )}

      <div className="detail-grid">
        <motion.section className="doc-card" initial={{ opacity: 0, y: 12 }} animate={{ opacity: 1, y: 0 }}>
          <div className="detail-badges">
            <span className={`status-pill s-${doc.status.toLowerCase()}`}>{DOC_STATUS_LABELS[doc.status] ?? doc.status}</span>
            <span className={`badge ${c.cls}`}>{c.ar}</span>
            <span className="badge internal">الإصدار v{doc.version}</span>
          </div>
          <dl className="detail-list">
            <dt>النوع</dt><dd>{doc.documentTypeName}</dd>
            <dt>تاريخ الوثيقة</dt><dd className="mono">{doc.documentDate ?? '—'}</dd>
            <dt>تاريخ الانتهاء</dt><dd className="mono">{doc.expiryDate ?? '—'}</dd>
            <dt>مدة الحفظ (شهر)</dt><dd className="mono">{doc.retentionMonths}</dd>
            <dt>الكلمات المفتاحية</dt><dd>{doc.keywords ?? '—'}</dd>
            <dt>مكان الحفظ الفيزيائي</dt>
            <dd>{doc.boxCode
              ? doc.boxCode
              : doc.physicalLocationName
              ? `${doc.physicalLocationName}${doc.boxNumber ? ` · صندوق ${doc.boxNumber}` : ''}${doc.fileNumber ? ` · ملف ${doc.fileNumber}` : ''}`
              : '—'}</dd>
            <dt>المجلد</dt>
            <dd className="detail-folder">
              {/* A native <select> cannot colour an option, so the folder the document is actually
                  in is drawn beside it — the same glyph the browser and the properties card use.
                  Absent while the document sits outside every folder. */}
              {doc.folderId != null && (() => {
                const f = folders.find((x) => x.id === doc.folderId)
                return <FolderGlyph className="detail-folder__art" color={f?.color} icon={f?.icon} />
              })()}
              <select value={doc.folderId ?? ''} onChange={(e) => moveToFolder(e.target.value ? Number(e.target.value) : null)}
                style={{ maxWidth: 220 }}>
                <option value="">— بدون مجلد —</option>
                {folders.map((f) => <option key={f.id} value={f.id}>{f.name}</option>)}
              </select>
            </dd>
            <dt>الوصف</dt><dd>{doc.description ?? '—'}</dd>
          </dl>

          {(auth.hasPermission('Archive.Archive') || auth.hasPermission('Documents.Delete')) && (
            <div className="action-bar">
              {auth.hasPermission('Archive.Archive') && (
                <button className="btn btn-seal" disabled={busy} onClick={openArchive}>
                  <Archive size={16} aria-hidden /> {doc.boxId ? 'تغيير مكان الحفظ' : 'تحديد مكان الحفظ'}
                </button>
              )}
              {auth.hasPermission('Documents.Delete') && (
                <button className="btn btn-ghost" disabled={busy} onClick={remove}>حذف الوثيقة</button>
              )}
            </div>
          )}
          {error && <p className="login__error">{error}</p>}

          {/* The QR that proves a printed copy came from here. */}
          <DocumentVerificationCard documentId={docId} />
        </motion.section>

        <motion.aside className="doc-card timeline-card" initial={{ opacity: 0, y: 12 }} animate={{ opacity: 1, y: 0 }} transition={{ delay: 0.08 }}>
          <div className="attach-header">
            {canPrintDoc && doc.attachments.length > 0 && (
              <button className="btn btn-ghost btn-sm" disabled={busy} onClick={printAllAttachments}>
                <Printer size={16} aria-hidden /> طباعة كل المرفقات
              </button>
            )}
          </div>

          <ul className="attach-list">
            {doc.attachments.length === 0 && <li className="muted">لا توجد مرفقات</li>}
            {doc.attachments.map((a) => (
              <li key={a.id} className="attach-item">
                <button className="attach-name" title="معاينة" onClick={() => openPreview(a)}>
                  <span className="attach-ext mono">{a.fileExtension.toUpperCase()}</span>
                  <span className="attach-meta">
                    <span className="attach-fname">
                      {a.fileName}
                      {a.isScanned && <span className="attach-scanned" title="ممسوحة ضوئيًا">مسح ضوئي</span>}
                    </span>
                    <span className="attach-time mono">{dateTime(a.createdAt)}</span>
                  </span>
                </button>
                <span className="attach-actions">
                  <span className="attach-size mono">{formatBytes(a.sizeBytes)}</span>
                  {canPrintDoc && (
                    <button className="attach-view" title="طباعة عبر المتصفح" onClick={() => printAttachment(a)}><Printer size={14} aria-hidden /></button>
                  )}
                  {canPrintDoc && (
                    <button className="attach-view" title="طباعة على طابعة محلية (عبر الوكيل)" disabled={busy}
                      onClick={() => printToPrinter(a)}><Printer size={14} /></button>
                  )}
                  <button className="attach-view" title="تنزيل" onClick={() => documents.download(docId, a.id, a.fileName)}><Download size={14} aria-hidden /></button>
                  {auth.hasPermission('Documents.Edit') && (
                    <button className="attach-remove" title="حذف المرفق" disabled={busy}
                      onClick={() => removeAttachment(a.id, a.fileName)}><X size={14} aria-hidden /></button>
                  )}
                </span>
              </li>
            ))}
          </ul>

          {auth.hasPermission('Documents.Edit') && (
            <>
              {/* No accept filter: the server takes any type, and a narrower picker here
                  would only make the browser hide files it would have accepted. */}
              <input ref={fileInput} type="file" style={{ display: 'none' }} onChange={onUpload} />
              {/* Fallback when no local scan agent is detected: pick a file the scanner produced. */}
              <input ref={scanFallbackInput} type="file" style={{ display: 'none' }} onChange={onScanFallback}
                accept=".pdf" />
              <label className="field scan-name-field">
                <input
                  value={scanName}
                  onChange={(e) => setScanName(e.target.value)}
                  placeholder="اسم المستند الممسوح"
                  maxLength={120}
                />
              </label>
              <div className="action-bar">
                <button className="btn btn-primary" disabled={busy} onClick={() => fileInput.current?.click()}>
                  + رفع مرفق
                </button>
                <button className="btn btn-seal" disabled={busy} onClick={onScan} title="المسح من ماسح ضوئي متصل بجهازك">
                  <Scan size={16} aria-hidden /> مسح ضوئي
                </button>
              </div>
              {scanMsg && <p className="muted scan-msg">{scanMsg}</p>}
            </>
          )}
        </motion.aside>
      </div>

      <CustomFieldsPanel entityType="Document" entityId={docId} documentTypeId={doc.documentTypeId}
        canEdit={auth.hasPermission('Documents.Edit')} />
      <PreservationPackagesPanel docId={docId} documentNumber={doc.documentNumber} />
      {auth.hasPermission('Notes.View') && <DocumentNotesPanel docId={docId} />}

      {shareOpen && (
        <div className="preview-overlay" onClick={() => setShareOpen(false)}>
          <motion.div className="preview-modal" onClick={(e) => e.stopPropagation()}
            style={{ width: 'min(560px, 92vw)', height: 'auto', maxHeight: '90vh', color: 'var(--ink-text, #211d17)' }}
            initial={{ opacity: 0, scale: 0.97 }} animate={{ opacity: 1, scale: 1 }}>
            <header className="preview-head">
              <span className="preview-title">مشاركة الوثيقة · {doc.documentNumber}</span>
              <button type="button" className="btn btn-ghost btn-sm" onClick={() => setShareOpen(false)}><X size={15} aria-hidden /> إغلاق</button>
            </header>
            <div style={{ padding: '1rem', overflow: 'auto' }}>
              <form className="form-grid" onSubmit={doShare}>
                <LabeledSelect label="مشاركة مع" value={shareForm.userId}
                  onChange={(e) => setShareForm((f) => ({ ...f, userId: e.target.value }))}>
                  <option value="">—</option>
                  {shareUsers.map((u) => <option key={u.id} value={u.id}>{u.fullName}</option>)}
                </LabeledSelect>
                <LabeledSelect label="الصلاحية" value={shareForm.canEdit ? '1' : '0'}
                  onChange={(e) => setShareForm((f) => ({ ...f, canEdit: e.target.value === '1' }))}>
                  <option value="0">عرض فقط</option><option value="1">عرض وتعديل</option>
                </LabeledSelect>
                <div className="form-actions"><button className="btn btn-primary" disabled={!shareForm.userId}>مشاركة</button></div>
              </form>
              <h4 className="detail-h3" style={{ marginTop: '1rem' }}>مُشاركة معهم ({shares.length})</h4>
              <ul style={{ listStyle: 'none', margin: 0, padding: 0, display: 'flex', flexDirection: 'column', gap: '.35rem' }}>
                {shares.length === 0 && <li className="muted">لم تتم المشاركة بعد</li>}
                {shares.map((s) => (
                  <li key={s.sharedWithUserId} style={{ display: 'flex', alignItems: 'center', gap: '.6rem', padding: '.4rem .6rem', border: '1px solid var(--color-border,#e2d3b2)', borderRadius: 6 }}>
                    <span style={{ flex: 1 }}>{s.userName}</span>
                    <span className="badge internal">{s.canEdit ? 'عرض وتعديل' : 'عرض'}</span>
                    <button className="btn btn-ghost btn-sm" onClick={() => removeShare(s.sharedWithUserId)}>إزالة</button>
                  </li>
                ))}
              </ul>
            </div>
          </motion.div>
        </div>
      )}

      {/* Sent to the archive — the receipt. The number is the thing to keep, so it is the biggest
          element and can be copied without selecting it by hand. */}
      {sentInfo && (
        <div className="preview-overlay" onClick={() => setSentInfo(null)}>
          <div className="preview-modal" onClick={(e) => e.stopPropagation()}
            role="dialog" aria-modal="true" aria-label="تم الإرسال إلى قسم الأرشيف"
            style={{ width: 'min(460px, 92vw)', height: 'auto' }}>
            <header className="preview-head">
              <span className="preview-title">تم الإرسال إلى قسم الأرشيف</span>
              <button type="button" className="btn btn-ghost btn-sm" onClick={() => setSentInfo(null)}>
                <X size={15} aria-hidden /> إغلاق
              </button>
            </header>
            <div style={{ padding: '1.1rem', textAlign: 'center', color: 'var(--ink-text, #211d17)' }}>
              <PackageCheck size={30} aria-hidden style={{ opacity: .7 }} />
              <p style={{ margin: '.6rem 0 .2rem' }}>
                أُرسلت «{sentInfo.title}» إلى قسم الأرشيف، وهي الآن بانتظار الأرشفة.
              </p>
              <p className="muted" style={{ margin: '0 0 .5rem', fontSize: '.8rem' }}>رقم الوثيقة</p>
              <div className="mono" style={{ fontSize: '1.35rem', fontWeight: 600, letterSpacing: '.02em' }}>
                {sentInfo.number}
              </div>
              <button type="button" className="btn btn-ghost btn-sm" style={{ marginTop: '.5rem' }}
                onClick={() => {
                  navigator.clipboard.writeText(sentInfo.number)
                    .then(() => toast.success('نُسخ رقم الوثيقة'))
                    .catch(() => {})
                }}>نسخ الرقم</button>
              <p className="muted" style={{ fontSize: '.78rem', marginTop: '.8rem' }}>
                سيحدّد قسم الأرشيف موقع الحفظ الفعلي، وسيصلك إشعار عند إتمام الأرشفة أو إرجاعها.
              </p>
            </div>
            <div className="form-actions" style={{ padding: '0 1rem 1rem', justifyContent: 'center' }}>
              <button type="button" className="btn btn-primary" onClick={() => setSentInfo(null)}>تم</button>
            </div>
          </div>
        </div>
      )}

      {reqOpen && doc && (
        <RequestDispositionModal documentId={docId} documentNumber={doc.documentNumber}
          onClose={() => setReqOpen(false)} onDone={load} />
      )}

      {archiveOpen && (
        <div className="preview-overlay" onClick={() => !busy && setArchiveOpen(false)}>
          <motion.form className="preview-modal" onClick={(e) => e.stopPropagation()} onSubmit={saveArchive}
            style={{ width: 'min(560px, 92vw)', height: 'auto', maxHeight: '90vh' }}
            initial={{ opacity: 0, scale: 0.97 }} animate={{ opacity: 1, scale: 1 }}>
            <header className="preview-head">
              <span className="preview-title">تغيير مكان الحفظ الفعلي · {doc.documentNumber}</span>
              <button type="button" className="btn btn-ghost btn-sm" onClick={() => setArchiveOpen(false)}><X size={15} aria-hidden /> إغلاق</button>
            </header>
            <div className="form-grid" style={{ padding: '1rem', overflow: 'auto', color: 'var(--ink-text, #211d17)' }}>
              <LocationPicker value={archBoxId} onChange={setArchBoxId} />
            </div>
            <div className="form-actions" style={{ padding: '0 1rem 1rem' }}>
              <button className="btn btn-primary" disabled={busy}>{busy ? '…' : 'حفظ'}</button>
              <button type="button" className="btn btn-ghost" onClick={() => setArchiveOpen(false)}>إلغاء</button>
            </div>
          </motion.form>
        </div>
      )}

      {preview && (
        <div className="preview-overlay" onClick={closePreview}>
          <motion.div className="preview-modal" onClick={(e) => e.stopPropagation()}
            initial={{ opacity: 0, scale: 0.97 }} animate={{ opacity: 1, scale: 1 }}>
            <header className="preview-head">
              <span className="preview-title">{preview.name}</span>
              <span className="preview-actions">
                {canPrintDoc && <button className="btn btn-ghost btn-sm" onClick={printPreview}><Printer size={15} aria-hidden /> طباعة</button>}
                <a className="btn btn-primary btn-sm" href={preview.url} download={preview.name}><Download size={15} aria-hidden /> تنزيل</a>
                <button className="btn btn-ghost btn-sm" onClick={closePreview}><X size={15} aria-hidden /> إغلاق</button>
              </span>
            </header>
            <div className="preview-body">
              {preview.type.includes('pdf') ? (
                <iframe ref={previewFrame} title="preview" src={preview.url} className="preview-frame" />
              ) : preview.type.startsWith('image/') ? (
                <img src={preview.url} alt={preview.name} className="preview-img" />
              ) : (
                <p className="muted preview-unsupported">لا يمكن معاينة هذا النوع داخل الصفحة — استخدم زر التنزيل.</p>
              )}
            </div>
          </motion.div>
        </div>
      )}
    </div>
  )
}
