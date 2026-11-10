import { useCallback, useState } from 'react'

/**
 * Attaching files to a letter straight from a list row's context menu.
 *
 * The file input is created on demand rather than rendered: a hidden <input> inside the menu
 * would be torn down the moment an item is selected — the menu unmounts before the file dialog
 * returns — and one parked at page level would need a ref read during render. Building it in the
 * click handler avoids both, and lets the chosen row be captured in the closure.
 */
export function useMailAttach(opts: {
  upload: (mailId: number, file: File) => Promise<unknown>
  /** Re-fetches the list so the attachment indicator updates. */
  onDone: () => Promise<unknown> | unknown
  onError: (message: string) => void
  onSuccess: (message: string) => void
}) {
  const [busy, setBusy] = useState(false)

  const uploadAll = useCallback(async (mailId: number, files: File[]) => {
    if (files.length === 0) return
    setBusy(true)
    let ok = 0
    try {
      // One at a time, reporting per file: a single rejected file must not make the whole
      // batch look like it failed.
      for (const f of files) {
        try { await opts.upload(mailId, f); ok++ }
        catch { opts.onError(`تعذّر رفع ${f.name}`) }
      }
      if (ok > 0) { await opts.onDone(); opts.onSuccess(`تم إرفاق ${ok} ملف`) }
    } finally { setBusy(false) }
  }, [opts])

  /** Opens the file dialog and attaches whatever is chosen to this letter. */
  const pickFor = useCallback((mailId: number) => {
    const input = document.createElement('input')
    input.type = 'file'
    input.multiple = true
    input.style.display = 'none'
    input.addEventListener('change', () => {
      const files = Array.from(input.files ?? [])
      input.remove()
      void uploadAll(mailId, files)
    })
    // Cancelling the dialog fires no 'change', so the element is cleaned up on close too.
    // 'cancel' is not supported everywhere; the detached node is tiny and harmless if it lingers.
    input.addEventListener('cancel', () => input.remove())
    document.body.appendChild(input)
    input.click()
  }, [uploadAll])

  return { pickFor, busy }
}
