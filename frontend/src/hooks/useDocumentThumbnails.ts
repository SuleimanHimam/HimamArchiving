import { useEffect, useMemo, useState } from 'react'
import { documents, type DocumentListItem } from '../lib/documents'
import { isImageExtension } from '../lib/fileFormats'

const MIME: Record<string, string> = {
  jpg: 'image/jpeg', jpeg: 'image/jpeg', png: 'image/png',
  gif: 'image/gif', webp: 'image/webp', avif: 'image/avif', svg: 'image/svg+xml',
}

/**
 * Object URLs for the image attachments on the current page of documents, so a
 * picture can show itself instead of a placeholder card.
 *
 * Attachments are behind the bearer token, so an `<img src>` pointing at the API
 * would come back 401 — each one has to be fetched and turned into a blob URL.
 * Only the page in view is fetched, and every URL is revoked when the page
 * changes or the component unmounts; a leaked blob URL pins the whole file in
 * memory for the life of the tab.
 */
export function useDocumentThumbnails(items: DocumentListItem[]): Record<number, string> {
  const [urls, setUrls] = useState<Record<number, string>>({})

  // The list is refetched on a timer, so a new array arrives every 30 seconds
  // with the same contents. Keying on what actually matters stops the thumbnails
  // being downloaded again each time.
  const signature = useMemo(
    () => items
      .filter((d) => d.primaryAttachmentId && isImageExtension(d.fileExtension))
      .map((d) => `${d.id}:${d.primaryAttachmentId}`)
      .join(','),
    [items],
  )

  useEffect(() => {
    let alive = true
    const created: string[] = []

    // An empty signature still goes through Promise.all, which resolves on a
    // microtask — setting state synchronously here would cascade a render.
    const wanted = signature ? signature.split(',') : []

    Promise.all(wanted.map(async (pair) => {
      const [docId, attId] = pair.split(':').map(Number)
      const ext = items.find((d) => d.id === docId)?.fileExtension ?? ''
      try {
        const url = await documents.fetchObjectUrl(
          docId, attId, MIME[ext.toLowerCase()] ?? 'application/octet-stream')
        created.push(url)
        return [docId, url] as const
      } catch {
        return null   // a single unreadable attachment falls back to its card
      }
    })).then((pairs) => {
      if (!alive) return
      setUrls(Object.fromEntries(pairs.filter((p) => p !== null)))
    })

    return () => {
      alive = false
      for (const url of created) URL.revokeObjectURL(url)
    }
  // `items` is only read to look an extension up; `signature` is what decides a refetch.
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [signature])

  return urls
}
