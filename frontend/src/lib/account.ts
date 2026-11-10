import { api } from './api'
import { auth, type AuthUser } from './auth'

/** Longest edge of the stored avatar, in px. */
const AVATAR_SIZE = 256
/** Mirrors User.AvatarMaxChars server-side; the API rejects anything larger. */
const AVATAR_MAX_CHARS = 400_000

export const account = {
  /** Self-service password change. The server also revokes other sessions. */
  changePassword: (currentPassword: string, newPassword: string) =>
    api.post('/auth/change-password', { currentPassword, newPassword }).then(() => undefined),

  /** Sets (or, with null, clears) the caller's own picture. Returns the refreshed user. */
  async setAvatar(avatarUrl: string | null): Promise<AuthUser> {
    const { data } = await api.put<AuthUser>('/auth/avatar', { avatarUrl })
    const token = auth.getAccessToken()
    const refresh = auth.getRefreshToken()
    if (token && refresh) auth.setSession(token, refresh, data)
    return data
  },

  /**
   * Chooses (or, with null, clears) the design this user's outgoing letters print on.
   *
   * The server refuses a design the user could not already print with, so a stale id from an
   * open tab cannot pin another department's letterhead onto their letters.
   */
  async setDefaultTemplate(templateId: number | null, target: 0 | 1 = 1): Promise<AuthUser> {
    const { data } = await api.put<AuthUser>('/auth/default-template', { templateId, target })
    const token = auth.getAccessToken()
    const refresh = auth.getRefreshToken()
    if (token && refresh) auth.setSession(token, refresh, data)
    return data
  },

  /** Sets (or, with null, clears) the caller's own signature or stamp. */
  async setSeal(kind: SealKind, imageUrl: string | null): Promise<AuthUser> {
    const { data } = await api.put<AuthUser>('/auth/seal', { kind, imageUrl })
    const token = auth.getAccessToken()
    const refresh = auth.getRefreshToken()
    if (token && refresh) auth.setSession(token, refresh, data)
    return data
  },

  /** The seal images to print on a letter this user sent. */
  seal: (userId: number) =>
    api.get<UserSeal>(`/auth/users/${userId}/seal`).then((r) => r.data),
}

export const SealKind = { Signature: 0, Stamp: 1 } as const
export type SealKind = (typeof SealKind)[keyof typeof SealKind]

export interface UserSeal { userId: number; signatureUrl: string | null; stampUrl: string | null }

/**
 * Reads an image file and re-encodes it to a small square JPEG data URI.
 *
 * Done client-side so the browser never uploads a 5 MB phone photo to store a
 * 40 px chip: it is centre-cropped to a square, scaled to AVATAR_SIZE, and
 * re-encoded — which also strips EXIF (including any GPS tag the camera wrote).
 */
export function fileToAvatarDataUrl(file: File): Promise<string> {
  return new Promise((resolve, reject) => {
    if (!file.type.startsWith('image/')) {
      reject(new Error('الملف المختار ليس صورة'))
      return
    }

    const url = URL.createObjectURL(file)
    const img = new Image()

    img.onload = () => {
      URL.revokeObjectURL(url)
      const side = Math.min(img.naturalWidth, img.naturalHeight)
      if (!side) { reject(new Error('تعذّر قراءة الصورة')); return }

      const canvas = document.createElement('canvas')
      canvas.width = AVATAR_SIZE
      canvas.height = AVATAR_SIZE
      const ctx = canvas.getContext('2d')
      if (!ctx) { reject(new Error('تعذّر معالجة الصورة')); return }

      // Centre-crop to a square, then scale — avoids the squashed look a plain
      // stretch gives a portrait photo.
      ctx.drawImage(
        img,
        (img.naturalWidth - side) / 2, (img.naturalHeight - side) / 2, side, side,
        0, 0, AVATAR_SIZE, AVATAR_SIZE,
      )

      let quality = 0.85
      let out = canvas.toDataURL('image/jpeg', quality)
      // Busy photographs can still exceed the cap at high quality; step down
      // rather than fail the upload.
      while (out.length > AVATAR_MAX_CHARS && quality > 0.4) {
        quality -= 0.15
        out = canvas.toDataURL('image/jpeg', quality)
      }
      if (out.length > AVATAR_MAX_CHARS) { reject(new Error('حجم الصورة كبير جدًا')); return }

      resolve(out)
    }

    img.onerror = () => { URL.revokeObjectURL(url); reject(new Error('تعذّر قراءة الصورة')) }
    img.src = url
  })
}

/** Bounding box a stored signature/stamp is scaled to fit inside, in px. */
const SEAL_MAX_W = 600
const SEAL_MAX_H = 300
/** Mirrors User.SealMaxChars server-side. */
const SEAL_MAX_CHARS = 400_000

/**
 * Reads a signature or stamp image and re-encodes it to a PNG data URI.
 *
 * Deliberately not the avatar path: a signature is wide, not square, so it is scaled to fit
 * inside a box rather than centre-cropped — cropping would cut the ends off a signature. PNG
 * rather than JPEG because these are usually transparent cut-outs that have to sit over the
 * letter text; JPEG would flatten that transparency to a white block.
 */
export function fileToSealDataUrl(file: File): Promise<string> {
  return new Promise((resolve, reject) => {
    if (!file.type.startsWith('image/')) {
      reject(new Error('الملف المختار ليس صورة'))
      return
    }

    const url = URL.createObjectURL(file)
    const img = new Image()

    img.onload = () => {
      URL.revokeObjectURL(url)
      const { naturalWidth: w, naturalHeight: h } = img
      if (!w || !h) { reject(new Error('تعذّر قراءة الصورة')); return }

      // Only ever scale down — enlarging a small scan just blurs it.
      const scale = Math.min(1, SEAL_MAX_W / w, SEAL_MAX_H / h)
      const canvas = document.createElement('canvas')
      canvas.width = Math.max(1, Math.round(w * scale))
      canvas.height = Math.max(1, Math.round(h * scale))
      const ctx = canvas.getContext('2d')
      if (!ctx) { reject(new Error('تعذّر معالجة الصورة')); return }
      ctx.drawImage(img, 0, 0, canvas.width, canvas.height)

      const out = canvas.toDataURL('image/png')
      // PNG has no quality dial to step down, so an oversized image is refused rather than
      // silently degraded. At 600×300 this only trips on very noisy scans.
      if (out.length > SEAL_MAX_CHARS) {
        reject(new Error('حجم الصورة كبير جدًا — جرّب صورة أوضح بخلفية شفافة'))
        return
      }
      resolve(out)
    }

    img.onerror = () => { URL.revokeObjectURL(url); reject(new Error('تعذّر قراءة الصورة')) }
    img.src = url
  })
}
