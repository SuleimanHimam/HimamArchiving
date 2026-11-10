/**
 * The system's own mark: three sheets seen in isometric, the middle one amber.
 *
 * Drawn as vector rather than an <img> of the exported icon, so it stays crisp at the 24–46px it is
 * used at and carries no bitmap into the bundle. The tile is a flat fill here, not the gradient the
 * exported icon uses: this component can appear twice on a page, and duplicated gradient ids are a
 * class of bug not worth inviting for a difference nobody sees at 30px.
 *
 * Shown wherever the *system* identifies itself and the institution has uploaded no logo of its own
 * (branding settings replace it). Not the icon for the "external organisation" concept — that is
 * lucide's Landmark, and the two must not be confused.
 */
export default function BrandMark({ size = 24 }: { size?: number }) {
  return (
    <svg width={size} height={size} viewBox="0 0 64 64" role="img" aria-label="الديوان الإلكتروني">
      <rect width="64" height="64" rx="14" fill="#17489B" />
      <g transform="translate(32 33) scale(1.12) translate(-32 -33)">
        <path d="M32 40.5 55 51 32 61.5 9 51Z" fill="#C9D8EE" />
        <path d="M32 38 55 48.5 32 59 9 48.5Z" fill="#FFFFFF" />
        <path d="M32 31 55 41.5 32 52 9 41.5Z" fill="#C8820F" />
        <path d="M32 28.5 55 39 32 49.5 9 39Z" fill="#F5A623" />
        <path d="M32 21.5 55 32 32 42.5 9 32Z" fill="#C9D8EE" />
        <path d="M32 19 55 29.5 32 40 9 29.5Z" fill="#FFFFFF" />
        <path d="M44.5 25.2 55 29.5 47.5 33Z" fill="#2C6FD6" />
        <g stroke="#1E56AD" strokeWidth="2.6" strokeLinecap="round">
          <path d="M23.5 29.6 30.5 33" />
          <path d="M27.5 27.7 34.5 31.1" />
          <path d="M31.5 25.8 38.5 29.2" />
        </g>
      </g>
    </svg>
  )
}
