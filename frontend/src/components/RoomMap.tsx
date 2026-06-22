import { type Room, type RoomConnection, CONNECTION_LABELS } from '../lib/locations'

/** Lightweight SVG node/edge view: the selected room in the centre, its connected rooms around it. */
export default function RoomMap({ rooms, connections, centerId }: { rooms: Room[]; connections: RoomConnection[]; centerId: number }) {
  if (connections.length === 0) return null
  const W = 640, H = 240, cx = W / 2, cy = H / 2, R = 85
  const center = rooms.find((r) => r.id === centerId)
  const nodes = connections.map((c, i) => {
    const a = (2 * Math.PI * i) / connections.length - Math.PI / 2
    return { id: c.connectedRoomId, name: c.connectedRoomName, type: c.connectionType, x: cx + R * Math.cos(a) * 1.7, y: cy + R * Math.sin(a) }
  })

  return (
    <svg viewBox={`0 0 ${W} ${H}`} style={{ width: '100%', height: 'auto', marginTop: '.6rem' }}>
      {nodes.map((n) => (
        <g key={n.id}>
          <line x1={cx} y1={cy} x2={n.x} y2={n.y} stroke="var(--brass, #B0892D)" strokeWidth={1.5} />
          <text x={(cx + n.x) / 2} y={(cy + n.y) / 2 - 4} textAnchor="middle" fontSize="10" fill="var(--text-muted, #6e6552)">{CONNECTION_LABELS[n.type ?? ''] ?? n.type}</text>
        </g>
      ))}
      {nodes.map((n) => (
        <g key={`n${n.id}`}>
          <circle cx={n.x} cy={n.y} r={26} fill="var(--paper, #FBF7EC)" stroke="var(--border, #E2D3B2)" strokeWidth={1.5} />
          <text x={n.x} y={n.y + 4} textAnchor="middle" fontSize="11" fill="var(--ink, #14213D)">{n.name.slice(0, 8)}</text>
        </g>
      ))}
      <circle cx={cx} cy={cy} r={30} fill="var(--ink, #14213D)" />
      <text x={cx} y={cy + 4} textAnchor="middle" fontSize="12" fill="#fff">{(center?.nameAr ?? '').slice(0, 9)}</text>
    </svg>
  )
}
