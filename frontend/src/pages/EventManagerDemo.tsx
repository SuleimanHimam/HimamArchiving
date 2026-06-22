import { EventManager, type Event } from '@/components/ui/event-manager'

const now = new Date()
const at = (dayOffset: number, h: number) => {
  const d = new Date(now.getFullYear(), now.getMonth(), now.getDate() + dayOffset, h, 0, 0)
  return d
}
const sample: Event[] = [
  { id: '1', title: 'Team Standup', description: 'Daily sync', startTime: at(0, 9), endTime: at(0, 10), color: 'blue', category: 'Meeting', tags: ['Team', 'Work'] },
  { id: '2', title: 'Client Review', description: 'Quarterly review', startTime: at(0, 14), endTime: at(0, 15), color: 'green', category: 'Meeting', tags: ['Client', 'Important'] },
  { id: '3', title: 'Submit Report', startTime: at(2, 11), endTime: at(2, 12), color: 'red', category: 'Task', tags: ['Urgent'] },
]

export default function EventManagerDemo() {
  return (
    <div dir="ltr" className="p-4">
      <EventManager events={sample} defaultView="month" />
    </div>
  )
}
